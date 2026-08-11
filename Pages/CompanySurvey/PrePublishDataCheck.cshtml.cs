using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using TINWeb.Pages.CompanySurvey;
using TINWeb.Data;
using TINWeb.Models;

namespace TINWeb.Pages.CompanySurvey
{
    public class PrePublishDataCheckModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public List<ColumnDefinition> Columns { get; } = BuildColumns();
        public List<PrePublishDataRow> Records { get; set; } = new();
        public List<(int Value, string Label)> TinStatusOptions { get; } = TinStatusHelper.DropdownOptions.ToList();
        public string SelectedEmailContentName { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public int SelectedTinStatus { get; set; } = (int)TinStatus.Tin200;

        public PrePublishDataCheckModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task OnGetAsync()
        {
            SelectedTinStatus = NormalizeTinStatusFilter(SelectedTinStatus);

            var emailContentOptions = await _context.EmailContent
                .AsNoTracking()
                .Where(x => x.Active)
                .OrderBy(x => x.Name)
                .ThenBy(x => x.Id)
                .Select(x => new EmailContentOption
                {
                    Id = x.Id,
                    Name = x.Name,
                    Subject = x.Subject,
                    Template = x.Template
                })
                .ToListAsync();

            var selectedEmailContent = ResolveSelectedEmailContent(emailContentOptions);
            SelectedEmailContentName = selectedEmailContent?.Name ?? string.Empty;

            await LoadRowsAsync();
        }

        private static EmailContentOption? ResolveSelectedEmailContent(List<EmailContentOption> emailContentOptions)
        {
            if (emailContentOptions.Count == 0)
            {
                return null;
            }

            var prePublishMatch = emailContentOptions.FirstOrDefault(x =>
                x.Name.Contains("pre", StringComparison.OrdinalIgnoreCase)
                && x.Name.Contains("publish", StringComparison.OrdinalIgnoreCase));

            return prePublishMatch ?? emailContentOptions.First();
        }

        private async Task LoadRowsAsync()
        {
            var companies = await _context.Tin200
                .AsNoTracking()
                .Where(x => (x.TinStatus ?? (int)TinStatus.Tin200) == SelectedTinStatus)
                .Select(x => new
                {
                    x.Id,
                    x.CompanyName,
                    x.CompanyDescription,
                    x.CeoFirstName,
                    x.CeoLastName,
                    x.Fye2025,
                    x.Fye2024,
                    x.Website,
                    x.Phone,
                    x.AddStreet,
                    x.AddSuburb,
                    x.AddCity,
                    x.AddPostcode
                })
                .OrderBy(x => x.CompanyName)
                .ThenBy(x => x.Id)
                .ToListAsync();

            if (companies.Count == 0)
            {
                Records = new List<PrePublishDataRow>();
                return;
            }

            var companyIds = companies.Select(x => x.Id).ToList();

            var historyRows = await (
                from cs in _context.CompanySurvey.AsNoTracking()
                join s in _context.Survey.AsNoTracking() on cs.SurveyId equals s.Id
                where companyIds.Contains(cs.CompanyId)
                orderby cs.CompanyId, s.CurrentSurvey descending, s.FinancialYear descending, cs.Id descending
                select new
                {
                    cs.CompanyId,
                    CompanySurveyId = (int?)cs.Id,
                    s.FinancialYear,
                    cs.Estimate
                })
                .ToListAsync();

            var latestSurveyInfoByCompanyId = historyRows
                .GroupBy(x => x.CompanyId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var latest = g.FirstOrDefault();
                        var previous = latest == null
                            ? null
                            : g.FirstOrDefault(x => x.FinancialYear < latest.FinancialYear);

                        return new
                        {
                            Latest = latest,
                            Previous = previous
                        };
                    });

            var companySurveyIds = latestSurveyInfoByCompanyId.Values
                .SelectMany(x => new[] { x.Latest?.CompanySurveyId, x.Previous?.CompanySurveyId })
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            var answersByCompanySurveyId = new Dictionary<int, Dictionary<string, string>>();

            if (companySurveyIds.Count > 0)
            {
                var answerRows = await (
                    from a in _context.Answer.AsNoTracking()
                    join q in _context.Question.AsNoTracking() on a.QuestionId equals q.Id
                    where companySurveyIds.Contains(a.CompanySurveyId) && q.Title != null
                    orderby a.CompanySurveyId, a.Id descending
                    select new
                    {
                        a.CompanySurveyId,
                        QuestionTitle = q.Title!,
                        a.AnswerText,
                        a.AnswerCurrency,
                        a.AnswerNumber
                    })
                    .ToListAsync();

                foreach (var row in answerRows)
                {
                    if (!answersByCompanySurveyId.TryGetValue(row.CompanySurveyId, out var answerMap))
                    {
                        answerMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        answersByCompanySurveyId[row.CompanySurveyId] = answerMap;
                    }

                    var normalizedTitle = NormalizeTemplateKey(row.QuestionTitle);
                    if (string.IsNullOrWhiteSpace(normalizedTitle) || answerMap.ContainsKey(normalizedTitle))
                    {
                        continue;
                    }

                    var value = ResolveAnswerValue(row.QuestionTitle, row.AnswerText, row.AnswerCurrency, row.AnswerNumber);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        answerMap[normalizedTitle] = value;
                    }
                }
            }

