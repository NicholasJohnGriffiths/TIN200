using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Services;

namespace TINWeb.Pages.Answers
{
    public class ExportGlobalModel : PageModel
    {
        private const string DefaultHeaderList = @"ID,Name,Description,Formation,Ownership,Primary Sector,Secondary Sector,Deeptech,Maori,Official Region,Region,CY TIN200 Rank Formatted,CY TIN200 Rank,CY-1 TIN200 Rank,Ranking Difference,CY Requested Estimate?,CEO Gender,CY Revenue,CY-1 Revenue,CY-2 Revenue,CY-3 Revenue,CY-4 Revenue,CY-5 Revenue,CY Rev Estimated?,CY-1 Rev Estimated?,CY-2 Rev Estimated?,CY EmpTTL,CY-1 EmpTTL,CY-2 EmpTTL,CY-3 EmpTTL,CY-4 EmpTTL,CY-5 EmpTTL,CY W&S,CY-1 W&S,CY-2 W&S,CY-3 W&S,CY-4 W&S,CY-5 W&S,CY R&D,CY-1 R&D,CY-2 R&D,CY-3 R&D,CY-4 R&D,CY-5 R&D,CY S&M,CY-1 S&M,CY-2 S&M,CY-3 S&M,CY-4 S&M,CY-5 S&M,CY EBITDA,CY-1 EBITDA,CY-2 EBITDA,CY-3 EBITDA,CY-4 EBITDA,CY-5 EBITDA,CY New Zealand,CY-1 New Zealand,CY-2 New Zealand,CY-3 New Zealand,CY-4 New Zealand,CY-5 New Zealand,CY Australia,CY-1 Australia,CY-2 Australia,CY-3 Australia,CY-4 Australia,CY-5 Australia,CY China,CY-1 China,CY-2 China,CY-3 China,CY-4 China,CY-5 China,CY Rest of Asia,CY-1 Rest of Asia,CY-2 Rest of Asia,CY-3 Rest of Asia,CY-4 Rest of Asia,CY-5 Rest of Asia,CY North America,CY-1 North America,CY-2 North America,CY-3 North America,CY-4 North America,CY-5 North America,CY Europe,CY-1 Europe,CY-2 Europe,CY-3 Europe,CY-4 Europe,CY-5 Europe,CY Middle East,CY-1 Middle East,CY-2 Middle East,CY-3 Middle East,CY-4 Middle East,CY-5 Middle East,CY Latin America,CY-1 Latin America,CY-2 Latin America,CY-3 Latin America,CY-4 Latin America,CY-5 Latin America,CY Africa,CY-1 Africa,CY-2 Africa,CY-3 Africa,CY-4 Africa,CY-5 Africa,CY Other,CY-1 Other,CY-2 Other,CY-3 Other,CY-4 Other,CY-5 Other,CY Total Exports,CY-1 Total Exports,CY Total Exports Growth,CY EmpNZ,CY-1 EmpNZ,CY-2 EmpNZ,CY-3 EmpNZ,CY-4 EmpNZ,CY-5 EmpNZ,CY EmpAus,CY-1 EmpAus,CY-2 EmpAus,CY-3 EmpAus,CY-4 EmpAus,CY-5 EmpAus,CY EmpChina,CY-1 EmpChina,CY-2 EmpChina,CY-3 EmpChina,CY-4 EmpChina,CY-5 EmpChina,CY EmpRestAsia,CY-1 EmpRestAsia,CY-2 EmpRestAsia,CY-3 EmpRestAsia,CY-4 EmpRestAsia,CY-5 EmpRestAsia,CY EmpNthAM,CY-1 EmpNthAM,CY-2 EmpNthAM,CY-3 EmpNthAM,CY-4 EmpNthAM,CY-5 EmpNthAM,CY EmpLA,CY-1 EmpLA,CY-2 EmpLA,CY-3 EmpLA,CY-4 EmpLA,CY-5 EmpLA,CY EmpEurope,CY-1 EmpEurope,CY-2 EmpEurope,CY-3 EmpEurope,CY-4 EmpEurope,CY-5 EmpEurope,CY EmpME,CY-1 EmpME,CY-2 EmpME,CY-3 EmpME,CY-4 EmpME,CY-5 EmpME,CY EmpOther,CY-1 EmpOther,CY-2 EmpOther,CY-3 EmpOther,CY-4 EmpOther,CY-5 EmpOther,CY EmpNorthland,CY-1 EmpNorthland,CY-2 EmpNorthland,CY-3 EmpNorthland,CY-4 EmpNorthland,CY-5 EmpNorthland,CY EmpAuckland,CY-1 EmpAuckland,CY-2 EmpAuckland,CY-3 EmpAuckland,CY-4 EmpAuckland,CY-5 EmpAuckland,CY EmpWaikato,CY-1 EmpWaikato,CY-2 EmpWaikato,CY-3 EmpWaikato,CY-4 EmpWaikato,CY-5 EmpWaikato,CY EmpBOP,CY-1 EmpBOP,CY-2 EmpBOP,CY-3 EmpBOP,CY-4 EmpBOP,CY-5 EmpBOP,CY EmpGisborne,CY-1 EmpGisborne,CY-2 EmpGisborne,CY-3 EmpGisborne,CY-4 EmpGisborne,CY-5 EmpGisborne,CY EmpHB,CY-1 EmpHB,CY-2 EmpHB,CY-3 EmpHB,CY-4 EmpHB,CY-5 EmpHB,CY EmpTaranaki,CY-1 EmpTaranaki,CY-2 EmpTaranaki,CY-3 EmpTaranaki,CY-4 EmpTaranaki,CY-5 EmpTaranaki,CY EmpWanganui,CY-1 EmpWanganui,CY-2 EmpWanganui,CY-3 EmpWanganui,CY-4 EmpWanganui,CY-5 EmpWanganui,CY EmpWgtn,CY-1 EmpWgtn,CY-2 EmpWgtn,CY-3 EmpWgtn,CY-4 EmpWgtn,CY-5 EmpWgtn,CY EmpManawatu,CY-1 EmpManawatu,CY-2 EmpManawatu,CY-3 EmpManawatu,CY-4 EmpManawatu,CY-5 EmpManawatu,CY EmpNelson,CY-1 EmpNelson,CY-2 EmpNelson,CY-3 EmpNelson,CY-4 EmpNelson,CY-5 EmpNelson,CY EmpMarlborough,CY-1 EmpMarlborough,CY-2 EmpMarlborough,CY-3 EmpMarlborough,CY-4 EmpMarlborough,CY-5 EmpMarlborough,CY EmpCanterbury,CY-1 EmpCanterbury,CY-2 EmpCanterbury,CY-3 EmpCanterbury,CY-4 EmpCanterbury,CY-5 EmpCanterbury,CY EmpWestCoast,CY-1 EmpWestCoast,CY-2 EmpWestCoast,CY-3 EmpWestCoast,CY-4 EmpWestCoast,CY-5 EmpWestCoast,CY EmpOtago,CY-1 EmpOtago,CY-2 EmpOtago,CY-3 EmpOtago,CY-4 EmpOtago,CY-5 EmpOtago,CY EmpSouthland,CY-1 EmpSouthland,CY-2 EmpSouthland,CY-3 EmpSouthland,CY-4 EmpSouthland,CY-5 EmpSouthland";

