using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using TINWeb.Services;

namespace TINWeb.Pages.Company
{
    public class ExportMailerliteModel : PageModel
    {
        private readonly CompanyService _service;
        private readonly ISurveyLinkTokenService _tokenService;
        private readonly SurveyLinkSettings _surveyLinkSettings;

        public ExportMailerliteModel(CompanyService service, ISurveyLinkTokenService tokenService, IOptions<SurveyLinkSettings> surveyLinkOptions)
        {
            _service = service;
            _tokenService = tokenService;
            _surveyLinkSettings = surveyLinkOptions.Value;
        }

        public async Task<IActionResult> OnGetAsync(int? lastTin200Year, string? companySearch, bool showTestCompanies = false)
        {
            var records = await _service.GetAllCompaniesAsync(lastTin200Year);

            if (!showTestCompanies)
            {
                records = records.Where(x => !x.Test).ToList();
            }

            if (!string.IsNullOrWhiteSpace(companySearch))
            {
                var search = companySearch.Trim();
                records = records
                    .Where(x =>
                        (!string.IsNullOrWhiteSpace(x.CompanyName) && x.CompanyName.Contains(search, StringComparison.OrdinalIgnoreCase))
                        || (!string.IsNullOrWhiteSpace(x.ExternalId) && x.ExternalId.Contains(search, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            var sb = new StringBuilder();
            sb.AppendLine("Email Address,Unique Company Identifier,Name,Last Name,Company,Company Description,TIN200Survey,Survey Link");

            foreach (var r in records)
            {
                var email = CsvField(r.Email);
                var externalId = CsvField(r.ExternalId);
                var firstName = CsvField(r.CeoFirstName);
                var lastName = CsvField(r.CeoLastName);
                var company = CsvField(r.CompanyName);
                var description = CsvQuoted(r.CompanyDescription);
                var surveyLink = CsvField(BuildSurveyUrl(r.Id));
                sb.AppendLine($"{email},{externalId},{firstName},{lastName},{company},{description},yes,{surveyLink}");
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = lastTin200Year.HasValue
                ? $"Mailerlite_export_{lastTin200Year.Value}.csv"
                : "Mailerlite_export_all.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }

        private string BuildSurveyUrl(int companyId)
        {
            var token = _tokenService.GenerateToken(companyId);
            var relativePath = Url.Page("/Company/AnswerSurvey", pageHandler: null, values: new { id = companyId, token }, protocol: null) ?? string.Empty;
            var configuredBaseUrl = (_surveyLinkSettings.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(configuredBaseUrl) && Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out _))
            {
                return $"{configuredBaseUrl}{relativePath}";
            }
            return Url.Page("/Company/AnswerSurvey", pageHandler: null, values: new { id = companyId, token }, protocol: Request.Scheme) ?? string.Empty;
        }

        private static string CsvField(string? value)
        {
            var s = (value ?? string.Empty).Trim();
            if (s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
            {
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            }
            return s;
        }

        // Always wraps in double quotes (as required for Company Description)
        private static string CsvQuoted(string? value)
        {
            var s = (value ?? string.Empty).Trim();
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }
}
