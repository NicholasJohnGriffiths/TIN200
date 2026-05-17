using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using TINWeb.Services;

namespace TINWeb.Pages.Transactions
{
    public class GravityFormsModel : PageModel
    {
        private readonly GravityFormsService _service;

        public GravityFormsModel(GravityFormsService service)
        {
            _service = service;
        }

        public List<GravityForm> Forms { get; private set; } = new();
        public string? ErrorMessage { get; private set; }
        public int AllFormsCount { get; private set; }
        public int ActiveFormsCount { get; private set; }
        public int InactiveFormsCount { get; private set; }
        public int FilteredFormsCount { get; private set; }
        public int TotalPages { get; private set; }
        public int EffectivePageNumber { get; private set; } = 1;
        public int EffectivePageSize { get; private set; } = 25;
        public string DiscoveryMode { get; private set; } = "none";
        public int DiscoveryProbedCount { get; private set; }
        public int DiscoveryCachedDiscoveredCount { get; private set; }
        public DateTime? DiscoveryCompletedAtUtc { get; private set; }
        [BindProperty(SupportsGet = true)]
        public string StatusFilter { get; set; } = "active";
        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;
        [BindProperty(SupportsGet = true)]
        public int? PageSize { get; set; }

        public async Task OnGetAsync()
        {
            try
            {
                var allForms = await _service.GetFormsAsync(StatusFilter);
                EffectivePageSize = NormalizePageSize(PageSize, StatusFilter);
                FilteredFormsCount = allForms.Count;
                TotalPages = Math.Max(1, (int)Math.Ceiling(FilteredFormsCount / (double)EffectivePageSize));
                EffectivePageNumber = Math.Clamp(PageNumber, 1, TotalPages);
                Forms = allForms
                    .Skip((EffectivePageNumber - 1) * EffectivePageSize)
                    .Take(EffectivePageSize)
                    .ToList();

                AllFormsCount = _service.LastSummary.All;
                ActiveFormsCount = _service.LastSummary.Active;
                InactiveFormsCount = _service.LastSummary.Inactive;
                DiscoveryMode = _service.LastDiscoveryDiagnostics.Mode;
                DiscoveryProbedCount = _service.LastDiscoveryDiagnostics.ProbedCount;
                DiscoveryCachedDiscoveredCount = _service.LastDiscoveryDiagnostics.CachedDiscoveredCount;
                DiscoveryCompletedAtUtc = _service.LastDiscoveryDiagnostics.CompletedAtUtc;
            }
            catch (GravityFormsApiException ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized || ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    ErrorMessage = $"Unable to connect to WordPress: {ex.Message} Check credentials and ensure the WordPress user has Gravity Forms API permissions (view forms/entries).";
                }
                else
                {
                    ErrorMessage = $"Unable to connect to WordPress: {ex.Message}";
                }
            }
            catch (HttpRequestException ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    ErrorMessage = "Unable to connect to WordPress: 401 Unauthorized. Check credentials in WordPress settings or env vars (WordPress__Username / WordPress__ApplicationPassword, or WP__RESTAPI__Username / WP__RESTAPI__Token).";
                }
                else
                {
                    ErrorMessage = $"Unable to connect to WordPress: {ex.Message}";
                }
            }
            catch (InvalidOperationException ex)
            {
                ErrorMessage = $"Unable to connect to WordPress: {ex.Message}";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"An error occurred: {ex.Message}";
            }
        }

        private static int NormalizePageSize(int? pageSize, string? statusFilter)
        {
            if (!pageSize.HasValue)
            {
                return string.Equals(statusFilter, "inactive", StringComparison.OrdinalIgnoreCase) ? 10 : 25;
            }

            return pageSize.Value switch
            {
                10 => 10,
                25 => 25,
                50 => 50,
                100 => 100,
                _ => string.Equals(statusFilter, "inactive", StringComparison.OrdinalIgnoreCase) ? 10 : 25
            };
        }
    }
}