        private readonly AnswerService _answerService;
        private readonly ApplicationDbContext _context;

        public ExportGlobalModel(AnswerService answerService, ApplicationDbContext context)
        {
            _answerService = answerService;
            _context = context;
        }

        public List<int> FinancialYears { get; set; } = new();

        [BindProperty]
        public int? SelectedFinancialYear { get; set; }

        [BindProperty]
        public string HeaderList { get; set; } = string.Empty;

        public ExportPreviewSummary? PreviewSummary { get; set; }

        public async Task OnGetAsync(int? financialYear)
        {
            await LoadPageDefaultsAsync(financialYear);

            if (string.IsNullOrWhiteSpace(HeaderList))
            {
                HeaderList = DefaultHeaderList;
            }
        }

        public async Task<IActionResult> OnPostPreviewAsync()
        {
            await LoadPageDefaultsAsync(SelectedFinancialYear);
            if (!ValidateYear())
            {
                return Page();
            }

            if (string.IsNullOrWhiteSpace(HeaderList))
            {
                HeaderList = DefaultHeaderList;
            }

            var headers = ParseHeaders(HeaderList);
            if (!headers.Any())
            {
                ModelState.AddModelError(string.Empty, "Paste at least one column header before running preview.");
                return Page();
            }

            PreviewSummary = await BuildPreviewSummaryAsync(SelectedFinancialYear!.Value, headers, 10);
            return Page();
        }

