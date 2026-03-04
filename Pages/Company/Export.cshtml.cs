using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWorkspaceTemp.Services;

namespace TINWorkspaceTemp.Pages.Company
{
    public class ExportModel : PageModel
    {
        private readonly CompanyService _service;

        public ExportModel(CompanyService service)
        {
            _service = service;
        }

        public async Task<IActionResult> OnGetAsync(int? year)
        {
            // default to latest when not supplied
            var years = await _service.GetAvailableFinancialYearsAsync();
            if (!year.HasValue)
            {
                year = years.FirstOrDefault();
            }

            var records = await _service.GetAllCompaniesAsync(year);

            var sb = new StringBuilder();
            // header
            sb.AppendLine(string.Join('\t', new[] { "Id", "CEO First Name", "CEO Last Name", "Email", "External ID", "Company Name", "Company Description", "FYE 2025", "FYE 2024", "FYE 2023", "FinancialYear" }));

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
                    r.FinancialYear.HasValue ? r.FinancialYear.Value.ToString() : string.Empty
                };
                sb.AppendLine(string.Join('\t', cols));
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = year.HasValue ? $"Company_export_{year.Value}.tsv" : "Company_export_all.tsv";
            return File(bytes, "text/tab-separated-values; charset=utf-8", fileName);
        }
    }
}

