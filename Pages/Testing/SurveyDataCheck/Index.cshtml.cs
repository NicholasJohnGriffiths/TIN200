using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using TINWeb.Data;
using TINWeb.Models;

namespace TINWeb.Pages.Testing.SurveyDataCheck
{
    public class IndexModel : PageModel
    {
        private static readonly ConcurrentDictionary<string, PendingUpdatePreview> PendingUpdatePreviews = new(StringComparer.OrdinalIgnoreCase);
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public int? SelectedFinancialYear { get; set; }

        /// <summary>Question IDs for each row in the mapping table.</summary>
        [BindProperty]
        public List<int> QuestionIds { get; set; } = new();

        /// <summary>Include flags aligned with QuestionIds; true means include in check process.</summary>
        [BindProperty]
        public List<bool> IncludeQuestions { get; set; } = new();

        /// <summary>Parallel to QuestionIds: manually chosen Excel column name per slot.
        /// Empty means "use the question's own ImportColumnNameAlt".</summary>
        [BindProperty]
        public List<string> SelectedColumnNames { get; set; } = new();

        [BindProperty]
        public string ExternalIdColumnName { get; set; } = "External ID";

        [BindProperty]
        public bool OverwriteExistingData { get; set; }

        public List<int> FinancialYears { get; set; } = new();
        public List<QuestionOption> Questions { get; set; } = new();
        public int SlotCount => Questions.Count;
        public List<string> AllImportColumnNames { get; set; } = new();
        public List<CompanySurveyRow> CompanySurveys { get; set; } = new();
        public List<CheckResultRow> CheckResults { get; set; } = new();
        public List<QuestionHeader> SelectedQuestionHeaders { get; set; } = new();
        public UpdatePreviewResult? UpdatePreview { get; set; }
        public string? PendingUpdateToken { get; set; }
        public bool HasResults { get; set; }
        [TempData]
        public string? StatusMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            await LoadPageDataAsync();
        }