        public async Task<IActionResult> OnPostExportAsync()
        {
            await LoadPageDefaultsAsync(SelectedFinancialYear);
            if (!ValidateYear())
            {
                return Page();
            }

            if (string.IsNullOrWhiteSpace(HeaderList))
            {
                HeaderList = DefaultHeaderList;
            }

            var headers = ParseHeaders(HeaderList);
            if (!headers.Any())
            {
                ModelState.AddModelError(string.Empty, "Paste at least one column header before exporting.");
                return Page();
            }

            var export = await BuildPreviewSummaryAsync(SelectedFinancialYear!.Value, headers, 0);
            var csv = new StringBuilder();
            csv.AppendLine(string.Join(',', headers.Select(EscapeCsv)));

            foreach (var row in export.AllRows)
            {
                csv.AppendLine(string.Join(',', row.Values.Select(EscapeCsv)));
            }

            var fileName = $"answers-global-export-fy-{SelectedFinancialYear.Value}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv; charset=utf-8", fileName);
        }

        private async Task LoadPageDefaultsAsync(int? financialYear)
        {
            FinancialYears = await _answerService.GetAvailableFinancialYearsAsync();
            SelectedFinancialYear = financialYear ?? await _answerService.GetCurrentSurveyFinancialYearAsync() ?? FinancialYears.FirstOrDefault();
        }

        private bool ValidateYear()
        {
            if (!SelectedFinancialYear.HasValue)
            {
                ModelState.AddModelError(string.Empty, "No survey year is available. Configure a survey year and try again.");
                return false;
            }

            return true;
        }

