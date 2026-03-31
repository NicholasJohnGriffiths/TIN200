using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Services;

namespace TINWeb.Pages.Company
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
            var fileName = lastTin200Year.HasValue
                ? $"Mailerlite_export_{lastTin200Year.Value}.csv"
                : "Mailerlite_export_all.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);
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
