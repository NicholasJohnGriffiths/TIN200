using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Services;

namespace TINWeb.Pages.Company
{
    public class ExportModel : PageModel
    {
        private readonly CompanyService _service;

        public ExportModel(CompanyService service)
        {
            _service = service;
        }

        public async Task<IActionResult> OnGetAsync(int? lastTin200Year)
        {
            // default to latest when not supplied
            var lastTin200Years = await _service.GetAvailableLastTin200YearsAsync();
            if (!lastTin200Year.HasValue)
            {
                lastTin200Year = lastTin200Years.FirstOrDefault();
            }

            var records = await _service.GetAllCompaniesAsync(lastTin200Year);

            var sb = new StringBuilder();
            // header
            sb.AppendLine(string.Join('\t', new[] { "Id", "CEO First Name", "CEO Last Name", "Email", "External ID", "Company Name", "Company Description", "FYE 2025", "FYE 2024", "FYE 2023", "LastTIN200Year" }));

            foreach (var r in records)
            {
                var cols = new string[] {
                    r.Id.ToString(),
                    (r.CeoFirstName ?? string.Empty).Replace('\t',' '),
                    (r.CeoLastName ?? string.Empty).Replace('\t',' '),
                    (r.Email ?? string.Empty).Replace('\t',' '),
                    (r.ExternalId ?? string.Empty).Replace('\t',' '),
                    (r.CompanyName ?? string.Empty).Replace('\t',' '),
                    (r.CompanyDescription ?? string.Empty).Replace('\t',' '),
                    r.Fye2025.HasValue ? r.Fye2025.Value.ToString("F0") : string.Empty,
                    r.Fye2024.HasValue ? r.Fye2024.Value.ToString("F0") : string.Empty,
                    r.Fye2023.HasValue ? r.Fye2023.Value.ToString("F0") : string.Empty,
                    r.LastTIN200Year.HasValue ? r.LastTIN200Year.Value.ToString() : string.Empty
                };
                sb.AppendLine(string.Join('\t', cols));
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = lastTin200Year.HasValue ? $"Company_export_{lastTin200Year.Value}.tsv" : "Company_export_all.tsv";
            return File(bytes, "text/tab-separated-values; charset=utf-8", fileName);
        }
    }
}