        private static List<string> ParseHeaders(string? rawHeaders)
        {
            if (string.IsNullOrWhiteSpace(rawHeaders))
            {
                return new List<string>();
            }

            var separators = new[] { ',', '\n', '\r', '\t' };
            return rawHeaders
                .Split(separators, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task<ExportPreviewSummary> BuildPreviewSummaryAsync(int financialYear, List<string> headers, int previewRowLimit)
        {
            var builtInHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ID",
                "Name"
            };

            var headerMappings = headers
                .Select(h =>
                {
                    var mapping = ResolveHeaderMapping(h);
                    return new HeaderMapping
                    {
                        Header = h,
                        BaseHeader = mapping.BaseHeader,
                        YearOffset = mapping.YearOffset
                    };
                })
                .ToList();

            var questionMap = await _context.Question
                .AsNoTracking()
                .Where(q => !string.IsNullOrWhiteSpace(q.ImportColumnNameAlt))
                .Select(q => new
                {
                    q.Id,
                    ImportAlt = q.ImportColumnNameAlt!.Trim()
                })
                .ToListAsync();

            var questionIdsByImportAlt = questionMap
                .GroupBy(x => x.ImportAlt, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Id).Distinct().ToList(), StringComparer.OrdinalIgnoreCase);

            var questionMetadataById = await _context.Question
                .AsNoTracking()
                .Where(q => questionMap.Select(x => x.Id).Contains(q.Id))
                .Select(q => new
                {
                    q.Id,
                    q.AnswerType,
                    q.Multi1,
                    q.Multi2,
                    q.Multi3,
                    q.Multi4
                })
                .ToDictionaryAsync(
                    x => x.Id,
                    x => new QuestionMetadata
                    {
                        AnswerType = x.AnswerType,
                        ChoiceOptions = new[] { x.Multi1, x.Multi2, x.Multi3, x.Multi4 }
                            .Where(v => !string.IsNullOrWhiteSpace(v))
                            .Select(v => v!.Trim())
                            .ToList()
                    });

            var matchedHeaders = headerMappings
                .Where(h => builtInHeaders.Contains(h.Header) || questionIdsByImportAlt.ContainsKey(h.BaseHeader))
                .Select(h => h.Header)
                .ToList();

            var unmatchedHeaders = headerMappings
                .Where(h => !builtInHeaders.Contains(h.Header) && !questionIdsByImportAlt.ContainsKey(h.BaseHeader))
                .Select(h => h.Header)
                .ToList();

            var companySurveyRows = await (
                from companySurvey in _context.CompanySurvey.AsNoTracking()
                join survey in _context.Survey.AsNoTracking() on companySurvey.SurveyId equals survey.Id
                join company in _context.Tin200.AsNoTracking() on companySurvey.CompanyId equals company.Id
                where survey.FinancialYear <= financialYear && survey.FinancialYear >= financialYear - 5
                select new
                {
                    CompanySurveyId = companySurvey.Id,
                    CompanyId = company.Id,
                    SurveyYear = survey.FinancialYear,
                    company.CompanyName,
                    company.ExternalId,
                    company.Email
                })
                .ToListAsync();

            var companyRows = companySurveyRows
                .Where(x => x.SurveyYear == financialYear)
                .GroupBy(x => x.CompanyId)
                .Select(g => g.OrderByDescending(x => x.CompanySurveyId).First())
                .OrderBy(x => x.CompanyName)
                .ThenBy(x => x.CompanyId)
                .ToList();

            var companySurveyIdByCompanyAndYear = companySurveyRows
                .GroupBy(x => new { x.CompanyId, x.SurveyYear })
                .ToDictionary(
                    g => (g.Key.CompanyId, g.Key.SurveyYear),
                    g => g.OrderByDescending(x => x.CompanySurveyId).First().CompanySurveyId);

            var currentCompanyIds = companyRows.Select(x => x.CompanyId).Distinct().ToList();

            var companySurveyIds = companySurveyIdByCompanyAndYear
                .Where(x => currentCompanyIds.Contains(x.Key.CompanyId))
                .Select(x => x.Value)
                .Distinct()
                .ToList();

            var matchedQuestionIds = headerMappings
                .Where(h => questionIdsByImportAlt.ContainsKey(h.BaseHeader))
                .SelectMany(h => questionIdsByImportAlt[h.BaseHeader])
                .Distinct()
                .ToList();

            var answers = await _context.Answer
                .AsNoTracking()
                .Where(a => companySurveyIds.Contains(a.CompanySurveyId) && matchedQuestionIds.Contains(a.QuestionId))
                .Select(a => new
                {
                    a.Id,
                    a.CompanySurveyId,
                    a.QuestionId,
                    a.AnswerText,
                    a.AnswerCurrency,
                    a.AnswerNumber
                })
                .ToListAsync();

            var latestAnswerBySurveyAndQuestion = answers
                .GroupBy(a => new { a.CompanySurveyId, a.QuestionId })
                .ToDictionary(
                    g => (g.Key.CompanySurveyId, g.Key.QuestionId),
                    g => g.OrderByDescending(x => x.Id).First());

            var allRows = new List<ExportPreviewRow>(companyRows.Count);

            foreach (var company in companyRows)
            {
                var values = new List<string>(headers.Count);

                foreach (var header in headerMappings)
                {
                    if (string.Equals(header.Header, "ID", StringComparison.OrdinalIgnoreCase))
                    {
                        values.Add(company.ExternalId ?? string.Empty);
                        continue;
                    }

                    if (string.Equals(header.Header, "Name", StringComparison.OrdinalIgnoreCase))
                    {
                        values.Add(company.CompanyName ?? string.Empty);
                        continue;
                    }

                    if (!questionIdsByImportAlt.TryGetValue(header.BaseHeader, out var questionIds))
                    {
                        values.Add(string.Empty);
                        continue;
                    }

                    var targetYear = financialYear - header.YearOffset;
                    if (!companySurveyIdByCompanyAndYear.TryGetValue((company.CompanyId, targetYear), out var targetCompanySurveyId))
                    {
                        values.Add(string.Empty);
                        continue;
                    }

                    var bestAnswer = questionIds
                        .Select(questionId => latestAnswerBySurveyAndQuestion.TryGetValue((targetCompanySurveyId, questionId), out var answer) ? answer : null)
                        .Where(answer => answer != null)
                        .OrderByDescending(answer => answer!.Id)
                        .FirstOrDefault();

                    var metadata = bestAnswer != null && questionMetadataById.TryGetValue(bestAnswer.QuestionId, out var resolvedMetadata)
                        ? resolvedMetadata
                        : null;

                    values.Add(FormatAnswer(
                        bestAnswer?.AnswerText,
                        bestAnswer?.AnswerCurrency,
                        bestAnswer?.AnswerNumber,
                        metadata?.AnswerType,
                        metadata?.ChoiceOptions,
                        header.Header));
                }

                allRows.Add(new ExportPreviewRow
                {
                    CompanySurveyId = company.CompanySurveyId,
                    CompanyName = company.CompanyName,
                    ExternalId = company.ExternalId,
                    Values = values
                });
            }

            return new ExportPreviewSummary
            {
                FinancialYear = financialYear,
                HeaderCount = headers.Count,
                RecordsFound = allRows.Count,
                MatchedHeaders = matchedHeaders,
                UnmatchedHeaders = unmatchedHeaders,
                Headers = headers,
                PreviewRows = previewRowLimit > 0 ? allRows.Take(previewRowLimit).ToList() : new List<ExportPreviewRow>(),
                AllRows = allRows
            };
        }

