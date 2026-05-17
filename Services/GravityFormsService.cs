using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;

namespace TINWeb.Services
{
    public class GravityFormsApiException : Exception
    {
        public System.Net.HttpStatusCode StatusCode { get; }
        public string? ApiCode { get; }

        public GravityFormsApiException(System.Net.HttpStatusCode statusCode, string message, string? apiCode = null)
            : base(message)
        {
            StatusCode = statusCode;
            ApiCode = apiCode;
        }
    }

    public class GravityFormsSettings
    {
        public string BaseUrl { get; set; } = "";
        public string Username { get; set; } = "";
        public string ApplicationPassword { get; set; } = "";
    }

    public class GravityForm
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string DateCreated { get; set; } = "";
        public int EntryCount { get; set; }
        public bool HasEntryCount { get; set; }
        public bool? IsActive { get; set; }
        public string ActiveStatusRaw { get; set; } = "";
        public decimal AmountTotal { get; set; }
        public bool HasAmountTotal { get; set; }
    }

    public class GravityFormDetail
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string DateCreated { get; set; } = "";
        public bool? IsActive { get; set; }
        public string ActiveStatusRaw { get; set; } = "";
        public List<GravityFormField> Fields { get; set; } = new();
    }

    public class GravityFormField
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        public string Type { get; set; } = "";
    }

    public class GravityFormEntry
    {
        public string Id { get; set; } = "";
        public string DateCreated { get; set; } = "";
        public string Status { get; set; } = "";
        public string Ip { get; set; } = "";
        public Dictionary<string, string> Fields { get; set; } = new();
    }

    public class GravityFormEntriesResult
    {
        public int TotalCount { get; set; }
        public List<GravityFormEntry> Entries { get; set; } = new();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class GravityFormsSummary
    {
        public int All { get; set; }
        public int Active { get; set; }
        public int Inactive { get; set; }
    }

    public class GravityFormsDiscoveryDiagnostics
    {
        public string Mode { get; set; } = "none";
        public int ProbedCount { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public int CachedDiscoveredCount { get; set; }
    }

    public class GravityFormsService
    {
        private readonly HttpClient _http;
        private static readonly ConcurrentDictionary<string, (DateTime ExpiresUtc, List<GravityForm> Forms)> InactiveDiscoveryCache = new();
        public GravityFormsSummary LastSummary { get; private set; } = new();
        public GravityFormsDiscoveryDiagnostics LastDiscoveryDiagnostics { get; private set; } = new();

        public GravityFormsService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<GravityForm>> GetFormsAsync(string statusFilter = "active")
        {
            EnsureBaseAddressConfigured();
            LastDiscoveryDiagnostics = new GravityFormsDiscoveryDiagnostics();
            var normalizedStatusFilter = NormalizeStatusFilter(statusFilter);
            var forms = await GetFormsFromEndpointAsync("wp-json/gf/v2/forms");

            if (normalizedStatusFilter != "active")
            {
                var inactiveForms = await TryGetFormsFromAnyEndpointAsync(new[]
                {
                    "wp-json/gf/v2/forms?status=inactive",
                    "wp-json/gf/v2/forms?is_active=0",
                    "wp-json/gf/v2/forms?active=0"
                });

                if (inactiveForms.Count > 0)
                {
                    foreach (var form in inactiveForms)
                    {
                        if (!forms.Any(f => f.Id == form.Id))
                        {
                            forms.Add(form);
                        }
                    }
                }
            }

            if (normalizedStatusFilter != "active" && !forms.Any(f => f.IsActive == false))
            {
                var discovered = await DiscoverFormsByIdProbeAsync(forms);
                if (discovered.Count > 0)
                {
                    forms.AddRange(discovered);
                }
            }

            await EnrichFormsByIncludeAsync(forms);

            forms = forms
                .GroupBy(f => f.Id)
                .Select(g => g.OrderByDescending(x => x.IsActive.HasValue).ThenByDescending(x => x.HasEntryCount).First())
                .ToList();

            foreach (var form in forms)
            {
                // Treat unknown as active for list/totals unless explicitly inactive.
                if (!form.IsActive.HasValue)
                {
                    form.IsActive = true;
                }
            }

            LastSummary = new GravityFormsSummary
            {
                All = forms.Count,
                Active = forms.Count(f => f.IsActive != false),
                Inactive = forms.Count(f => f.IsActive == false)
            };

            forms = normalizedStatusFilter switch
            {
                "active" => forms.Where(f => f.IsActive != false).ToList(),
                "inactive" => forms.Where(f => f.IsActive == false).ToList(),
                _ => forms
            };

            if (normalizedStatusFilter == "all")
            {
                // For ALL view, prioritize active forms, then date desc.
                return forms
                    .OrderByDescending(f => f.IsActive != false)
                    .ThenByDescending(f => ParseSortableDate(f.DateCreated))
                    .ThenByDescending(f => f.Id)
                    .ToList();
            }

            return forms
                .OrderByDescending(f => ParseSortableDate(f.DateCreated))
                .ThenByDescending(f => f.Id)
                .ToList();
        }

        private async Task EnrichFormsByIncludeAsync(List<GravityForm> forms)
        {
            var targets = forms
                .Where(f => f.IsActive == null || string.IsNullOrWhiteSpace(f.DateCreated))
                .Select(f => f.Id)
                .Distinct()
                .ToList();

            if (targets.Count == 0)
            {
                return;
            }

            const int chunkSize = 40;
            var lookup = forms
                .GroupBy(f => f.Id)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderByDescending(x => x.IsActive.HasValue)
                        .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.DateCreated))
                        .ThenByDescending(x => x.HasEntryCount)
                        .First());

            for (var i = 0; i < targets.Count; i += chunkSize)
            {
                var chunk = targets.Skip(i).Take(chunkSize).ToList();
                var includeQuery = string.Join("&", chunk.Select(id => $"include[]={id}"));
                var endpoint = $"wp-json/gf/v2/forms?{includeQuery}";

                List<GravityForm> enriched;
                try
                {
                    enriched = await GetFormsFromEndpointAsync(endpoint);
                }
                catch
                {
                    continue;
                }

                foreach (var fromApi in enriched)
                {
                    if (!lookup.TryGetValue(fromApi.Id, out var existing))
                    {
                        continue;
                    }

                    if (existing.IsActive == null && fromApi.IsActive.HasValue)
                    {
                        existing.IsActive = fromApi.IsActive.Value;
                    }

                    if (string.IsNullOrWhiteSpace(existing.DateCreated)
                        && !string.IsNullOrWhiteSpace(fromApi.DateCreated))
                    {
                        existing.DateCreated = fromApi.DateCreated;
                    }

                    if (string.IsNullOrWhiteSpace(existing.ActiveStatusRaw)
                        && !string.IsNullOrWhiteSpace(fromApi.ActiveStatusRaw))
                    {
                        existing.ActiveStatusRaw = fromApi.ActiveStatusRaw;
                    }

                    if (!existing.HasEntryCount && fromApi.HasEntryCount)
                    {
                        existing.EntryCount = fromApi.EntryCount;
                        existing.HasEntryCount = true;
                    }
                }
            }
        }

        private async Task<List<GravityForm>> DiscoverFormsByIdProbeAsync(List<GravityForm> existingForms)
        {
            var cacheKey = _http.BaseAddress?.ToString() ?? "default";
            if (InactiveDiscoveryCache.TryGetValue(cacheKey, out var cached)
                && cached.ExpiresUtc > DateTime.UtcNow)
            {
                LastDiscoveryDiagnostics = new GravityFormsDiscoveryDiagnostics
                {
                    Mode = "cache",
                    ProbedCount = 0,
                    CompletedAtUtc = DateTime.UtcNow,
                    CachedDiscoveredCount = cached.Forms.Count
                };
                var existing = existingForms.Select(f => f.Id).ToHashSet();
                return cached.Forms
                    .Where(f => f.IsActive == false && !existing.Contains(f.Id))
                    .ToList();
            }

            var existingIds = existingForms.Select(f => f.Id).ToHashSet();
            var maxKnownId = existingForms.Count > 0 ? existingForms.Max(f => f.Id) : 0;

            // Probe a bounded range of form IDs to discover forms not included in /forms listing.
            var probeUpperBound = Math.Min(Math.Max(120, maxKnownId + 40), 220);
            var discovered = new List<GravityForm>();
            var candidateIds = Enumerable
                .Range(1, probeUpperBound)
                .Where(id => !existingIds.Contains(id))
                .ToList();

            var maxConcurrency = 8;
            using var semaphore = new SemaphoreSlim(maxConcurrency);
            var tasks = candidateIds.Select(async id =>
            {
                await semaphore.WaitAsync();
                try
                {
                    return await TryGetFormDetailAsync(id);
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToList();

            var details = await Task.WhenAll(tasks);
            foreach (var detail in details)
            {
                if (detail == null || detail.Id <= 0 || string.IsNullOrWhiteSpace(detail.Title))
                {
                    continue;
                }

                if (detail.IsActive == false)
                {
                    discovered.Add(new GravityForm
                    {
                        Id = detail.Id,
                        Title = detail.Title,
                        DateCreated = detail.DateCreated,
                        IsActive = detail.IsActive,
                        ActiveStatusRaw = detail.ActiveStatusRaw,
                        HasEntryCount = false,
                        EntryCount = 0
                    });
                }
            }

            InactiveDiscoveryCache[cacheKey] = (DateTime.UtcNow.AddMinutes(15), discovered);

            LastDiscoveryDiagnostics = new GravityFormsDiscoveryDiagnostics
            {
                Mode = "fresh",
                ProbedCount = candidateIds.Count,
                CompletedAtUtc = DateTime.UtcNow,
                CachedDiscoveredCount = discovered.Count
            };

            return discovered;
        }

        private static string NormalizeStatusFilter(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "active";
            }

            return value.Trim().ToLowerInvariant() switch
            {
                "all" => "all",
                "inactive" => "inactive",
                _ => "active"
            };
        }

        private async Task<List<GravityForm>> GetFormsFromEndpointAsync(string endpoint)
        {
            var response = await _http.GetAsync(endpoint);
            await EnsureSuccessWithDetailsAsync(response);
            var json = await response.Content.ReadAsStringAsync();
            EnsureJsonPayload(endpoint, response, json);
            return ParseForms(json);
        }

        private async Task<List<GravityForm>> TryGetFormsFromAnyEndpointAsync(IEnumerable<string> endpoints)
        {
            foreach (var endpoint in endpoints)
            {
                try
                {
                    var forms = await GetFormsFromEndpointAsync(endpoint);
                    if (forms.Count > 0)
                    {
                        return forms;
                    }
                }
                catch
                {
                    // Try the next endpoint variant.
                }
            }

            return new List<GravityForm>();
        }

        private static List<GravityForm> ParseForms(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var forms = new List<GravityForm>();

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in doc.RootElement.EnumerateArray())
                    forms.Add(ParseForm(el));
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                // GF may return object keyed by form ID.
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Object)
                        forms.Add(ParseForm(prop.Value));
                }
            }

            return forms;
        }

        private void EnsureBaseAddressConfigured()
        {
            if (_http.BaseAddress == null)
            {
                throw new InvalidOperationException(
                    "WordPress BaseUrl is not configured. Set WordPress:BaseUrl or WP__RESTAPI__Url.");
            }
        }

            public async Task<decimal?> GetFormAmountTotalAsync(int formId)
            {
                EnsureBaseAddressConfigured();
                return await TryGetAmountTotalAsync(formId);
            }

        public async Task<GravityFormDetail> GetFormDetailAsync(int formId)
        {
            EnsureBaseAddressConfigured();
            var response = await _http.GetAsync($"wp-json/gf/v2/forms/{formId}");
            await EnsureSuccessWithDetailsAsync(response);
            var json = await response.Content.ReadAsStringAsync();
            EnsureJsonPayload($"wp-json/gf/v2/forms/{formId}", response, json);
            using var doc = JsonDocument.Parse(json);
            return ParseFormDetail(doc.RootElement);
        }

        public async Task<GravityFormEntriesResult> GetEntriesAsync(int formId, int page = 1, int pageSize = 20)
        {
            EnsureBaseAddressConfigured();
            var endpoint =
                $"wp-json/gf/v2/entries?form_ids={formId}&paging[page_size]={pageSize}&paging[current_page]={page}";
            var response = await _http.GetAsync(endpoint);
            await EnsureSuccessWithDetailsAsync(response);
            var json = await response.Content.ReadAsStringAsync();
            EnsureJsonPayload(endpoint, response, json);
            using var doc = JsonDocument.Parse(json);

            var totalCount = GetIntProperty(doc.RootElement, "total_count");
            var entries = new List<GravityFormEntry>();

            if (doc.RootElement.TryGetProperty("entries", out var entriesProp)
                && entriesProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in entriesProp.EnumerateArray())
                    entries.Add(ParseEntry(el));
            }

            return new GravityFormEntriesResult
            {
                TotalCount = totalCount,
                Entries = entries,
                PageNumber = page,
                PageSize = pageSize,
                TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize))
            };
        }

        private static void EnsureJsonPayload(string endpoint, HttpResponseMessage response, string body)
        {
            try
            {
                using var _ = JsonDocument.Parse(body);
            }
            catch (JsonException ex)
            {
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "unknown";
                var snippet = BuildResponseSnippet(body);
                throw new InvalidOperationException(
                    $"WordPress API returned non-JSON data for '{endpoint}' (Content-Type: {contentType}). " +
                    $"Verify WordPress API URL/credentials and any security layer returning HTML. Response starts with: {snippet}",
                    ex);
            }
        }

        private static string BuildResponseSnippet(string? body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return "<empty>";
            }

            var snippet = body.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return snippet.Length <= 180
                ? snippet
                : snippet.Substring(0, 180) + "...";
        }

        private static GravityForm ParseForm(JsonElement el)
        {
            var hasEntryCount = TryGetIntProperty(
                el,
                out var entryCount,
                "entries",
                "entry_count",
                "entries_count",
                "submission_count",
                "submissions_count");

            var hasIsActive = TryGetBoolProperty(
                el,
                out var isActive,
                "status",
                "is_active",
                "isActive",
                "active");

            return new GravityForm
            {
                Id = GetIntProperty(el, "id"),
                Title = GetStringProperty(el, "title"),
                DateCreated = GetFirstStringProperty(el, "date_created", "date_created_gmt", "dateCreated"),
                EntryCount = hasEntryCount ? entryCount : 0,
                HasEntryCount = hasEntryCount,
                IsActive = hasIsActive ? isActive : null,
                ActiveStatusRaw = GetFirstStringProperty(el, "status", "is_active", "isActive", "active")
            };
        }

        private async Task PopulateMissingEntryCountsAsync(List<GravityForm> forms)
        {
            foreach (var form in forms.Where(f => !f.HasEntryCount))
            {
                var count = await TryGetEntryCountAsync(form.Id);
                if (count.HasValue)
                {
                    form.EntryCount = count.Value;
                    form.HasEntryCount = true;
                }
            }
        }

        private async Task<int?> TryGetEntryCountAsync(int formId)
        {
            try
            {
                var response = await _http.GetAsync(
                    $"wp-json/gf/v2/entries?form_ids={formId}&paging[page_size]=1&paging[current_page]=1");

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                return GetIntProperty(doc.RootElement, "total_count");
            }
            catch
            {
                return null;
            }
        }

        private async Task PopulateFormStatusesAsync(List<GravityForm> forms)
        {
            foreach (var form in forms)
            {
                var detail = await TryGetFormDetailAsync(form.Id);
                if (detail?.IsActive.HasValue == true)
                {
                    form.IsActive = detail.IsActive.Value;
                }

                if (string.IsNullOrWhiteSpace(form.DateCreated)
                    && !string.IsNullOrWhiteSpace(detail?.DateCreated))
                {
                    form.DateCreated = detail.DateCreated;
                }

                if (!string.IsNullOrWhiteSpace(detail?.ActiveStatusRaw))
                {
                    form.ActiveStatusRaw = detail.ActiveStatusRaw;
                }
            }
        }

        private async Task<GravityFormDetail?> TryGetFormDetailAsync(int formId)
        {
            try
            {
                return await GetFormDetailAsync(formId);
            }
            catch
            {
                return null;
            }
        }

        private async Task<decimal?> TryGetAmountTotalAsync(int formId, GravityFormDetail? detail = null)
        {
            try
            {
                detail ??= await GetFormDetailAsync(formId);
                var amountFieldIds = detail.Fields
                    .Where(IsAmountLikeField)
                    .Select(f => f.Id)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                decimal total = 0m;
                var foundAnyAmount = false;
                var page = 1;
                const int pageSize = 200;

                while (true)
                {
                    var response = await _http.GetAsync(
                        $"wp-json/gf/v2/entries?form_ids={formId}&paging[page_size]={pageSize}&paging[current_page]={page}");

                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);

                    if (!doc.RootElement.TryGetProperty("entries", out var entriesProp)
                        || entriesProp.ValueKind != JsonValueKind.Array)
                    {
                        break;
                    }

                    var entries = entriesProp.EnumerateArray().ToList();
                    if (entries.Count == 0)
                    {
                        break;
                    }

                    foreach (var entry in entries)
                    {
                        if (TryGetEntryAmount(entry, amountFieldIds, out var amount))
                        {
                            total += amount;
                            foundAnyAmount = true;
                        }
                    }

                    if (entries.Count < pageSize)
                    {
                        break;
                    }

                    page++;
                }

                return foundAnyAmount ? total : null;
            }
            catch
            {
                return null;
            }
        }

        private static GravityFormDetail ParseFormDetail(JsonElement el)
        {
            var fields = new List<GravityFormField>();
            if (el.TryGetProperty("fields", out var fieldsProp) && fieldsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var f in fieldsProp.EnumerateArray())
                {
                    var fid = f.TryGetProperty("id", out var fip) ? fip.GetRawText().Trim('"') : "";
                    var flabel = GetStringProperty(f, "label");
                    var ftype = GetStringProperty(f, "type");
                    if (!string.IsNullOrWhiteSpace(flabel))
                        fields.Add(new GravityFormField { Id = fid, Label = flabel, Type = ftype });
                }
            }

            return new GravityFormDetail
            {
                Id = GetIntProperty(el, "id"),
                Title = GetStringProperty(el, "title"),
                DateCreated = GetFirstStringProperty(el, "date_created", "date_created_gmt", "dateCreated", "created_at"),
                IsActive = TryGetBoolProperty(
                    el,
                    out var isActive,
                    "status",
                    "is_active",
                    "isActive",
                    "active")
                    ? isActive
                    : null,
                ActiveStatusRaw = GetFirstStringProperty(el, "status", "is_active", "isActive", "active"),
                Fields = fields
            };
        }

        private static GravityFormEntry ParseEntry(JsonElement el)
        {
            var entry = new GravityFormEntry();
            foreach (var prop in el.EnumerateObject())
            {
                switch (prop.Name)
                {
                    case "id":
                        entry.Id = JsonValueToDisplayString(prop.Value);
                        break;
                    case "date_created":
                        entry.DateCreated = JsonValueToDisplayString(prop.Value);
                        break;
                    case "status":
                        entry.Status = JsonValueToDisplayString(prop.Value);
                        break;
                    case "ip":
                        entry.Ip = JsonValueToDisplayString(prop.Value);
                        break;
                    default:
                        // Field values use numeric keys (e.g. "1", "2", "1.3")
                        if (prop.Name.Length > 0 && (char.IsDigit(prop.Name[0])))
                        {
                            var val = JsonValueToDisplayString(prop.Value);
                            if (!string.IsNullOrEmpty(val))
                                entry.Fields[prop.Name] = val;
                        }
                        break;
                }
            }
            return entry;
        }

        private static string JsonValueToDisplayString(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.Null => string.Empty,
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Array => string.Join(", ", value.EnumerateArray().Select(JsonValueToDisplayString).Where(v => !string.IsNullOrWhiteSpace(v))),
                JsonValueKind.Object => value.GetRawText(),
                _ => value.GetRawText()
            };
        }

        private static DateTime ParseSortableDate(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return DateTime.MinValue;
            }

            if (DateTime.TryParseExact(
                raw,
                "yyyy-MM-dd HH:mm:ss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeLocal,
                out var gfDate))
            {
                return gfDate;
            }

            return DateTime.TryParse(raw, out var parsed)
                ? parsed
                : DateTime.MinValue;
        }

        private static bool IsAmountLikeField(GravityFormField field)
        {
            var label = field.Label ?? string.Empty;
            var type = field.Type ?? string.Empty;

            return label.Contains("amount", StringComparison.OrdinalIgnoreCase)
                || label.Contains("$", StringComparison.Ordinal)
                || string.Equals(type, "total", StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, "price", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetEntryAmount(JsonElement entry, HashSet<string> amountFieldIds, out decimal amount)
        {
            foreach (var prop in entry.EnumerateObject())
            {
                var isPaymentAmount = string.Equals(prop.Name, "payment_amount", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(prop.Name, "payment_total", StringComparison.OrdinalIgnoreCase);

                if (!isPaymentAmount && !amountFieldIds.Contains(prop.Name))
                {
                    continue;
                }

                if (TryParseDecimal(prop.Value, out amount))
                {
                    return true;
                }
            }

            amount = 0m;
            return false;
        }

        private static bool TryParseDecimal(JsonElement value, out decimal amount)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Number:
                    if (value.TryGetDecimal(out amount))
                    {
                        return true;
                    }
                    break;
                case JsonValueKind.String:
                    return TryParseDecimalString(value.GetString(), out amount);
            }

            amount = 0m;
            return false;
        }

        private static bool TryParseDecimalString(string? raw, out decimal amount)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                amount = 0m;
                return false;
            }

            if (decimal.TryParse(
                raw,
                System.Globalization.NumberStyles.Number | System.Globalization.NumberStyles.AllowCurrencySymbol,
                System.Globalization.CultureInfo.InvariantCulture,
                out amount))
            {
                return true;
            }

            if (decimal.TryParse(
                raw,
                System.Globalization.NumberStyles.Number | System.Globalization.NumberStyles.AllowCurrencySymbol,
                new System.Globalization.CultureInfo("en-US"),
                out amount))
            {
                return true;
            }

            var cleaned = new string(raw.Where(c => char.IsDigit(c) || c == '.' || c == '-' || c == ',').ToArray());
            if (decimal.TryParse(
                cleaned,
                System.Globalization.NumberStyles.Number | System.Globalization.NumberStyles.AllowLeadingSign,
                System.Globalization.CultureInfo.InvariantCulture,
                out amount))
            {
                return true;
            }

            amount = 0m;
            return false;
        }

        private static int GetIntProperty(JsonElement el, string name)
        {
            if (!el.TryGetProperty(name, out var prop)) return 0;
            return prop.ValueKind switch
            {
                JsonValueKind.Number => prop.GetInt32(),
                JsonValueKind.String => int.TryParse(prop.GetString(), out var v) ? v : 0,
                _ => 0
            };
        }

        private static string GetStringProperty(JsonElement el, string name)
        {
            if (!el.TryGetProperty(name, out var prop)) return "";
            return JsonValueToDisplayString(prop);
        }

        private static string GetFirstStringProperty(JsonElement el, params string[] names)
        {
            foreach (var name in names)
            {
                if (!el.TryGetProperty(name, out var prop))
                {
                    continue;
                }

                var value = JsonValueToDisplayString(prop);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static bool TryGetIntProperty(JsonElement el, out int value, params string[] names)
        {
            foreach (var name in names)
            {
                if (!el.TryGetProperty(name, out var prop))
                {
                    continue;
                }

                switch (prop.ValueKind)
                {
                    case JsonValueKind.Number:
                        if (prop.TryGetInt32(out value))
                        {
                            return true;
                        }
                        break;
                    case JsonValueKind.String:
                        if (int.TryParse(prop.GetString(), out value))
                        {
                            return true;
                        }
                        break;
                }
            }

            value = 0;
            return false;
        }

        private static bool TryGetBoolProperty(JsonElement el, out bool value, params string[] names)
        {
            foreach (var name in names)
            {
                if (!el.TryGetProperty(name, out var prop))
                {
                    continue;
                }

                switch (prop.ValueKind)
                {
                    case JsonValueKind.True:
                        value = true;
                        return true;
                    case JsonValueKind.False:
                        value = false;
                        return true;
                    case JsonValueKind.Number:
                        if (prop.TryGetInt32(out var number))
                        {
                            value = number != 0;
                            return true;
                        }
                        break;
                    case JsonValueKind.String:
                        var raw = prop.GetString();
                        if (string.Equals(raw, "active", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(raw, "enabled", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase))
                        {
                            value = true;
                            return true;
                        }

                        if (string.Equals(raw, "inactive", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(raw, "disabled", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(raw, "off", StringComparison.OrdinalIgnoreCase))
                        {
                            value = false;
                            return true;
                        }

                        if (bool.TryParse(raw, out value))
                        {
                            return true;
                        }

                        if (int.TryParse(raw, out var numeric))
                        {
                            value = numeric != 0;
                            return true;
                        }

                        if (string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(raw, "y", StringComparison.OrdinalIgnoreCase))
                        {
                            value = true;
                            return true;
                        }

                        if (string.Equals(raw, "no", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(raw, "n", StringComparison.OrdinalIgnoreCase))
                        {
                            value = false;
                            return true;
                        }
                        break;
                }
            }

            value = false;
            return false;
        }

        private static async Task EnsureSuccessWithDetailsAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var body = await response.Content.ReadAsStringAsync();
            var apiMessage = body;
            string? apiCode = null;

            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (doc.RootElement.TryGetProperty("code", out var codeProp))
                    {
                        apiCode = codeProp.ValueKind == JsonValueKind.String ? codeProp.GetString() : codeProp.GetRawText();
                    }

                    if (doc.RootElement.TryGetProperty("message", out var messageProp))
                    {
                        apiMessage = messageProp.ValueKind == JsonValueKind.String
                            ? messageProp.GetString() ?? apiMessage
                            : messageProp.GetRawText();
                    }
                }
            }
            catch
            {
                // keep raw response body if not JSON
            }

            throw new GravityFormsApiException(
                response.StatusCode,
                $"WordPress API {(int)response.StatusCode} ({response.ReasonPhrase}): {apiMessage}",
                apiCode);
        }
    }
}
