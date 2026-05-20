using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace TINWeb.Services
{
    public class EmailEventsService
    {
        private static readonly HttpClient HttpClient = new();
        private readonly EmailEventsSettings _settings;
        private readonly ILogger<EmailEventsService> _logger;

        public EmailEventsService(
            IOptions<EmailEventsSettings> settings,
            ILogger<EmailEventsService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public bool IsEnabled => _settings.Enabled;

        public async Task<EmailEventsQueryResult> QueryAsync(DateTime startUtc, DateTime endUtc)
        {
            if (!_settings.Enabled)
            {
                return new EmailEventsQueryResult
                {
                    Error = "Email event querying is disabled. Configure EmailEvents:Enabled=true to enable this page."
                };
            }

            if (string.IsNullOrWhiteSpace(_settings.CommandTemplate))
            {
                return new EmailEventsQueryResult
                {
                    Error = "EmailEvents:CommandTemplate is not configured."
                };
            }

            var command = BuildCommand(_settings.CommandTemplate, startUtc, endUtc);
            var azExecutablePath = ResolveAzExecutablePath();

            var primaryResult = await ExecuteAzCommandAsync(azExecutablePath, command);
            if (primaryResult.StartErrorResult != null)
            {
                if (TryExtractWorkspaceIdentifier(_settings.CommandTemplate, out var workspaceIdentifier))
                {
                    var apiFallbackResult = await QueryFallbackViaApiAsync(workspaceIdentifier, startUtc, endUtc);
                    if (string.IsNullOrWhiteSpace(apiFallbackResult.Error))
                    {
                        return apiFallbackResult;
                    }
                }

                return primaryResult.StartErrorResult;
            }

            if (primaryResult.ExitCode == 0)
            {
                return new EmailEventsQueryResult
                {
                    Rows = ParseRows(primaryResult.Stdout),
                    RawJson = primaryResult.Stdout
                };
            }

            if (ShouldUseFallbackQueries(primaryResult.Stderr)
                && TryExtractWorkspaceIdentifier(_settings.CommandTemplate, out var workspaceIdentifier))
            {
                var fallbackRows = await QueryFallbackCommandsAsync(azExecutablePath, workspaceIdentifier, startUtc, endUtc);
                return new EmailEventsQueryResult
                {
                    Rows = fallbackRows,
                    RawJson = primaryResult.Stdout
                };
            }

            _logger.LogWarning("Azure CLI email events query failed. ExitCode={ExitCode}, Stderr={Stderr}", primaryResult.ExitCode, primaryResult.Stderr);
            return new EmailEventsQueryResult
            {
                Error = string.IsNullOrWhiteSpace(primaryResult.Stderr)
                    ? "Azure CLI query failed."
                    : primaryResult.Stderr.Trim()
            };
        }

        private async Task<AzCommandExecutionResult> ExecuteAzCommandAsync(string azExecutablePath, string command)
        {
            var processStartInfo = BuildProcessStartInfo(azExecutablePath, command);

            using var process = new Process { StartInfo = processStartInfo };
            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Azure CLI process. FileName={FileName}", azExecutablePath);
                return new AzCommandExecutionResult
                {
                    StartErrorResult = new EmailEventsQueryResult
                    {
                        Error = $"Could not start Azure CLI. Attempted path: {azExecutablePath}. Ensure Azure CLI is installed and available to the app process."
                    }
                };
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Clamp(_settings.CommandTimeoutSeconds, 5, 300)));

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                TryKillProcess(process);
                return new AzCommandExecutionResult
                {
                    StartErrorResult = new EmailEventsQueryResult
                    {
                        Error = "Azure CLI query timed out."
                    }
                };
            }

            return new AzCommandExecutionResult
            {
                ExitCode = process.ExitCode,
                Stdout = await stdoutTask,
                Stderr = await stderrTask
            };
        }

        private static ProcessStartInfo BuildProcessStartInfo(string azExecutablePath, string command)
        {
            // .cmd/.bat scripts require cmd.exe when UseShellExecute=false.
            if (azExecutablePath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
                || azExecutablePath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            {
                var safeExecutablePath = NormalizeExecutablePath(azExecutablePath).Replace("\"", "\"\"");
                return new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/d /s /c \"\"{safeExecutablePath}\" {command}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
            }

            return new ProcessStartInfo
            {
                FileName = azExecutablePath,
                Arguments = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        private async Task<List<EmailEventRow>> QueryFallbackCommandsAsync(string azExecutablePath, string workspaceIdentifier, DateTime startUtc, DateTime endUtc)
        {
            var allRows = new List<EmailEventRow>();

            foreach (var fallbackCommand in BuildFallbackCommands(workspaceIdentifier, startUtc, endUtc))
            {
                var fallbackResult = await ExecuteAzCommandAsync(azExecutablePath, fallbackCommand);
                if (fallbackResult.StartErrorResult != null)
                {
                    continue;
                }

                if (fallbackResult.ExitCode != 0)
                {
                    if (!IsIgnorableQueryError(fallbackResult.Stderr))
                    {
                        _logger.LogWarning("Fallback Azure CLI query failed. Stderr={Stderr}", fallbackResult.Stderr);
                    }

                    continue;
                }

                allRows.AddRange(ParseRows(fallbackResult.Stdout));
            }

            return allRows
                .GroupBy(row => string.Join("|",
                    row.TimestampUtc?.ToString("O") ?? string.Empty,
                    row.EventType ?? string.Empty,
                    row.Recipient ?? string.Empty,
                    row.MessageId ?? string.Empty,
                    row.Subject ?? string.Empty))
                .Select(group => group.First())
                .OrderByDescending(row => row.TimestampUtc)
                .ToList();
        }

        private async Task<EmailEventsQueryResult> QueryFallbackViaApiAsync(string workspaceIdentifier, DateTime startUtc, DateTime endUtc)
        {
            try
            {
                var accessToken = await GetManagedIdentityAccessTokenAsync();
                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    return new EmailEventsQueryResult
                    {
                        Error = "Azure CLI was unavailable and managed identity token acquisition failed for Log Analytics API."
                    };
                }

                var workspaceForApi = NormalizeWorkspaceIdentifierForApi(workspaceIdentifier);
                var allRows = new List<EmailEventRow>();

                foreach (var kql in BuildFallbackKqlQueries(startUtc, endUtc))
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.loganalytics.io/v1/workspaces/{workspaceForApi}/query")
                    {
                        Content = new StringContent(JsonSerializer.Serialize(new { query = kql }), Encoding.UTF8, "application/json")
                    };

                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                    using var response = await HttpClient.SendAsync(request);
                    var body = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        if (!IsIgnorableQueryError(body))
                        {
                            _logger.LogWarning("Log Analytics API fallback query failed. Status={StatusCode}, Body={Body}", response.StatusCode, body);
                        }

                        continue;
                    }

                    allRows.AddRange(ParseRows(body));
                }

                return new EmailEventsQueryResult
                {
                    Rows = allRows
                        .GroupBy(row => string.Join("|",
                            row.TimestampUtc?.ToString("O") ?? string.Empty,
                            row.EventType ?? string.Empty,
                            row.Recipient ?? string.Empty,
                            row.MessageId ?? string.Empty,
                            row.Subject ?? string.Empty))
                        .Select(group => group.First())
                        .OrderByDescending(row => row.TimestampUtc)
                        .ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Log Analytics API fallback failed.");
                return new EmailEventsQueryResult
                {
                    Error = "Azure CLI was unavailable and direct Log Analytics API fallback failed."
                };
            }
        }

        private static IEnumerable<string> BuildFallbackCommands(string workspaceIdentifier, DateTime startUtc, DateTime endUtc)
        {
            foreach (var kql in BuildFallbackKqlQueries(startUtc, endUtc))
            {
                yield return $"monitor log-analytics query --workspace {workspaceIdentifier} --analytics-query \"{kql}\" --output json";
            }
        }

        private static IEnumerable<string> BuildFallbackKqlQueries(DateTime startUtc, DateTime endUtc)
        {
            var start = startUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");
            var end = endUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");

            foreach (var table in new[]
            {
                "ACSEmailSendMailOperational",
                "ACSEmailStatusUpdateOperational",
                "ACSEmailUserEngagementOperational",
                "EmailSendMailOperational",
                "EmailStatusUpdateOperational",
                "EmailUserEngagementOperational"
            })
            {
                yield return $"{table} | where TimeGenerated between (datetime({start}) .. datetime({end})) | project TimeGenerated, EventType=coalesce(tostring(column_ifexists('Status','')), tostring(column_ifexists('DeliveryStatus','')), tostring(column_ifexists('EventType','')), tostring(column_ifexists('OperationName','')), tostring(column_ifexists('Category',''))), Recipient=tostring(coalesce(column_ifexists('RecipientId',''), column_ifexists('Recipient',''), column_ifexists('RecipientAddress',''), column_ifexists('EmailAddress',''))), Subject=tostring(column_ifexists('Subject','')), MessageId=tostring(coalesce(column_ifexists('MessageId',''), column_ifexists('CorrelationId',''), column_ifexists('OperationId',''))), Details=tostring(coalesce(column_ifexists('StatusDetails',''), column_ifexists('DiagnosticCode',''), column_ifexists('ErrorMessage',''), column_ifexists('ResultDescription',''))) | order by TimeGenerated desc";
            }

            yield return $"AzureDiagnostics | where TimeGenerated between (datetime({start}) .. datetime({end})) | where ResourceProvider == 'MICROSOFT.COMMUNICATION' or ResourceType =~ 'MICROSOFT.COMMUNICATION/COMMUNICATIONSERVICES' or Category in ('EmailSendMailOperational', 'EmailStatusUpdateOperational', 'EmailUserEngagementOperational') | project TimeGenerated, EventType=coalesce(tostring(column_ifexists('Category','')), tostring(column_ifexists('OperationName','')), tostring(column_ifexists('status_s','')), tostring(column_ifexists('deliveryStatus_s',''))), Recipient=tostring(coalesce(column_ifexists('RecipientId_s',''), column_ifexists('recipient_s',''), column_ifexists('RecipientAddress_s',''), column_ifexists('EmailAddress_s',''))), Subject=tostring(coalesce(column_ifexists('Subject_s',''), column_ifexists('subject_s',''))), MessageId=tostring(coalesce(column_ifexists('MessageId_g',''), column_ifexists('MessageId_s',''), column_ifexists('CorrelationId_g',''), column_ifexists('CorrelationId_s',''), column_ifexists('OperationId_g',''), column_ifexists('OperationId_s',''))), Details=tostring(coalesce(column_ifexists('statusDetails_s',''), column_ifexists('DiagnosticCode_s',''), column_ifexists('ErrorMessage_s',''), column_ifexists('ResultDescription',''))) | order by TimeGenerated desc";
        }

        private async Task<string?> GetManagedIdentityAccessTokenAsync()
        {
            var identityEndpoint = Environment.GetEnvironmentVariable("IDENTITY_ENDPOINT");
            var identityHeader = Environment.GetEnvironmentVariable("IDENTITY_HEADER");
            if (!string.IsNullOrWhiteSpace(identityEndpoint) && !string.IsNullOrWhiteSpace(identityHeader))
            {
                var url = $"{identityEndpoint}?resource={Uri.EscapeDataString("https://api.loganalytics.io")}&api-version=2019-08-01";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("X-IDENTITY-HEADER", identityHeader);

                using var response = await HttpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(content);
                    if (TryGetPropertyIgnoreCase(doc.RootElement, "access_token", out var tokenElement))
                    {
                        return tokenElement.GetString();
                    }
                }
            }

            var msiEndpoint = Environment.GetEnvironmentVariable("MSI_ENDPOINT");
            var msiSecret = Environment.GetEnvironmentVariable("MSI_SECRET");
            if (!string.IsNullOrWhiteSpace(msiEndpoint) && !string.IsNullOrWhiteSpace(msiSecret))
            {
                var url = $"{msiEndpoint}?resource={Uri.EscapeDataString("https://api.loganalytics.io")}&api-version=2017-09-01";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Secret", msiSecret);

                using var response = await HttpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(content);
                    if (TryGetPropertyIgnoreCase(doc.RootElement, "access_token", out var tokenElement))
                    {
                        return tokenElement.GetString();
                    }
                }
            }

            return null;
        }

        private static string NormalizeWorkspaceIdentifierForApi(string workspaceIdentifier)
        {
            var trimmed = workspaceIdentifier.Trim().Trim('"', '\'');

            var guidMatch = Regex.Match(trimmed, "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
            if (guidMatch.Success)
            {
                return guidMatch.Value;
            }

            if (trimmed.StartsWith("/subscriptions/", StringComparison.OrdinalIgnoreCase))
            {
                var parts = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return parts.LastOrDefault() ?? trimmed;
            }

            return trimmed;
        }

        private static bool TryExtractWorkspaceIdentifier(string commandTemplate, out string workspaceIdentifier)
        {
            var match = Regex.Match(commandTemplate, @"--workspace\s+(?<workspace>\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                workspaceIdentifier = match.Groups["workspace"].Value.Trim();
                return !string.IsNullOrWhiteSpace(workspaceIdentifier);
            }

            workspaceIdentifier = string.Empty;
            return false;
        }

        private static bool ShouldUseFallbackQueries(string? stderr)
        {
            return !string.IsNullOrWhiteSpace(stderr)
                && (stderr.Contains("SEM0529", StringComparison.OrdinalIgnoreCase)
                    || stderr.Contains("SemanticError", StringComparison.OrdinalIgnoreCase)
                    || stderr.Contains("PathNotFoundError", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsIgnorableQueryError(string? stderr)
        {
            return !string.IsNullOrWhiteSpace(stderr)
                && (stderr.Contains("SEM0529", StringComparison.OrdinalIgnoreCase)
                    || stderr.Contains("SEM0100", StringComparison.OrdinalIgnoreCase)
                    || stderr.Contains("PathNotFoundError", StringComparison.OrdinalIgnoreCase));
        }

        private string ResolveAzExecutablePath()
        {
            var configuredPath = NormalizeExecutablePath(string.IsNullOrWhiteSpace(_settings.AzPath) ? "az" : _settings.AzPath.Trim());
            if (Path.IsPathRooted(configuredPath) && File.Exists(configuredPath))
            {
                return configuredPath;
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return configuredPath;
            }

            foreach (var candidate in GetWindowsAzCandidates(configuredPath))
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            var pathCandidate = FindWindowsExecutableOnPath(configuredPath)
                ?? FindWindowsExecutableOnPath("az.cmd")
                ?? FindWindowsExecutableOnPath("az");
            if (!string.IsNullOrWhiteSpace(pathCandidate))
            {
                return pathCandidate;
            }

            return configuredPath;
        }

        private static IEnumerable<string> GetWindowsAzCandidates(string configuredPath)
        {
            yield return configuredPath;

            if (!configuredPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
            {
                yield return configuredPath + ".cmd";
            }

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrWhiteSpace(programFiles))
            {
                yield return Path.Combine(programFiles, "Microsoft SDKs", "Azure", "CLI2", "wbin", "az.cmd");
            }

            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrWhiteSpace(programFilesX86))
            {
                yield return Path.Combine(programFilesX86, "Microsoft SDKs", "Azure", "CLI2", "wbin", "az.cmd");
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                yield return Path.Combine(localAppData, "Programs", "Azure CLI", "wbin", "az.cmd");
            }
        }

        private static string? FindWindowsExecutableOnPath(string executable)
        {
            var normalized = NormalizeExecutablePath(executable);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            var path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var hasExtension = Path.HasExtension(normalized);
            foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (string.IsNullOrWhiteSpace(dir))
                {
                    continue;
                }

                var direct = Path.Combine(dir, normalized);
                if (File.Exists(direct))
                {
                    return direct;
                }

                if (!hasExtension)
                {
                    var cmd = direct + ".cmd";
                    if (File.Exists(cmd))
                    {
                        return cmd;
                    }

                    var exe = direct + ".exe";
                    if (File.Exists(exe))
                    {
                        return exe;
                    }
                }
            }

            return null;
        }

        private static string NormalizeExecutablePath(string value)
        {
            return value.Trim().Trim('"', '\'');
        }

        private static void TryKillProcess(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Ignore cleanup failures.
            }
        }

        private static string BuildCommand(string template, DateTime startUtc, DateTime endUtc)
        {
            return template
                .Replace("{startUtc}", startUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"), StringComparison.Ordinal)
                .Replace("{endUtc}", endUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"), StringComparison.Ordinal)
                .Replace("{startDate}", startUtc.ToString("yyyy-MM-dd"), StringComparison.Ordinal)
                .Replace("{endDate}", endUtc.ToString("yyyy-MM-dd"), StringComparison.Ordinal);
        }

        private static List<EmailEventRow> ParseRows(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<EmailEventRow>();
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    return ParseObjectArray(root);
                }

                if (root.ValueKind == JsonValueKind.Object
                    && TryGetPropertyIgnoreCase(root, "tables", out var tables)
                    && tables.ValueKind == JsonValueKind.Array)
                {
                    return ParseTableRows(tables);
                }
            }
            catch
            {
                // Return empty rows when output is not JSON.
            }

            return new List<EmailEventRow>();
        }

        private static List<EmailEventRow> ParseObjectArray(JsonElement arrayElement)
        {
            var rows = new List<EmailEventRow>();
            foreach (var item in arrayElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                rows.Add(MapObjectRow(item));
            }

            return rows
                .OrderByDescending(r => r.TimestampUtc)
                .ToList();
        }

        private static List<EmailEventRow> ParseTableRows(JsonElement tablesElement)
        {
            var rows = new List<EmailEventRow>();

            foreach (var table in tablesElement.EnumerateArray())
            {
                if (table.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!TryGetPropertyIgnoreCase(table, "columns", out var columns)
                    || !TryGetPropertyIgnoreCase(table, "rows", out var tableRows)
                    || columns.ValueKind != JsonValueKind.Array
                    || tableRows.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var names = new List<string>();
                foreach (var column in columns.EnumerateArray())
                {
                    var name = GetString(column, "name") ?? string.Empty;
                    names.Add(name);
                }

                foreach (var row in tableRows.EnumerateArray())
                {
                    if (row.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                    var i = 0;
                    foreach (var value in row.EnumerateArray())
                    {
                        if (i < names.Count)
                        {
                            map[names[i]] = ToStringValue(value);
                        }

                        i++;
                    }

                    rows.Add(MapDictionaryRow(map));
                }
            }

            return rows
                .OrderByDescending(r => r.TimestampUtc)
                .ToList();
        }

        private static EmailEventRow MapObjectRow(JsonElement item)
        {
            var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in item.EnumerateObject())
            {
                map[property.Name] = ToStringValue(property.Value);
            }

            return MapDictionaryRow(map);
        }

        private static EmailEventRow MapDictionaryRow(Dictionary<string, string?> map)
        {
            var timestampText = FirstNonEmpty(
                GetValue(map, "timestamp"),
                GetValue(map, "timeGenerated"),
                GetValue(map, "eventTimestamp"),
                GetValue(map, "eventTime"));

            DateTime.TryParse(timestampText, out var timestamp);

            var eventType = FirstNonEmpty(
                GetValue(map, "status"),
                GetValue(map, "deliveryStatus"),
                GetValue(map, "eventType"),
                GetValue(map, "operationName"));

            var recipient = FirstNonEmpty(
                GetValue(map, "recipient"),
                GetValue(map, "recipientAddress"),
                GetValue(map, "emailAddress"),
                GetValue(map, "to"));

            var messageId = FirstNonEmpty(
                GetValue(map, "messageId"),
                GetValue(map, "operationId"),
                GetValue(map, "correlationId"),
                GetValue(map, "id"));

            var subject = FirstNonEmpty(
                GetValue(map, "subject"),
                GetValue(map, "emailSubject"));

            var details = FirstNonEmpty(
                GetValue(map, "reason"),
                GetValue(map, "statusDetails"),
                GetValue(map, "diagnosticCode"),
                GetValue(map, "errorMessage"));

            return new EmailEventRow
            {
                TimestampUtc = timestamp == default ? (DateTime?)null : DateTime.SpecifyKind(timestamp, DateTimeKind.Utc),
                EventType = eventType,
                Recipient = recipient,
                Subject = subject,
                MessageId = messageId,
                Details = details,
                Raw = BuildRawSummary(map)
            };
        }

        private static string BuildRawSummary(Dictionary<string, string?> map)
        {
            var builder = new StringBuilder();
            foreach (var pair in map)
            {
                if (string.IsNullOrWhiteSpace(pair.Value))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(" | ");
                }

                builder.Append(pair.Key);
                builder.Append(':');
                builder.Append(' ');
                builder.Append(pair.Value);
            }

            return builder.ToString();
        }

        private static string? GetValue(Dictionary<string, string?> map, string key)
        {
            return map.TryGetValue(key, out var value) ? value : null;
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
        }

        private static string? GetString(JsonElement element, string propertyName)
        {
            return TryGetPropertyIgnoreCase(element, propertyName, out var value)
                ? ToStringValue(value)
                : null;
        }

        private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        private static string? ToStringValue(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Null => null,
                JsonValueKind.Undefined => null,
                _ => value.GetRawText()
            };
        }
    }

    public class EmailEventsQueryResult
    {
        public List<EmailEventRow> Rows { get; set; } = new();

        public string? Error { get; set; }

        public string? RawJson { get; set; }
    }

    internal sealed class AzCommandExecutionResult
    {
        public int ExitCode { get; set; }

        public string Stdout { get; set; } = string.Empty;

        public string Stderr { get; set; } = string.Empty;

        public EmailEventsQueryResult? StartErrorResult { get; set; }
    }

    public class EmailEventRow
    {
        public DateTime? TimestampUtc { get; set; }

        public string? EventType { get; set; }

        public string? Recipient { get; set; }

        public string? Subject { get; set; }

        public string? MessageId { get; set; }

        public string? Details { get; set; }

        public string? Raw { get; set; }
    }
}