        private static (string BaseHeader, int YearOffset) ResolveHeaderMapping(string header)
        {
            var normalized = (header ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return (string.Empty, 0);
            }

            var match = Regex.Match(normalized, @"-(?<offset>[1-5])\b", RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                return (normalized, 0);
            }

            var offsetValue = int.Parse(match.Groups["offset"].Value, CultureInfo.InvariantCulture);
            var baseHeader = Regex.Replace(normalized, @"-(?<offset>[1-5])\b", string.Empty, RegexOptions.CultureInvariant)
                .Replace("  ", " ")
                .Trim();

            return (baseHeader, offsetValue);
        }

        private static string FormatAnswer(
            string? answerText,
            decimal? answerCurrency,
            double? answerNumber,
            string? answerType,
            IReadOnlyList<string>? choiceOptions,
            string? header)
        {
            var isBooleanType = IsBooleanLikeField(answerType, choiceOptions, header);

            if (!string.IsNullOrWhiteSpace(answerText))
            {
                var normalizedText = answerText.Trim();

                if (normalizedText.Equals("true", StringComparison.OrdinalIgnoreCase)
                    || (isBooleanType && (normalizedText.Equals("1", StringComparison.OrdinalIgnoreCase) || normalizedText.Equals("yes", StringComparison.OrdinalIgnoreCase))))
                {
                    return "Yes";
                }

                if (normalizedText.Equals("false", StringComparison.OrdinalIgnoreCase)
                    || (isBooleanType && (normalizedText.Equals("0", StringComparison.OrdinalIgnoreCase) || normalizedText.Equals("no", StringComparison.OrdinalIgnoreCase))))
                {
                    return "No";
                }

                return answerText;
            }

            if (answerCurrency.HasValue)
            {
                return answerCurrency.Value.ToString(CultureInfo.InvariantCulture);
            }

            if (answerNumber.HasValue)
            {
                if (isBooleanType)
                {
                    if (Math.Abs(answerNumber.Value - 1d) < 0.000001d)
                    {
                        return "Yes";
                    }

                    if (Math.Abs(answerNumber.Value) < 0.000001d)
                    {
                        return "No";
                    }
                }

                return answerNumber.Value.ToString(CultureInfo.InvariantCulture);
            }

            return string.Empty;
        }

        private static bool IsBooleanLikeField(string? answerType, IReadOnlyList<string>? choiceOptions, string? header)
        {
            var normalizedType = (answerType ?? string.Empty).Trim();
            if (normalizedType.Equals("Boolean", StringComparison.OrdinalIgnoreCase)
                || normalizedType.Equals("Bool", StringComparison.OrdinalIgnoreCase)
                || normalizedType.Equals("YesNo", StringComparison.OrdinalIgnoreCase)
                || normalizedType.Equals("TrueFalse", StringComparison.OrdinalIgnoreCase)
                || normalizedType.Equals("True/False", StringComparison.OrdinalIgnoreCase)
                || normalizedType.Equals("Yes/No", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var options = (choiceOptions ?? Array.Empty<string>())
                .Select(v => (v ?? string.Empty).Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToList();

            if (options.Count > 0)
            {
                var optionSet = new HashSet<string>(options, StringComparer.OrdinalIgnoreCase);
                var hasYesNo = optionSet.Contains("Yes") && optionSet.Contains("No");
                var hasTrueFalse = optionSet.Contains("True") && optionSet.Contains("False");

                if (hasYesNo || hasTrueFalse)
                {
                    return true;
                }
            }

            var normalizedHeader = (header ?? string.Empty).Trim();
            return normalizedHeader.Contains('?');
        }

        private sealed class QuestionMetadata
        {
            public string? AnswerType { get; set; }
            public List<string> ChoiceOptions { get; set; } = new();
        }

        private sealed class HeaderMapping
        {
            public string Header { get; set; } = string.Empty;
            public string BaseHeader { get; set; } = string.Empty;
            public int YearOffset { get; set; }
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

        public class ExportPreviewSummary
        {
            public int FinancialYear { get; set; }
            public int HeaderCount { get; set; }
            public int RecordsFound { get; set; }
            public List<string> MatchedHeaders { get; set; } = new();
            public List<string> UnmatchedHeaders { get; set; } = new();
            public List<string> Headers { get; set; } = new();
            public List<ExportPreviewRow> PreviewRows { get; set; } = new();
            public List<ExportPreviewRow> AllRows { get; set; } = new();
        }

        public class ExportPreviewRow
        {
            public int CompanySurveyId { get; set; }
            public string? CompanyName { get; set; }
            public string? ExternalId { get; set; }
            public List<string> Values { get; set; } = new();
        }
    }
}