        public async Task<IActionResult> OnPostCheckDataAsync(IFormFile? excelFile)
        {
            await LoadPageDataAsync();

            if (!TryValidateRequest(excelFile, out var validationError))
            {
                ErrorMessage = validationError;
                return Page();
            }

            var slotMappings = await BuildSlotMappingsAsync();
            if (!slotMappings.Any())
            {
                ErrorMessage = "Please tick at least one question to check.";
                return Page();
            }

            SelectedQuestionHeaders = slotMappings
                .Select(m => new QuestionHeader
                {
                    Title = m.Question.Title ?? m.Question.ImportColumnNameAlt ?? m.Question.ImportColumnName ?? $"Q{m.QuestionId}",
                    ExcelColumnName = m.ExcelColumnName
                })
                .ToList();

            // Load latest DB answers
            var companySurveyIds = CompanySurveys.Select(x => x.CompanySurveyId).ToList();
            var slotQuestionIds = slotMappings.Select(m => m.QuestionId).Distinct().ToList();
            var answerLookup = new Dictionary<(int CompanySurveyId, int QuestionId), Answer>();

            if (companySurveyIds.Any() && slotQuestionIds.Any())
            {
                var latestAnswerIds = await _context.Answer
                    .Where(a => companySurveyIds.Contains(a.CompanySurveyId)
                                && slotQuestionIds.Contains(a.QuestionId))
                    .GroupBy(a => new { a.CompanySurveyId, a.QuestionId })
                    .Select(g => g.Max(a => a.Id))
                    .ToListAsync();

                var dbAnswers = await _context.Answer
                    .Where(a => latestAnswerIds.Contains(a.Id))
                    .ToListAsync();

                answerLookup = dbAnswers
                    .GroupBy(a => (a.CompanySurveyId, a.QuestionId))
                    .ToDictionary(g => g.Key, g => g.First());
            }

            try
            {
                var excelRows = await BuildExcelRowsAsync(excelFile!, slotMappings);

                CheckResults = CompanySurveys.Select(cs =>
                {
                    excelRows.TryGetValue(cs.ExternalId ?? string.Empty, out var excelCells);
                    var foundInExcel = excelCells != null && !string.IsNullOrWhiteSpace(cs.ExternalId);

                    var qResults = slotMappings.Select(slot =>
                    {
                        var questionTitle = slot.Question.Title ?? slot.Question.ImportColumnNameAlt ?? slot.Question.ImportColumnName ?? $"Q{slot.QuestionId}";

                        if (!foundInExcel)
                            return new QuestionCheckResult { QuestionTitle = questionTitle, Status = CheckStatus.NoExcelMatch };

                        if (slot.ExcelColumnNumber < 0)
                            return new QuestionCheckResult
                            {
                                QuestionTitle = questionTitle,
                                ExcelValue = $"(column '{slot.ExcelColumnName}' not found)",
                                Status = CheckStatus.ColumnNotFound
                            };

                        answerLookup.TryGetValue((cs.CompanySurveyId, slot.QuestionId), out var answer);
                        var dbValue = FormatAnswerValue(answer);
                        excelCells!.TryGetValue(slot.ExcelColumnNumber, out var xlValue);
                        xlValue ??= string.Empty;

                        return new QuestionCheckResult
                        {
                            QuestionTitle = questionTitle,
                            DbValue = dbValue,
                            ExcelValue = xlValue,
                            Status = ValuesMatch(dbValue, xlValue) ? CheckStatus.Match : CheckStatus.Mismatch
                        };
                    }).ToList();

                    var rowStatus = !foundInExcel
                        ? CheckStatus.NoExcelMatch
                        : qResults.Any(r => r.Status == CheckStatus.Mismatch)
                            ? CheckStatus.Mismatch
                            : CheckStatus.Match;

                    return new CheckResultRow
                    {
                        CompanyName = cs.CompanyName,
                        ExternalId = cs.ExternalId,
                        SurveyLink = cs.SurveyLink,
                        RowStatus = rowStatus,
                        QuestionResults = qResults
                    };
                }).ToList();

                HasResults = true;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to process Excel file: {ex.Message}";
                return Page();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostUpdateDbDataAsync(IFormFile? excelFile)
        {
            await LoadPageDataAsync();

            if (!TryValidateRequest(excelFile, out var validationError))
            {
                ErrorMessage = validationError;
                return Page();
            }

            var slotMappings = await BuildSlotMappingsAsync();
            if (!slotMappings.Any())
            {
                ErrorMessage = "Please tick at least one question to update.";
                return Page();
            }

            try
            {
                CleanupExpiredPendingPreviews();

                var token = Guid.NewGuid().ToString("N");
                var tempDir = Path.Combine(Path.GetTempPath(), "tinweb-survey-data-check");
                Directory.CreateDirectory(tempDir);
                var tempFilePath = Path.Combine(tempDir, $"{token}.xlsx");

                await using (var tempFileStream = System.IO.File.Create(tempFilePath))
                {
                    await excelFile!.CopyToAsync(tempFileStream);
                }

                await using var previewStream = System.IO.File.OpenRead(tempFilePath);
                var preview = await BuildUpdatePreviewAsync(previewStream, slotMappings, OverwriteExistingData);

                PendingUpdatePreviews[token] = new PendingUpdatePreview
                {
                    Token = token,
                    TempFilePath = tempFilePath,
                    CreatedUtc = DateTime.UtcNow,
                    OverwriteExistingData = OverwriteExistingData
                };

                UpdatePreview = preview;
                PendingUpdateToken = token;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to preview DB update: {ex.Message}";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostConfirmUpdateDbDataAsync(string? previewToken)
        {
            await LoadPageDataAsync();

            CleanupExpiredPendingPreviews();

            if (string.IsNullOrWhiteSpace(previewToken)
                || !PendingUpdatePreviews.TryGetValue(previewToken, out var pendingPreview)
                || !System.IO.File.Exists(pendingPreview.TempFilePath))
            {
                ErrorMessage = "Update preview expired or could not be found. Please preview the update again.";
                return Page();
            }

            var slotMappings = await BuildSlotMappingsAsync();
            if (!slotMappings.Any())
            {
                ErrorMessage = "Please tick at least one question to update.";
                return Page();
            }

            try
            {
                OverwriteExistingData = pendingPreview.OverwriteExistingData;
                await using var updateStream = System.IO.File.OpenRead(pendingPreview.TempFilePath);
                var excelRows = await BuildExcelRowsAsync(updateStream, slotMappings);
                var plan = await BuildUpdatePlanAsync(excelRows, slotMappings, OverwriteExistingData);

                foreach (var operation in plan.Operations)
                {
                    if (operation.ExistingAnswer == null)
                    {
                        var answer = new Answer
                        {
                            CompanySurveyId = operation.CompanySurveyId,
                            QuestionId = operation.QuestionId
                        };

                        ApplyParsedValue(answer, operation.Value);
                        _context.Answer.Add(answer);
                    }
                    else
                    {
                        ApplyParsedValue(operation.ExistingAnswer, operation.Value);
                    }
                }

                await _context.SaveChangesAsync();

                StatusMessage = $"DB update completed. Inserted: {plan.InsertedCount}, Updated: {plan.UpdatedCount}, Skipped existing: {plan.SkippedExistingCount}, Missing companies in Excel: {plan.MissingCompanyCount}, Missing columns: {plan.MissingColumnCount}, Blank Excel values skipped: {plan.BlankExcelValueCount}.";

                // Immediately rerun the check against the updated DB so the table reflects the final state.
                await PopulateCheckResultsAsync(excelRows, slotMappings);

                PendingUpdatePreviews.TryRemove(previewToken, out _);
                TryDeleteFile(pendingPreview.TempFilePath);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to update DB data: {ex.Message}";
            }

            return Page();
        }

        private async Task LoadPageDataAsync()
        {
            FinancialYears = await _context.Survey
                .Select(s => s.FinancialYear)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();

            if (!SelectedFinancialYear.HasValue && FinancialYears.Any())
                SelectedFinancialYear = FinancialYears.First();

            // Left selector: all active questions by title
            Questions = await _context.Question
                .Where(q => q.Active != false)
                .OrderBy(q => q.OrderNumber)
                .ThenBy(q => q.Id)
                .Select(q => new QuestionOption
                {
                    Id = q.Id,
                    TitleLabel = (q.Title ?? q.ImportColumnNameAlt ?? q.ImportColumnName) ?? ("Q" + q.Id.ToString()),
                    ImportColumnName = q.ImportColumnNameAlt
                })
                .ToListAsync();

            // Right selector: distinct ImportColumnNameAlt values
            AllImportColumnNames = await _context.Question
                .Where(q => q.Active != false && !string.IsNullOrWhiteSpace(q.ImportColumnNameAlt))
                .Select(q => q.ImportColumnNameAlt!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            if (SelectedFinancialYear.HasValue)
            {
                CompanySurveys = await (
                    from cs in _context.CompanySurvey
                    join survey in _context.Survey on cs.SurveyId equals survey.Id
                    join company in _context.Tin200 on cs.CompanyId equals company.Id into companyJoin
                    from company in companyJoin.DefaultIfEmpty()
                    where survey.FinancialYear == SelectedFinancialYear.Value
                    select new CompanySurveyRow
                    {
                        CompanySurveyId = cs.Id,
                        CompanyName = company.CompanyName ?? string.Empty,
                        ExternalId = company.ExternalId ?? string.Empty,
                        SurveyLink = cs.SurveyLink
                    }
                ).OrderBy(x => x.CompanyName).ToListAsync();
            }

            while (QuestionIds.Count < SlotCount) QuestionIds.Add(Questions[QuestionIds.Count].Id);
            while (IncludeQuestions.Count < SlotCount) IncludeQuestions.Add(false);
            while (SelectedColumnNames.Count < SlotCount) SelectedColumnNames.Add(string.Empty);

            if (QuestionIds.Count > SlotCount) QuestionIds = QuestionIds.Take(SlotCount).ToList();
            if (IncludeQuestions.Count > SlotCount) IncludeQuestions = IncludeQuestions.Take(SlotCount).ToList();
            if (SelectedColumnNames.Count > SlotCount) SelectedColumnNames = SelectedColumnNames.Take(SlotCount).ToList();

            for (var i = 0; i < SlotCount; i++)
            {
                QuestionIds[i] = Questions[i].Id;

                if (string.IsNullOrWhiteSpace(SelectedColumnNames[i]))
                {
                    SelectedColumnNames[i] = Questions[i].ImportColumnName?.Trim() ?? string.Empty;
                }
            }
        }

        private static string FormatAnswerValue(Answer? answer)
        {
            if (answer == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(answer.AnswerText)) return answer.AnswerText.Trim();
            if (answer.AnswerCurrency.HasValue)
                return answer.AnswerCurrency.Value.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
            if (answer.AnswerNumber.HasValue)
                return answer.AnswerNumber.Value.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
            return string.Empty;
        }

        private static bool ValuesMatch(string dbValue, string excelValue)
        {
            if (string.IsNullOrWhiteSpace(dbValue) && string.IsNullOrWhiteSpace(excelValue)) return true;
            var db = dbValue.Trim();
            var xl = excelValue.Trim();
            if (double.TryParse(db, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var dbNum) &&
                double.TryParse(xl, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var xlNum))
                return Math.Abs(dbNum - xlNum) < 0.001;
            return string.Equals(db, xl, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeCol(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            return new string(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        }

        private bool TryValidateRequest(IFormFile? excelFile, out string errorMessage)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                errorMessage = "Please select an Excel (.xlsx) file.";
                return false;
            }

            if (!excelFile.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Only .xlsx Excel files are supported.";
                return false;
            }

            if (!SelectedFinancialYear.HasValue)
            {
                errorMessage = "Please select a financial year.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(ExternalIdColumnName))
            {
                errorMessage = "External ID column name is required.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private async Task<List<SlotMapping>> BuildSlotMappingsAsync()
        {
            var activeSlotIds = new List<int>();
            for (var i = 0; i < SlotCount; i++)
            {
                var include = i < IncludeQuestions.Count && IncludeQuestions[i];
                if (!include)
                {
                    continue;
                }

                var qId = i < QuestionIds.Count ? QuestionIds[i] : 0;
                if (qId > 0)
                {
                    activeSlotIds.Add(qId);
                }
            }

            var questionIds = activeSlotIds.Distinct().ToList();
            if (!questionIds.Any())
            {
                return new List<SlotMapping>();
            }

            var questionsById = await _context.Question
                .Where(q => questionIds.Contains(q.Id))
                .ToDictionaryAsync(q => q.Id);

            var slotMappings = new List<SlotMapping>();
            for (var i = 0; i < SlotCount; i++)
            {
                var include = i < IncludeQuestions.Count && IncludeQuestions[i];
                if (!include)
                {
                    continue;
                }

                var qId = i < QuestionIds.Count ? QuestionIds[i] : 0;
                if (qId <= 0 || !questionsById.TryGetValue(qId, out var q))
                {
                    continue;
                }

                var overrideCol = i < SelectedColumnNames.Count ? SelectedColumnNames[i] : null;
                var effectiveColName = string.IsNullOrWhiteSpace(overrideCol)
                    ? q.ImportColumnNameAlt
                    : overrideCol.Trim();

                slotMappings.Add(new SlotMapping
                {
                    QuestionId = qId,
                    Question = q,
                    ExcelColumnName = effectiveColName
                });
            }

            return slotMappings;
        }

        private async Task PopulateCheckResultsAsync(Dictionary<string, Dictionary<int, string>> excelRows, List<SlotMapping> slotMappings)
        {
            SelectedQuestionHeaders = slotMappings
                .Select(m => new QuestionHeader
                {
                    Title = m.Question.Title ?? m.Question.ImportColumnNameAlt ?? m.Question.ImportColumnName ?? $"Q{m.QuestionId}",
                    ExcelColumnName = m.ExcelColumnName
                })
                .ToList();

            var companySurveyIds = CompanySurveys.Select(x => x.CompanySurveyId).ToList();
            var slotQuestionIds = slotMappings.Select(m => m.QuestionId).Distinct().ToList();

            var latestAnswerIds = await _context.Answer
                .Where(a => companySurveyIds.Contains(a.CompanySurveyId) && slotQuestionIds.Contains(a.QuestionId))
                .GroupBy(a => new { a.CompanySurveyId, a.QuestionId })
                .Select(g => g.Max(a => a.Id))
                .ToListAsync();

            var answerLookup = await _context.Answer
                .Where(a => latestAnswerIds.Contains(a.Id))
                .ToListAsync();

            var latestByKey = answerLookup
                .GroupBy(a => (a.CompanySurveyId, a.QuestionId))
                .ToDictionary(g => g.Key, g => g.First());

            CheckResults = CompanySurveys.Select(cs =>
            {
                excelRows.TryGetValue(cs.ExternalId ?? string.Empty, out var excelCells);
                var foundInExcel = excelCells != null && !string.IsNullOrWhiteSpace(cs.ExternalId);

                var qResults = slotMappings.Select(slot =>
                {
                    var questionTitle = slot.Question.Title ?? slot.Question.ImportColumnNameAlt ?? slot.Question.ImportColumnName ?? $"Q{slot.QuestionId}";

                    if (!foundInExcel)
                    {
                        return new QuestionCheckResult { QuestionTitle = questionTitle, Status = CheckStatus.NoExcelMatch };
                    }

                    if (slot.ExcelColumnNumber < 0)
                    {
                        return new QuestionCheckResult
                        {
                            QuestionTitle = questionTitle,
                            ExcelValue = $"(column '{slot.ExcelColumnName}' not found)",
                            Status = CheckStatus.ColumnNotFound
                        };
                    }

                    latestByKey.TryGetValue((cs.CompanySurveyId, slot.QuestionId), out var answer);
                    var dbValue = FormatAnswerValue(answer);
                    excelCells!.TryGetValue(slot.ExcelColumnNumber, out var xlValue);
                    xlValue ??= string.Empty;

                    return new QuestionCheckResult
                    {
                        QuestionTitle = questionTitle,
                        DbValue = dbValue,
                        ExcelValue = xlValue,
                        Status = ValuesMatch(dbValue, xlValue) ? CheckStatus.Match : CheckStatus.Mismatch
                    };
                }).ToList();

                var rowStatus = !foundInExcel
                    ? CheckStatus.NoExcelMatch
                    : qResults.Any(r => r.Status == CheckStatus.Mismatch)
                        ? CheckStatus.Mismatch
                        : CheckStatus.Match;

                return new CheckResultRow
                {
                    CompanyName = cs.CompanyName,
                    ExternalId = cs.ExternalId,
                    SurveyLink = cs.SurveyLink,
                    RowStatus = rowStatus,
                    QuestionResults = qResults
                };
            }).ToList();

            HasResults = true;
        }

        private async Task<Dictionary<string, Dictionary<int, string>>> BuildExcelRowsAsync(IFormFile excelFile, List<SlotMapping> slotMappings)
        {
            using var memStream = new MemoryStream();
            await excelFile.CopyToAsync(memStream);
            memStream.Position = 0;

            return await BuildExcelRowsAsync(memStream, slotMappings);
        }

        private async Task<Dictionary<string, Dictionary<int, string>>> BuildExcelRowsAsync(Stream excelStream, List<SlotMapping> slotMappings)
        {
            excelStream.Position = 0;

            using var workbook = new XLWorkbook(excelStream);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
            {
                throw new InvalidOperationException("The Excel file contains no worksheets.");
            }

            var lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

            var colNumToName = new Dictionary<int, string>();
            var nameToColNum = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var normToColNum = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (var col = 1; col <= lastCol; col++)
            {
                var header = worksheet.Cell(1, col).GetString().Trim();
                if (string.IsNullOrWhiteSpace(header)) continue;
                colNumToName[col] = header;
                if (!nameToColNum.ContainsKey(header)) nameToColNum[header] = col;
                var norm = NormalizeCol(header);
                if (!string.IsNullOrWhiteSpace(norm) && !normToColNum.ContainsKey(norm))
                    normToColNum[norm] = col;
            }

            int FindCol(string? name)
            {
                if (string.IsNullOrWhiteSpace(name)) return -1;
                if (nameToColNum.TryGetValue(name, out var c)) return c;
                var norm = NormalizeCol(name);
                return !string.IsNullOrWhiteSpace(norm) && normToColNum.TryGetValue(norm, out c) ? c : -1;
            }

            var extIdColNum = FindCol(ExternalIdColumnName);
            if (extIdColNum < 0)
            {
                var available = colNumToName.Values.Take(25);
                throw new InvalidOperationException($"Column '{ExternalIdColumnName}' was not found in the Excel file. Available columns: {string.Join(", ", available)}");
            }

            foreach (var slot in slotMappings)
            {
                slot.ExcelColumnNumber = FindCol(slot.ExcelColumnName);
            }

            var excelRows = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
            for (var row = 2; row <= lastRow; row++)
            {
                var extId = worksheet.Cell(row, extIdColNum).GetString().Trim();
                if (string.IsNullOrWhiteSpace(extId) || excelRows.ContainsKey(extId))
                {
                    continue;
                }

                var cells = new Dictionary<int, string>();
                for (var col = 1; col <= lastCol; col++)
                {
                    cells[col] = worksheet.Cell(row, col).GetString().Trim();
                }

                excelRows[extId] = cells;
            }

            return excelRows;
        }

        private async Task<UpdatePreviewResult> BuildUpdatePreviewAsync(Stream excelStream, List<SlotMapping> slotMappings, bool overwriteExistingData)
        {
            var excelRows = await BuildExcelRowsAsync(excelStream, slotMappings);
            var plan = await BuildUpdatePlanAsync(excelRows, slotMappings, overwriteExistingData);

            return new UpdatePreviewResult
            {
                InsertedCount = plan.InsertedCount,
                UpdatedCount = plan.UpdatedCount,
                SkippedExistingCount = plan.SkippedExistingCount,
                MissingCompanyCount = plan.MissingCompanyCount,
                MissingColumnCount = plan.MissingColumnCount,
                BlankExcelValueCount = plan.BlankExcelValueCount,
                PreviewRows = plan.PreviewRows.Take(25).ToList()
            };
        }

        private async Task<UpdatePlanResult> BuildUpdatePlanAsync(
            Dictionary<string, Dictionary<int, string>> excelRows,
            List<SlotMapping> slotMappings,
            bool overwriteExistingData)
        {
            var companySurveyIds = CompanySurveys.Select(x => x.CompanySurveyId).ToList();
            var questionIds = slotMappings.Select(x => x.QuestionId).Distinct().ToList();

            var existingAnswers = await _context.Answer
                .Where(a => companySurveyIds.Contains(a.CompanySurveyId) && questionIds.Contains(a.QuestionId))
                .OrderByDescending(a => a.Id)
                .ToListAsync();

            var latestAnswerLookup = existingAnswers
                .GroupBy(a => (a.CompanySurveyId, a.QuestionId))
                .ToDictionary(g => g.Key, g => g.First());

            var plan = new UpdatePlanResult();

            foreach (var companySurvey in CompanySurveys)
            {
                if (!excelRows.TryGetValue(companySurvey.ExternalId, out var excelCells))
                {
                    plan.MissingCompanyCount++;
                    continue;
                }

                var companyPreview = new UpdatePreviewRow
                {
                    CompanyName = companySurvey.CompanyName,
                    ExternalId = companySurvey.ExternalId
                };

                foreach (var slot in slotMappings)
                {
                    if (slot.ExcelColumnNumber < 0)
                    {
                        plan.MissingColumnCount++;
                        continue;
                    }

                    excelCells.TryGetValue(slot.ExcelColumnNumber, out var rawExcelValue);
                    rawExcelValue = rawExcelValue?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(rawExcelValue))
                    {
                        plan.BlankExcelValueCount++;
                        continue;
                    }

                    latestAnswerLookup.TryGetValue((companySurvey.CompanySurveyId, slot.QuestionId), out var existingAnswer);
                    if (existingAnswer != null && HasAnswerValue(existingAnswer) && !overwriteExistingData)
                    {
                        plan.SkippedExistingCount++;
                        companyPreview.SkippedCount++;
                        continue;
                    }

                    var parsedValue = ParseAnswerValue(slot.Question, rawExcelValue);
                    if (!parsedValue.HasValue)
                    {
                        plan.BlankExcelValueCount++;
                        continue;
                    }

                    plan.Operations.Add(new UpdateOperation
                    {
                        CompanySurveyId = companySurvey.CompanySurveyId,
                        QuestionId = slot.QuestionId,
                        ExistingAnswer = existingAnswer,
                        Value = parsedValue
                    });

                    if (existingAnswer == null)
                    {
                        plan.InsertedCount++;
                        companyPreview.InsertedCount++;
                    }
                    else
                    {
                        plan.UpdatedCount++;
                        companyPreview.UpdatedCount++;
                    }
                }

                if (companyPreview.InsertedCount > 0 || companyPreview.UpdatedCount > 0 || companyPreview.SkippedCount > 0)
                {
                    plan.PreviewRows.Add(companyPreview);
                }
            }

            return plan;
        }

        private void CleanupExpiredPendingPreviews()
        {
            var cutoff = DateTime.UtcNow.AddHours(-2);
            foreach (var pending in PendingUpdatePreviews.Values.Where(x => x.CreatedUtc < cutoff).ToList())
            {
                PendingUpdatePreviews.TryRemove(pending.Token, out _);
                TryDeleteFile(pending.TempFilePath);
            }
        }

        private static void TryDeleteFile(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            {
                return;
            }

            try
            {
                System.IO.File.Delete(path);
            }
            catch
            {
            }
        }

        private static bool HasAnswerValue(Answer? answer)
        {
            return answer != null
                && (!string.IsNullOrWhiteSpace(answer.AnswerText)
                    || answer.AnswerNumber.HasValue
                    || answer.AnswerCurrency.HasValue);
        }

        private static ParsedAnswerValue ParseAnswerValue(Question question, string rawValue)
        {
            var trimmed = rawValue?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return ParsedAnswerValue.Empty;
            }

            var answerType = question.AnswerType?.Trim() ?? string.Empty;
            if (answerType.Equals("Number", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(trimmed, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var numberValue)
                    || double.TryParse(trimmed, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out numberValue))
                {
                    return ParsedAnswerValue.ForNumber(numberValue);
                }

                return ParsedAnswerValue.ForText(trimmed);
            }

            if (answerType.Equals("Currency", StringComparison.OrdinalIgnoreCase))
            {
                if (decimal.TryParse(trimmed, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var currencyValue)
                    || decimal.TryParse(trimmed, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out currencyValue))
                {
                    return ParsedAnswerValue.ForCurrency(currencyValue);
                }

                return ParsedAnswerValue.ForText(trimmed);
            }

            return ParsedAnswerValue.ForText(trimmed);
        }

        private static void ApplyParsedValue(Answer answer, ParsedAnswerValue parsedValue)
        {
            answer.AnswerText = parsedValue.AnswerText;
            answer.AnswerNumber = parsedValue.AnswerNumber;
            answer.AnswerCurrency = parsedValue.AnswerCurrency;
        }

        // ── Inner types ───────────────────────────────────────────────────────────

        private sealed class SlotMapping
        {
            public int QuestionId { get; set; }
            public Question Question { get; set; } = null!;
            public string? ExcelColumnName { get; set; }
            public int ExcelColumnNumber { get; set; } = -1;
        }

        private sealed class ParsedAnswerValue
        {
            public static ParsedAnswerValue Empty { get; } = new ParsedAnswerValue();

            public string? AnswerText { get; private set; }
            public double? AnswerNumber { get; private set; }
            public decimal? AnswerCurrency { get; private set; }

            public bool HasValue => !string.IsNullOrWhiteSpace(AnswerText) || AnswerNumber.HasValue || AnswerCurrency.HasValue;

            public static ParsedAnswerValue ForText(string value) => new ParsedAnswerValue { AnswerText = value };
            public static ParsedAnswerValue ForNumber(double value) => new ParsedAnswerValue { AnswerNumber = value };
            public static ParsedAnswerValue ForCurrency(decimal value) => new ParsedAnswerValue { AnswerCurrency = value };
        }

        private sealed class UpdateOperation
        {
            public int CompanySurveyId { get; set; }
            public int QuestionId { get; set; }
            public Answer? ExistingAnswer { get; set; }
            public ParsedAnswerValue Value { get; set; } = ParsedAnswerValue.Empty;
        }

        private sealed class UpdatePlanResult
        {
            public List<UpdateOperation> Operations { get; } = new();
            public List<UpdatePreviewRow> PreviewRows { get; } = new();
            public int InsertedCount { get; set; }
            public int UpdatedCount { get; set; }
            public int SkippedExistingCount { get; set; }
            public int MissingCompanyCount { get; set; }
            public int MissingColumnCount { get; set; }
            public int BlankExcelValueCount { get; set; }
        }

        private sealed class PendingUpdatePreview
        {
            public string Token { get; set; } = string.Empty;
            public string TempFilePath { get; set; } = string.Empty;
            public DateTime CreatedUtc { get; set; }
            public bool OverwriteExistingData { get; set; }
        }

        public class QuestionOption
        {
            public int Id { get; set; }
            public string TitleLabel { get; set; } = string.Empty;
            public string? ImportColumnName { get; set; }
        }

        public class QuestionHeader
        {
            public string Title { get; set; } = string.Empty;
            public string? ExcelColumnName { get; set; }
        }

        public class UpdatePreviewResult
        {
            public int InsertedCount { get; set; }
            public int UpdatedCount { get; set; }
            public int SkippedExistingCount { get; set; }
            public int MissingCompanyCount { get; set; }
            public int MissingColumnCount { get; set; }
            public int BlankExcelValueCount { get; set; }
            public List<UpdatePreviewRow> PreviewRows { get; set; } = new();
        }

        public class UpdatePreviewRow
        {
            public string CompanyName { get; set; } = string.Empty;
            public string ExternalId { get; set; } = string.Empty;
            public int InsertedCount { get; set; }
            public int UpdatedCount { get; set; }
            public int SkippedCount { get; set; }
        }

        public class CompanySurveyRow
        {
            public int CompanySurveyId { get; set; }
            public string CompanyName { get; set; } = string.Empty;
            public string ExternalId { get; set; } = string.Empty;
            public string? SurveyLink { get; set; }
        }

        public class CheckResultRow
        {
            public string CompanyName { get; set; } = string.Empty;
            public string ExternalId { get; set; } = string.Empty;
            public string? SurveyLink { get; set; }
            public CheckStatus RowStatus { get; set; }
            public List<QuestionCheckResult> QuestionResults { get; set; } = new();
        }

        public class QuestionCheckResult
        {
            public string QuestionTitle { get; set; } = string.Empty;
            public string DbValue { get; set; } = string.Empty;
            public string ExcelValue { get; set; } = string.Empty;
            public CheckStatus Status { get; set; }
        }

        public enum CheckStatus { Match, Mismatch, NoExcelMatch, ColumnNotFound }
    }
}
