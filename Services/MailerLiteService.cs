using System.Net.Http.Headers;
using System.Text.Json;

namespace TINWeb.Services
{
    public class MailerLiteSettings
    {
        public string ApiKey { get; set; } = "";
    }

    public class MailerLiteGroup
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int SubscribersCount { get; set; }
    }

    public class MailerLiteSubscriber
    {
        public string Email { get; set; } = "";
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? CompanyName { get; set; }
    }

    public class MailerLiteService
    {
        private readonly HttpClient _http;

        public MailerLiteService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<MailerLiteGroup>> GetGroupsAsync(CancellationToken cancellationToken = default)
        {
            var groups = new List<MailerLiteGroup>();
            string? nextUrl = "groups?limit=200";

            while (!string.IsNullOrWhiteSpace(nextUrl))
            {
                using var response = await _http.GetAsync(nextUrl, cancellationToken);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = document.RootElement;

                if (root.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in dataElement.EnumerateArray())
                    {
                        groups.Add(new MailerLiteGroup
                        {
                            Id = GetString(item, "id") ?? "",
                            Name = GetString(item, "name") ?? "",
                            SubscribersCount = GetInt(item, "active_count") ?? GetInt(item, "total") ?? 0
                        });
                    }
                }

                nextUrl = GetNextPageUrl(root);
            }

            return groups.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public async Task<List<MailerLiteSubscriber>> GetGroupSubscribersAsync(string groupId, CancellationToken cancellationToken = default)
        {
            var subscribers = new List<MailerLiteSubscriber>();
            string? nextUrl = $"groups/{Uri.EscapeDataString(groupId)}/subscribers?limit=200";

            while (!string.IsNullOrWhiteSpace(nextUrl))
            {
                using var response = await _http.GetAsync(nextUrl, cancellationToken);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = document.RootElement;

                if (root.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in dataElement.EnumerateArray())
                    {
                        string? firstName = null;
                        string? lastName = null;
                        string? companyName = null;

                        if (item.TryGetProperty("fields", out var fieldsElement) && fieldsElement.ValueKind == JsonValueKind.Object)
                        {
                            firstName = GetString(fieldsElement, "name");
                            lastName = GetString(fieldsElement, "last_name");
                            companyName = GetString(fieldsElement, "company");
                        }

                        subscribers.Add(new MailerLiteSubscriber
                        {
                            Email = GetString(item, "email") ?? "",
                            FirstName = firstName,
                            LastName = lastName,
                            CompanyName = companyName
                        });
                    }
                }

                nextUrl = GetNextPageUrl(root);
            }

            return subscribers.OrderBy(s => s.Email, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string? GetNextPageUrl(JsonElement root)
        {
            if (root.TryGetProperty("links", out var linksElement)
                && linksElement.ValueKind == JsonValueKind.Object
                && linksElement.TryGetProperty("next", out var nextElement)
                && nextElement.ValueKind == JsonValueKind.String)
            {
                var next = nextElement.GetString();
                if (!string.IsNullOrWhiteSpace(next))
                {
                    // MailerLite returns a full absolute URL; HttpClient with BaseAddress needs a relative path.
                    var apiIndex = next.IndexOf("/api/", StringComparison.OrdinalIgnoreCase);
                    return apiIndex >= 0 ? next[(apiIndex + "/api/".Length)..] : next;
                }
            }

            return null;
        }

        private static string? GetString(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var value))
            {
                if (value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }

                if (value.ValueKind == JsonValueKind.Number)
                {
                    return value.ToString();
                }
            }

            return null;
        }

        private static int? GetInt(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            {
                return number;
            }

            return null;
        }
    }
}
