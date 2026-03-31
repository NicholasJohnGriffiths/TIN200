using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Services;

namespace TINWeb.Pages.Testing.TestCompanies
{
    public class ExportMailerliteModel : PageModel
    {
        private readonly CompanyService _service;
        private readonly ApplicationDbContext _context;

        public ExportMailerliteModel(CompanyService service, ApplicationDbContext context)
        {
            _service = service;
            _context = context;
        }

        public async Task<IActionResult> OnGetAsync(string? search)
        {
            var records = await _service.GetTestCompaniesAsync(search);

            // Get current survey ID
            var currentSurveyId = await _context.Survey
                .Where(s => s.CurrentSurvey)
                .OrderByDescending(s => s.FinancialYear)
                .ThenByDescending(s => s.Id)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync();

            // Get survey links for all companies from CompanySurvey table
            var surveyLinks = new Dictionary<int, string>();
            if (currentSurveyId.HasValue)
            {
                surveyLinks = await _context.CompanySurvey
                    .Where(cs => cs.SurveyId == currentSurveyId.Value)
                    .ToDictionaryAsync(cs => cs.CompanyId, cs => cs.SurveyLink ?? string.Empty);
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
                
                // Use survey link from CompanySurvey table if available
                var surveyLink = string.Empty;
                if (surveyLinks.TryGetValue(r.Id, out var storedLink) && !string.IsNullOrWhiteSpace(storedLink))
                {
                    surveyLink = CsvField(storedLink);
                }

                sb.AppendLine($"{email},{externalId},{firstName},{lastName},{company},{description},yes,{surveyLink}");
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv; charset=utf-8", "Mailerlite_test_export.csv");
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