            var records = new List<PrePublishDataRow>();

            foreach (var company in companies)
            {
                latestSurveyInfoByCompanyId.TryGetValue(company.Id, out var surveyInfo);

                var templateValues = BuildBaseTemplateValues(
                    company.CompanyName,
                    company.Website,
                    company.Phone,
                    company.AddStreet,
                    company.AddSuburb,
                    company.AddCity,
                    company.AddPostcode,
                    surveyInfo?.Latest?.Estimate,
                    surveyInfo?.Previous?.Estimate,
                    surveyInfo?.Previous != null);

                var latestCompanySurveyId = surveyInfo?.Latest?.CompanySurveyId;
                if (latestCompanySurveyId.HasValue
                    && answersByCompanySurveyId.TryGetValue(latestCompanySurveyId.Value, out var answerMap))
                {
                    foreach (var pair in answerMap)
                    {
                        if (!templateValues.ContainsKey(pair.Key))
                        {
                            templateValues[pair.Key] = pair.Value;
                        }
                    }
                }

                var row = new PrePublishDataRow
                {
                    CompanyId = company.Id,
                    CompanyName = string.IsNullOrWhiteSpace(company.CompanyName) ? "(No company name)" : company.CompanyName.Trim()
                };

                var ceoFullName = string.Join(" ", new[] { company.CeoFirstName, company.CeoLastName }
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!.Trim()));
                var canonicalTemplateValues = BuildCanonicalLookup(templateValues);
                var previousTemplateValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var previousCanonicalTemplateValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                var previousCompanySurveyId = surveyInfo?.Previous?.CompanySurveyId;
                if (previousCompanySurveyId.HasValue
                    && answersByCompanySurveyId.TryGetValue(previousCompanySurveyId.Value, out var previousAnswerMap))
                {
                    previousTemplateValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var pair in previousAnswerMap)
                    {
                        previousTemplateValues[pair.Key] = pair.Value;
                    }

