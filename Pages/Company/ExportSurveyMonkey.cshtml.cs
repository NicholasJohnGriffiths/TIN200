using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Services;

namespace TINWeb.Pages.Company
{
    public class ExportSurveyMonkeyModel : PageModel
    {
        private readonly CompanyService _service;

        public ExportSurveyMonkeyModel(CompanyService service)
        {
            _service = service;
        }

        public async Task<IActionResult> OnGetAsync(int? lastTin200Year)
        {
            var lastTin200Years = await _service.GetAvailableLastTin200YearsAsync();
            if (!lastTin200Year.HasValue)
            {
                lastTin200Year = lastTin200Years.FirstOrDefault();
            }

            var records = await _service.GetAllCompaniesAsync(lastTin200Year);

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(',', new[]
            {
                "Email",
                "FirstName",
                "LastName",
                "CompanyName",
                "ExternalId",
                "CompanyDescription",
                "LastTIN200Year",
                "FYE2025",
                "FYE2024",
                "FYE2023"
            }));

            foreach (var record in records)
            {
                var cols = new[]
                {
                    EscapeCsv(record.Email),
                    EscapeCsv(record.CeoFirstName),
                    EscapeCsv(record.CeoLastName),
                    EscapeCsv(record.CompanyName),
                    EscapeCsv(record.ExternalId),
                    EscapeCsv(record.CompanyDescription),
                    EscapeCsv(record.LastTIN200Year.HasValue ? record.LastTIN200Year.Value.ToString() : string.Empty),
                    EscapeCsv(record.Fye2025.HasValue ? record.Fye2025.Value.ToString("F0") : string.Empty),
                    EscapeCsv(record.Fye2024.HasValue ? record.Fye2024.Value.ToString("F0") : string.Empty),
                    EscapeCsv(record.Fye2023.HasValue ? record.Fye2023.Value.ToString("F0") : string.Empty)
                };

                sb.AppendLine(string.Join(',', cols));
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = lastTin200Year.HasValue
                ? $"company-surveymonkey-export-{lastTin200Year.Value}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv"
                : $"company-surveymonkey-export-all-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";

            return File(bytes, "text/csv; charset=utf-8", fileName);
        }

        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var escaped = value.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }
    }
}
