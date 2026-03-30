using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using TINWeb.Services;

namespace TINWeb.Pages.Testing.TestCompanies
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

        public async Task<IActionResult> OnGetAsync(string? search)
        {
            var records = await _service.GetTestCompaniesAsync(search);

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
            return File(bytes, "text/csv; charset=utf-8", "Mailerlite_test_export.csv");
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

        private static string CsvQuoted(string? value)
        {
            var s = (value ?? string.Empty).Trim();
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }
}