                    previousCanonicalTemplateValues = BuildCanonicalLookup(previousTemplateValues);
                }

                foreach (var column in Columns)
                {
                    row.Values[column.Header] = ResolveColumnValue(
                        column,
                        templateValues,
                        canonicalTemplateValues,
                        previousTemplateValues,
                        previousCanonicalTemplateValues,
                        company.CompanyDescription,
                        ceoFullName,
                        company.Fye2025,
                        company.Fye2024);
                }

                records.Add(row);
            }

            Records = records;
        }

        private static Dictionary<string, string> BuildBaseTemplateValues(
            string? companyName,
            string? website,
            string? phone,
            string? street,
            string? suburb,
            string? city,
            string? postcode,
            bool? isEstimated,
            bool? isEstimatedYearMinusOne,
            bool hasPreviousYear)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var resolvedCompanyName = companyName?.Trim() ?? string.Empty;
            values[NormalizeTemplateKey("CompanyName")] = resolvedCompanyName;
            values[NormalizeTemplateKey("Company Name")] = resolvedCompanyName;

            values[NormalizeTemplateKey("Estimated")] = isEstimated == true ? "Estimated" : "Not Estimated";
            values[NormalizeTemplateKey("Estimated Year-1")] = !hasPreviousYear
                ? string.Empty
                : (isEstimatedYearMinusOne == true ? "Estimated" : "Not Estimated");

            var webAddress = website?.Trim();
            var companyPhone = phone?.Trim();
            var physicalAddress = BuildPhysicalAddress(street, suburb, city, postcode);

            values[NormalizeTemplateKey("Web Address")] = string.IsNullOrWhiteSpace(webAddress) ? "TBC" : webAddress;
            values[NormalizeTemplateKey("WebAddress")] = string.IsNullOrWhiteSpace(webAddress) ? "TBC" : webAddress;
            values[NormalizeTemplateKey("Company Phone")] = string.IsNullOrWhiteSpace(companyPhone) ? "TBC" : companyPhone;
            values[NormalizeTemplateKey("CompanyPhone")] = string.IsNullOrWhiteSpace(companyPhone) ? "TBC" : companyPhone;
            values[NormalizeTemplateKey("Physical Address")] = string.IsNullOrWhiteSpace(physicalAddress) ? "TBC" : physicalAddress;
            values[NormalizeTemplateKey("PhysicalAddress")] = string.IsNullOrWhiteSpace(physicalAddress) ? "TBC" : physicalAddress;

            return values;
        }

        private static string ResolveColumnValue(
            ColumnDefinition column,
            IReadOnlyDictionary<string, string> templateValues,
            IReadOnlyDictionary<string, string> canonicalTemplateValues,
            IReadOnlyDictionary<string, string> previousTemplateValues,
            IReadOnlyDictionary<string, string> previousCanonicalTemplateValues,
            string? companyDescription,
            string ceoFullName,
            decimal? fyeLastFinancialYear,
            decimal? fyeYearMinusOne)
        {
            if (string.Equals(column.Header, "Company description", StringComparison.OrdinalIgnoreCase))
            {
                var description = companyDescription?.Trim();
                if (!string.IsNullOrWhiteSpace(description))
                {
                    return description;
                }
            }

            if (string.Equals(column.Header, "CEO", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(ceoFullName))
            {
                return ceoFullName;
            }

            if (string.Equals(column.Header, "Revenue Last Financial Year ($000)", StringComparison.OrdinalIgnoreCase))
            {
                var mappedRevenue = GetFirstMappedValue(templateValues, canonicalTemplateValues, column.SourceKeys);
                if (!string.IsNullOrWhiteSpace(mappedRevenue))
                {
                    return mappedRevenue;
                }

                if (fyeLastFinancialYear.HasValue)
                {
                    return Math.Round(fyeLastFinancialYear.Value, 0, MidpointRounding.AwayFromZero).ToString("N0");
                }

                return string.Empty;
            }

            if (string.Equals(column.Header, "Revenue Year-1 ($000)", StringComparison.OrdinalIgnoreCase))
            {
                var mappedRevenue = GetFirstMappedValue(previousTemplateValues, previousCanonicalTemplateValues, column.SourceKeys);
                if (!string.IsNullOrWhiteSpace(mappedRevenue))
                {
                    return mappedRevenue;
                }

                mappedRevenue = GetFirstMappedValue(templateValues, canonicalTemplateValues, "Revenue year-1 ($000)", "Revenue year-1", "Total Revenue Year-1 ($000)", "Total Revenue Year-1");
                if (!string.IsNullOrWhiteSpace(mappedRevenue))
                {
                    return mappedRevenue;
                }

                if (fyeYearMinusOne.HasValue)
                {
                    return Math.Round(fyeYearMinusOne.Value, 0, MidpointRounding.AwayFromZero).ToString("N0");
                }

                return string.Empty;
            }

            if (string.Equals(column.Header, "Staff employed Year-1", StringComparison.OrdinalIgnoreCase))
            {
                var mappedStaff = GetFirstMappedValue(previousTemplateValues, previousCanonicalTemplateValues, column.SourceKeys);
                if (!string.IsNullOrWhiteSpace(mappedStaff))
                {
                    return mappedStaff;
                }

                mappedStaff = GetFirstMappedValue(templateValues, canonicalTemplateValues, "Staff employed year-1", "Total staff employed year-1", "Employment year-1", "Staff employed previous year");
                return mappedStaff;
            }

            return GetFirstMappedValue(templateValues, canonicalTemplateValues, column.SourceKeys);
        }

        private static string GetFirstMappedValue(
            IReadOnlyDictionary<string, string> templateValues,
            IReadOnlyDictionary<string, string> canonicalTemplateValues,
            IEnumerable<string> sourceKeys)
        {
            foreach (var key in sourceKeys)
            {
                var normalized = NormalizeTemplateKey(key);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                if (templateValues.TryGetValue(normalized, out var value)
                    && !string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }

                var canonical = CanonicalizeKey(normalized);
                if (!string.IsNullOrWhiteSpace(canonical)
                    && canonicalTemplateValues.TryGetValue(canonical, out var canonicalValue)
                    && !string.IsNullOrWhiteSpace(canonicalValue))
                {
                    return canonicalValue.Trim();
                }
            }

            return string.Empty;
        }

        private static string GetFirstMappedValue(
            IReadOnlyDictionary<string, string> templateValues,
            IReadOnlyDictionary<string, string> canonicalTemplateValues,
            params string[] sourceKeys)
        {
            return GetFirstMappedValue(templateValues, canonicalTemplateValues, (IEnumerable<string>)sourceKeys);
        }

        private static Dictionary<string, string> BuildCanonicalLookup(IReadOnlyDictionary<string, string> templateValues)
        {
            var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in templateValues)
            {
                if (string.IsNullOrWhiteSpace(pair.Value))
                {
                    continue;
                }

                var canonical = CanonicalizeKey(pair.Key);
                if (string.IsNullOrWhiteSpace(canonical) || lookup.ContainsKey(canonical))
                {
                    continue;
                }

                lookup[canonical] = pair.Value;
            }

            return lookup;
        }

        private static List<ColumnDefinition> BuildColumns()
        {
            return new List<ColumnDefinition>
            {
                new("Company name", "Company Name", "CompanyName"),
                new("Company description", "Company Description", "CompanyDescription"),
                new("Ownership", "Ownership", "Ownership Type", "OwnershipType", "Ownership structure", "Ownership Structure"),
                new("Formed", "Formed", "Formation Year", "Year of Formation", "Company formed", "Company Formed", "Year Company Formed", "Year founded", "Year founded?", "Year established", "Company founded", "Founded", "Date founded", "Date established", "Year of establishment"),
                new("Staff employed Last Financial Year", "Staff employed last financial year", "Total staff employed last financial year", "Employment last financial year", "Staff employed current", "Total Employment Last Financial Year", "Total Emplyment Last Financial Year"),
                new("Staff employed Year-1", "Staff employed last financial year", "Total staff employed last financial year", "Employment last financial year", "Staff employed current", "Total Employment Last Financial Year", "Total Emplyment Last Financial Year", "Staff employed year-1", "Total staff employed year-1", "Employment year-1", "Staff employed previous year"),
                new("Revenue Last Financial Year ($000)", "Revenue last financial year ($000)", "Revenue last financial year", "Total Revenue Last Financial Year ($000)", "Total Revenue Last Financial Year", "Revenue 2026 ($000)", "Revenue 2026", "Total Revenue 2026 ($000)", "Total Revenue 2026"),
                new("Revenue Year-1 ($000)", "Revenue last financial year ($000)", "Revenue last financial year", "Total Revenue Last Financial Year ($000)", "Total Revenue Last Financial Year", "Revenue year-1 ($000)", "Revenue year-1", "Total Revenue Year-1 ($000)", "Total Revenue Year-1", "Revenue 2025 ($000)", "Revenue 2025", "Total Revenue 2025 ($000)", "Total Revenue 2025"),
                new("Estimated Last Financial Year", "Estimated"),
                new("Estimated Year-1", "Estimated Year-1"),
                new("CEO", "CEO", "CEO Name", "Chief Executive Officer"),
                new("Web address", "Web Address", "WebAddress", "Website"),
                new("Company phone", "Company Phone", "CompanyPhone", "Phone"),
                new("Physical address", "Physical Address", "PhysicalAddress"),
                new("Primary sector", "Primary sector", "Primary Sector"),
                new("Secondary sector", "Secondary sector", "Secondary Sector"),
                new("Best business decision for 2026", "Best business decision for 2026", "Best Business Decision for 2026"),
                new("Key products", "Key products", "Key Products")
            };
        }

        private static string ResolveAnswerValue(string? questionTitle, string? answerText, decimal? answerCurrency, double? answerNumber)
        {
            if (!string.IsNullOrWhiteSpace(answerText))
            {
                return answerText.Trim();
            }

            var normalizedQuestionTitle = NormalizeTemplateKey(questionTitle);
            var useWholeNumberFormat = ShouldFormatAsWholeNumber(normalizedQuestionTitle);

            if (answerCurrency.HasValue)
            {
                return useWholeNumberFormat
                    ? Math.Round(answerCurrency.Value, 0, MidpointRounding.AwayFromZero).ToString("N0")
                    : answerCurrency.Value.ToString("N2");
            }

            if (answerNumber.HasValue)
            {
                return useWholeNumberFormat
                    ? Math.Round((decimal)answerNumber.Value, 0, MidpointRounding.AwayFromZero).ToString("N0")
                    : answerNumber.Value.ToString("0.##");
            }

            return string.Empty;
        }

        private static bool ShouldFormatAsWholeNumber(string? normalizedQuestionTitle)
        {
            if (string.IsNullOrWhiteSpace(normalizedQuestionTitle))
            {
                return false;
            }

            return normalizedQuestionTitle.Contains("revenue", StringComparison.OrdinalIgnoreCase)
                || normalizedQuestionTitle.Contains("employment", StringComparison.OrdinalIgnoreCase)
                || normalizedQuestionTitle.Contains("staffemployed", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildPhysicalAddress(string? street, string? suburb, string? city, string? postcode)
        {
            var parts = new[] { street, suburb, city, postcode }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .ToList();

            return parts.Count == 0
                ? string.Empty
                : string.Join(", ", parts);
        }

        private static string NormalizeTemplateKey(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            return Regex.Replace(raw.Trim(), @"\s+", " ");
        }

        private static string CanonicalizeKey(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var normalized = NormalizeTemplateKey(raw).ToLowerInvariant();
            return Regex.Replace(normalized, @"[^a-z0-9]", string.Empty);
        }

        private static int NormalizeTinStatusFilter(int tinStatus)
        {
            return tinStatus switch
            {
                (int)TinStatus.Tin200 => (int)TinStatus.Tin200,
                (int)TinStatus.Tin200Potential => (int)TinStatus.Tin200Potential,
                (int)TinStatus.Tin1000 => (int)TinStatus.Tin1000,
                (int)TinStatus.TinTest => (int)TinStatus.TinTest,
                _ => (int)TinStatus.Tin200
            };
        }

        public class PrePublishDataRow
        {
            public int CompanyId { get; set; }
            public string CompanyName { get; set; } = string.Empty;
            public Dictionary<string, string> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);

            public string GetValue(string columnHeader)
            {
                return Values.TryGetValue(columnHeader, out var value)
                    ? value
                    : string.Empty;
            }
        }

        public class ColumnDefinition
        {
            public string Header { get; }
            public IReadOnlyList<string> SourceKeys { get; }

            public ColumnDefinition(string header, params string[] sourceKeys)
            {
                Header = header;
                SourceKeys = sourceKeys;
            }
        }

        public class EmailContentOption
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Subject { get; set; }
            public string? Template { get; set; }
        }
    }
}
