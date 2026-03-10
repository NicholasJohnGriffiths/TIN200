using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using TINWeb.Data;
using TINWeb.Models;

namespace TINWeb.Services
{
    public class AnswerService
    {
        private readonly ApplicationDbContext _context;

        public AnswerService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<int>> GetAvailableFinancialYearsAsync()
        {
            return await _context.Survey
            .Select(s => s.FinancialYear)
            .Distinct()
            .OrderByDescending(year => year)
            .ToListAsync();
        }

        public async Task<int> GetQuestionCountAsync()
        {
            return await _context.Question.CountAsync();
        }

        public async Task<int?> GetCurrentSurveyFinancialYearAsync()
        {
            return await _context.Survey
                .Where(s => s.CurrentSurvey)
                .Select(s => (int?)s.FinancialYear)
                .OrderByDescending(y => y)
                .FirstOrDefaultAsync();
        }

        public async Task<List<CompanySurveyOption>> GetCompanySurveyOptionsAsync(int? financialYear)
        {
            var query =
                from companySurvey in _context.CompanySurvey
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                join company in _context.Tin200 on companySurvey.CompanyId equals company.Id
                join answer in _context.Answer on companySurvey.Id equals answer.CompanySurveyId into answerJoin
                select new CompanySurveyOption
                {
                    CompanySurveyId = companySurvey.Id,
                    CompanyId = company.Id,
                    CompanyName = company.CompanyName,
                    FinancialYear = survey.FinancialYear,
                    AnswerCount = answerJoin.Select(a => a.QuestionId).Distinct().Count(),
                    Saved = companySurvey.Saved,
                    Submitted = companySurvey.Submitted,
                    Requested = companySurvey.Requested,
                    SavedDate = companySurvey.SavedDate,
                    SubmittedDate = companySurvey.SubmittedDate,
                    RequestedDate = companySurvey.RequestedDate
                };

            if (financialYear.HasValue)
            {
                query = query.Where(x => x.FinancialYear == financialYear.Value);
            }

            return await query
                .OrderBy(x => x.CompanyName)
                .ThenByDescending(x => x.FinancialYear)
                .ToListAsync();
        }

        public async Task<List<AnswerListRow>> GetAnswerRowsAsync(int companySurveyId)
        {
            var context = await (
                from companySurvey in _context.CompanySurvey
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                join company in _context.Tin200 on companySurvey.CompanyId equals company.Id
                where companySurvey.Id == companySurveyId
                select new
                {
                    companySurvey.Id,
                    CompanyId = company.Id,
                    CompanyName = company.CompanyName,
                    survey.FinancialYear
                }
            ).FirstOrDefaultAsync();

            if (context == null)
            {
                return new List<AnswerListRow>();
            }

            var latestAnswerIds = _context.Answer
                .Where(a => a.CompanySurveyId == companySurveyId)
                .GroupBy(a => a.QuestionId)
                .Select(g => g.Max(a => a.Id));

            var latestAnswers = _context.Answer.Where(a => latestAnswerIds.Contains(a.Id));

            var query =
                from question in _context.Question
                join answer in latestAnswers on question.Id equals answer.QuestionId into answerJoin
                from answer in answerJoin.DefaultIfEmpty()
                orderby question.OrderNumber, question.Id
                select new AnswerListRow
                {
                    Id = answer != null ? answer.Id : 0,
                    CompanySurveyId = companySurveyId,
                    CompanyId = context.CompanyId,
                    CompanyName = context.CompanyName,
                    FinancialYear = context.FinancialYear,
                    QuestionId = question.Id,
                    QuestionOrderNumber = question.OrderNumber,
                    QuestionText = question.QuestionText,
                    AnswerText = answer != null ? answer.AnswerText : null,
                    AnswerNumber = answer != null ? answer.AnswerNumber : null,
                    AnswerCurrency = answer != null ? answer.AnswerCurrency : null
                };

            return await query.ToListAsync();
        }

        public async Task<List<AnswerExportRow>> GetAnswerExportRowsAsync(int? financialYear)
        {
            var query =
                from answer in _context.Answer
                join companySurvey in _context.CompanySurvey on answer.CompanySurveyId equals companySurvey.Id
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                join company in _context.Tin200 on companySurvey.CompanyId equals company.Id
                join question in _context.Question on answer.QuestionId equals question.Id
                select new AnswerExportRow
                {
                    CompanyExternalId = company.ExternalId,
                    CompanyId = company.Id,
                    CompanyName = company.CompanyName,
                    CompanyEmail = company.Email,
                    AnswerId = answer.Id,
                    CompanySurveyId = answer.CompanySurveyId,
                    QuestionId = answer.QuestionId,
                    QuestionTitle = question.Title,
                    AnswerText = answer.AnswerText,
                    AnswerCurrency = answer.AnswerCurrency,
                    AnswerNumber = answer.AnswerNumber,
                    FinancialYear = survey.FinancialYear
                };

            if (financialYear.HasValue)
            {
                query = query.Where(x => x.FinancialYear == financialYear.Value);
            }

            return await query
                .OrderBy(x => x.CompanyName)
                .ThenBy(x => x.AnswerId)
                .ToListAsync();
        }

        public async Task<AnswerEditRow?> GetAnswerForEditAsync(int answerId)
        {
            var row = await (
                from answer in _context.Answer
                join companySurvey in _context.CompanySurvey on answer.CompanySurveyId equals companySurvey.Id
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                join company in _context.Tin200 on companySurvey.CompanyId equals company.Id
                join question in _context.Question on answer.QuestionId equals question.Id
                where answer.Id == answerId
                select new AnswerEditRow
                {
                    Id = answer.Id,
                    CompanySurveyId = answer.CompanySurveyId,
                    QuestionId = answer.QuestionId,
                    CompanyId = company.Id,
                    CompanyName = company.CompanyName,
                    FinancialYear = survey.FinancialYear,
                    QuestionText = question.QuestionText,
                    AnswerType = question.AnswerType,
                    Multi1 = question.Multi1,
                    Multi2 = question.Multi2,
                    Multi3 = question.Multi3,
                    Multi4 = question.Multi4,
                    Multi5 = question.Multi5,
                    Multi6 = question.Multi6,
                    Multi7 = question.Multi7,
                    Multi8 = question.Multi8,
                    AnswerText = answer.AnswerText,
                    AnswerNumber = answer.AnswerNumber,
                    AnswerCurrency = answer.AnswerCurrency
                }
            ).FirstOrDefaultAsync();

            if (row == null)
            {
                return null;
            }

            row.ChoiceOptions = new[]
            {
                row.Multi1,
                row.Multi2,
                row.Multi3,
                row.Multi4,
                row.Multi5,
                row.Multi6,
                row.Multi7,
                row.Multi8
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

            return row;
        }

        public async Task<bool> UpdateAnswerAsync(AnswerEditInput input)
        {
            var answer = await _context.Answer.FirstOrDefaultAsync(x => x.Id == input.Id);
            if (answer == null)
            {
                return false;
            }

            var question = await _context.Question.FirstOrDefaultAsync(q => q.Id == answer.QuestionId);
            var answerType = question?.AnswerType?.Trim() ?? string.Empty;

            var choiceOptions = question == null
                ? new List<string>()
                : new[]
                {
                    question.Multi1,
                    question.Multi2,
                    question.Multi3,
                    question.Multi4,
                    question.Multi5,
                    question.Multi6,
                    question.Multi7,
                    question.Multi8
                }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .Distinct()
                .ToList();

            if (answerType.Equals("Number", StringComparison.OrdinalIgnoreCase))
            {
                answer.AnswerText = null;
                answer.AnswerCurrency = null;
                answer.AnswerNumber = input.AnswerNumber;
            }
            else if (answerType.Equals("Currency", StringComparison.OrdinalIgnoreCase))
            {
                answer.AnswerText = null;
                answer.AnswerNumber = null;
                answer.AnswerCurrency = input.AnswerCurrency;
            }
            else if (answerType.Equals("SingleChoice", StringComparison.OrdinalIgnoreCase))
            {
                answer.AnswerText = choiceOptions.Contains(input.AnswerText ?? string.Empty)
                    ? input.AnswerText
                    : null;
                answer.AnswerNumber = null;
                answer.AnswerCurrency = null;
            }
            else if (answerType.Equals("Multichoice", StringComparison.OrdinalIgnoreCase) || answerType.Equals("MultiChoice", StringComparison.OrdinalIgnoreCase))
            {
                var selected = (input.SelectedChoices ?? new List<string>())
                    .Select(value => (value ?? string.Empty).Trim())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Where(value => choiceOptions.Contains(value))
                    .Distinct()
                    .ToList();

                answer.AnswerText = selected.Count == 0 ? null : string.Join("; ", selected);
                answer.AnswerNumber = null;
                answer.AnswerCurrency = null;
            }
            else
            {
                answer.AnswerText = input.AnswerText;
                answer.AnswerNumber = null;
                answer.AnswerCurrency = null;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<int> CreateMissingAnswersForYearAsync(int financialYear)
        {
            var surveyId = await _context.Survey
                .Where(s => s.FinancialYear == financialYear)
                .OrderByDescending(s => s.CurrentSurvey)
                .ThenByDescending(s => s.Id)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync();

            if (!surveyId.HasValue)
            {
                return 0;
            }

            var companyIds = await _context.Tin200
                .Select(c => c.Id)
                .ToListAsync();

            var existingCompanySurveyCompanyIds = await _context.CompanySurvey
                .Where(cs => cs.SurveyId == surveyId.Value)
                .Select(cs => cs.CompanyId)
                .Distinct()
                .ToListAsync();

            var missingCompanySurveyCompanyIds = companyIds
                .Except(existingCompanySurveyCompanyIds)
                .ToList();

            foreach (var companyId in missingCompanySurveyCompanyIds)
            {
                _context.CompanySurvey.Add(new CompanySurvey
                {
                    CompanyId = companyId,
                    SurveyId = surveyId.Value,
                    Saved = false,
                    Submitted = false,
                    Requested = false,
                    SavedDate = null,
                    SubmittedDate = null,
                    RequestedDate = null
                });
            }

            if (missingCompanySurveyCompanyIds.Any())
            {
                await _context.SaveChangesAsync();
            }

            var companySurveyIds = await _context.CompanySurvey
                .Where(cs => cs.SurveyId == surveyId.Value)
                .Select(cs => cs.Id)
                .Distinct()
                .ToListAsync();

            if (!companySurveyIds.Any())
            {
                return 0;
            }

            var questionIds = await _context.Question
                .Select(q => q.Id)
                .ToListAsync();

            if (!questionIds.Any())
            {
                return 0;
            }

            var existingPairs = await _context.Answer
                .Where(a => companySurveyIds.Contains(a.CompanySurveyId))
                .Select(a => new { a.CompanySurveyId, a.QuestionId })
                .Distinct()
                .ToListAsync();

            var existingSet = new HashSet<(int CompanySurveyId, int QuestionId)>(
                existingPairs.Select(x => (x.CompanySurveyId, x.QuestionId)));

            var rowsToInsert = new List<Answer>();

            foreach (var companySurveyId in companySurveyIds)
            {
                foreach (var questionId in questionIds)
                {
                    if (existingSet.Contains((companySurveyId, questionId)))
                    {
                        continue;
                    }

                    rowsToInsert.Add(new Answer
                    {
                        CompanySurveyId = companySurveyId,
                        QuestionId = questionId
                    });
                }
            }

            if (!rowsToInsert.Any())
            {
                return 0;
            }

            _context.Answer.AddRange(rowsToInsert);
            await _context.SaveChangesAsync();
            return rowsToInsert.Count;
        }

        public async Task RecreateAnswerTableAsync()
        {
            const string sql = @"
IF OBJECT_ID(N'[dbo].[Answer]', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[Answer];
END;

CREATE TABLE [dbo].[Answer](
    [Id] [int] IDENTITY(1,1) NOT NULL,
    [CompanySurveyId] [int] NOT NULL,
    [QuestionId] [int] NOT NULL,
    [AnswerText] [varchar](max) NULL,
    [AnswerCurrency] [money] NULL,
    [AnswerNumber] [float] NULL,
 CONSTRAINT [PK_Answer] PRIMARY KEY CLUSTERED 
(
    [Id] ASC
) WITH (
    PAD_INDEX = OFF,
    STATISTICS_NORECOMPUTE = OFF,
    IGNORE_DUP_KEY = OFF,
    ALLOW_ROW_LOCKS = ON,
    ALLOW_PAGE_LOCKS = ON,
    OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF
) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY];

ALTER TABLE [dbo].[Answer] WITH CHECK ADD CONSTRAINT [FK_Answer_CompanySurvey]
FOREIGN KEY([CompanySurveyId]) REFERENCES [dbo].[CompanySurvey] ([Id]);

ALTER TABLE [dbo].[Answer] CHECK CONSTRAINT [FK_Answer_CompanySurvey];

ALTER TABLE [dbo].[Answer] WITH CHECK ADD CONSTRAINT [FK_Answer_Question]
FOREIGN KEY([QuestionId]) REFERENCES [dbo].[Question] ([Id]);

ALTER TABLE [dbo].[Answer] CHECK CONSTRAINT [FK_Answer_Question];
";

            await _context.Database.ExecuteSqlRawAsync(sql);
        }

        public async Task<AnswerImportPreviewResult> PreviewAnswersImportFromExcelAsync(Stream excelStream, int financialYear)
        {
            var plan = await BuildImportPlanAsync(excelStream, financialYear);
            return new AnswerImportPreviewResult
            {
                FinancialYear = financialYear,
                InsertedCount = plan.InsertedCount,
                UpdatedCount = plan.UpdatedCount,
                MatchedExternalIds = plan.MatchedExternalIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                UnmatchedExternalIds = plan.UnmatchedExternalIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                Errors = plan.Errors.ToList()
            };
        }

        public async Task<AnswerImportResult> ImportAnswersFromExcelAsync(Stream excelStream, int financialYear)
        {
            var result = new AnswerImportResult();
            using var bufferedStream = new MemoryStream();
            await excelStream.CopyToAsync(bufferedStream);
            bufferedStream.Position = 0;

            var plan = await BuildImportPlanAsync(bufferedStream, financialYear);

            if (plan.UnmatchedExternalIds.Any())
            {
                await EnsureCompanyAndCompanySurveyRecordsAsync(plan.UnmatchedExternalIds, financialYear);
                bufferedStream.Position = 0;
                plan = await BuildImportPlanAsync(bufferedStream, financialYear);
            }

            foreach (var error in plan.Errors)
            {
                result.Errors.Add(error);
            }

            var updateIds = plan.Operations
                .Where(o => o.ExistingAnswerId.HasValue)
                .Select(o => o.ExistingAnswerId!.Value)
                .Distinct()
                .ToList();

            var existingById = await _context.Answer
                .Where(a => updateIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id);

            foreach (var operation in plan.Operations)
            {
                if (operation.ExistingAnswerId.HasValue)
                {
                    if (!existingById.TryGetValue(operation.ExistingAnswerId.Value, out var existingAnswer))
                    {
                        result.Errors.Add($"Unable to find existing answer Id {operation.ExistingAnswerId.Value} during apply.");
                        continue;
                    }

                    existingAnswer.AnswerText = operation.Value.AnswerText;
                    existingAnswer.AnswerNumber = operation.Value.AnswerNumber;
                    existingAnswer.AnswerCurrency = operation.Value.AnswerCurrency;
                }
                else
                {
                    _context.Answer.Add(new Answer
                    {
                        CompanySurveyId = operation.CompanySurveyId,
                        QuestionId = operation.QuestionId,
                        AnswerText = operation.Value.AnswerText,
                        AnswerNumber = operation.Value.AnswerNumber,
                        AnswerCurrency = operation.Value.AnswerCurrency
                    });
                }
            }

            await _context.SaveChangesAsync();
            result.InsertedCount = plan.InsertedCount;
            result.UpdatedCount = plan.UpdatedCount;

            return result;
        }

        private async Task EnsureCompanyAndCompanySurveyRecordsAsync(IEnumerable<string> externalIds, int financialYear)
        {
            var normalizedExternalIds = externalIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!normalizedExternalIds.Any())
            {
                return;
            }

            var surveyId = await _context.Survey
                .Where(s => s.FinancialYear == financialYear)
                .OrderByDescending(s => s.CurrentSurvey)
                .ThenByDescending(s => s.Id)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync();

            if (!surveyId.HasValue)
            {
                throw new InvalidOperationException($"No Survey record exists for financial year {financialYear}. Cannot create CompanySurvey rows for import.");
            }

            var existingCompanyRows = await _context.Tin200
                .Where(c => c.ExternalId != null && normalizedExternalIds.Contains(c.ExternalId))
                .Select(c => new { c.Id, c.ExternalId })
                .ToListAsync();

            var companyIdByExternalId = existingCompanyRows
                .Where(c => !string.IsNullOrWhiteSpace(c.ExternalId))
                .GroupBy(c => c.ExternalId!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.Id).First().Id, StringComparer.OrdinalIgnoreCase);

            var missingExternalIds = normalizedExternalIds
                .Where(eid => !companyIdByExternalId.ContainsKey(eid))
                .ToList();

            foreach (var externalId in missingExternalIds)
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO [Company] ([ExternalID], [CompanyName]) VALUES ({externalId}, {externalId})");
            }

            if (missingExternalIds.Any())
            {
                var insertedCompanyRows = await _context.Tin200
                    .Where(c => c.ExternalId != null && missingExternalIds.Contains(c.ExternalId))
                    .Select(c => new { c.Id, c.ExternalId })
                    .ToListAsync();

                foreach (var row in insertedCompanyRows)
                {
                    if (!string.IsNullOrWhiteSpace(row.ExternalId))
                    {
                        companyIdByExternalId[row.ExternalId.Trim()] = row.Id;
                    }
                }
            }

            var companyIds = companyIdByExternalId.Values.Distinct().ToList();

            var existingCompanySurveyCompanyIds = await _context.CompanySurvey
                .Where(cs => cs.SurveyId == surveyId.Value && companyIds.Contains(cs.CompanyId))
                .Select(cs => cs.CompanyId)
                .Distinct()
                .ToListAsync();

            var companyIdsToCreateSurvey = companyIds
                .Except(existingCompanySurveyCompanyIds)
                .ToList();

            foreach (var companyId in companyIdsToCreateSurvey)
            {
                _context.CompanySurvey.Add(new CompanySurvey
                {
                    CompanyId = companyId,
                    SurveyId = surveyId.Value,
                    Saved = false,
                    Submitted = false,
                    Requested = false,
                    SavedDate = null,
                    SubmittedDate = null,
                    RequestedDate = null
                });
            }

            if (companyIdsToCreateSurvey.Any())
            {
                await _context.SaveChangesAsync();
            }
        }

        private async Task<ImportPlanResult> BuildImportPlanAsync(Stream excelStream, int financialYear)
        {
            var plan = new ImportPlanResult();

            using var workbook = new XLWorkbook(excelStream);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
            {
                plan.Errors.Add("The Excel workbook does not contain any worksheets.");
                return plan;
            }

            var firstRow = worksheet.FirstRowUsed();
            if (firstRow == null)
            {
                plan.Errors.Add("The worksheet is empty.");
                return plan;
            }

            var lastColumnNumber = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
            var columnByHeader = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var column = 1; column <= lastColumnNumber; column++)
            {
                var header = firstRow.Cell(column).GetString().Trim();
                if (string.IsNullOrWhiteSpace(header))
                {
                    continue;
                }

                if (!columnByHeader.ContainsKey(header))
                {
                    columnByHeader.Add(header, column);
                }
            }

            if (!columnByHeader.TryGetValue("External ID", out var externalIdColumn))
            {
                plan.Errors.Add("Missing required column header: External ID.");
                return plan;
            }

            var questions = await _context.Question
                .Where(q => !string.IsNullOrWhiteSpace(q.ImportColumnName))
                .OrderBy(q => q.OrderNumber)
                .ThenBy(q => q.Id)
                .ToListAsync();

            if (!questions.Any())
            {
                plan.Errors.Add("No questions have ImportColumnName configured.");
                return plan;
            }

            var mappedQuestions = questions
                .Where(q => columnByHeader.ContainsKey(q.ImportColumnName!.Trim()))
                .ToList();

            if (!mappedQuestions.Any())
            {
                plan.Errors.Add("No question ImportColumnName values match any Excel column headers.");
                return plan;
            }

            var companySurveyRows = await (
                from companySurvey in _context.CompanySurvey
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                join company in _context.Tin200 on companySurvey.CompanyId equals company.Id
                where survey.FinancialYear == financialYear && company.ExternalId != null
                select new
                {
                    companySurvey.Id,
                    company.ExternalId
                }
            )
            .ToListAsync();

            var companySurveyByExternalId = companySurveyRows
                .Where(x => !string.IsNullOrWhiteSpace(x.ExternalId))
                .GroupBy(x => x.ExternalId!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.Id).First().Id,
                    StringComparer.OrdinalIgnoreCase);

            if (!companySurveyByExternalId.Any())
            {
                plan.Errors.Add($"No CompanySurvey records found for financial year {financialYear} with matching External ID values.");
                return plan;
            }

            var companySurveyIds = companySurveyByExternalId.Values.Distinct().ToList();
            var questionIds = mappedQuestions.Select(q => q.Id).ToList();

            var existingAnswers = await _context.Answer
                .Where(a => companySurveyIds.Contains(a.CompanySurveyId) && questionIds.Contains(a.QuestionId))
                .OrderByDescending(a => a.Id)
                .ToListAsync();

            var latestByKey = new Dictionary<(int CompanySurveyId, int QuestionId), Answer>();
            foreach (var answer in existingAnswers)
            {
                var key = (answer.CompanySurveyId, answer.QuestionId);
                if (!latestByKey.ContainsKey(key))
                {
                    latestByKey[key] = answer;
                }
            }

            var operationsByKey = new Dictionary<(int CompanySurveyId, int QuestionId), ImportAnswerOperation>();

            var firstDataRowNumber = firstRow.RowNumber() + 1;
            var lastRowNumber = worksheet.LastRowUsed()?.RowNumber() ?? firstDataRowNumber - 1;

            for (var rowNumber = firstDataRowNumber; rowNumber <= lastRowNumber; rowNumber++)
            {
                var row = worksheet.Row(rowNumber);
                var externalId = row.Cell(externalIdColumn).GetString().Trim();
                if (string.IsNullOrWhiteSpace(externalId))
                {
                    continue;
                }

                if (!companySurveyByExternalId.TryGetValue(externalId, out var companySurveyId))
                {
                    plan.UnmatchedExternalIds.Add(externalId);
                    continue;
                }

                plan.MatchedExternalIds.Add(externalId);

                foreach (var question in mappedQuestions)
                {
                    var importColumnName = question.ImportColumnName!.Trim();
                    var columnNumber = columnByHeader[importColumnName];
                    var cell = row.Cell(columnNumber);
                    var textValue = cell.GetString().Trim();
                    var key = (companySurveyId, question.Id);

                    latestByKey.TryGetValue(key, out var existingAnswer);

                    var hasAnyValue = !cell.IsEmpty() || !string.IsNullOrWhiteSpace(textValue);
                    if (!hasAnyValue)
                    {
                        if (existingAnswer == null && !operationsByKey.ContainsKey(key))
                        {
                            operationsByKey[key] = new ImportAnswerOperation
                            {
                                CompanySurveyId = companySurveyId,
                                QuestionId = question.Id,
                                ExistingAnswerId = null,
                                Value = new ParsedAnswerValue()
                            };
                            plan.InsertedCount++;
                        }

                        continue;
                    }

                    var parsedValue = BuildAnswerValue(question.AnswerType, cell);
                    if (parsedValue == null)
                    {
                        plan.Errors.Add($"Row {rowNumber}, column '{importColumnName}': unable to parse value '{textValue}' for question type '{question.AnswerType}'.");

                        if (existingAnswer == null && !operationsByKey.ContainsKey(key))
                        {
                            operationsByKey[key] = new ImportAnswerOperation
                            {
                                CompanySurveyId = companySurveyId,
                                QuestionId = question.Id,
                                ExistingAnswerId = null,
                                Value = new ParsedAnswerValue()
                            };
                            plan.InsertedCount++;
                        }

                        continue;
                    }

                    if (!operationsByKey.TryGetValue(key, out var operation))
                    {
                        operation = new ImportAnswerOperation
                        {
                            CompanySurveyId = companySurveyId,
                            QuestionId = question.Id,
                            ExistingAnswerId = existingAnswer?.Id,
                            Value = parsedValue ?? new ParsedAnswerValue()
                        };
                        operationsByKey[key] = operation;

                        if (existingAnswer != null)
                        {
                            plan.UpdatedCount++;
                        }
                        else
                        {
                            plan.InsertedCount++;
                        }
                    }
                    else
                    {
                        operation.Value = parsedValue ?? new ParsedAnswerValue();
                    }
                }
            }

            plan.Operations = operationsByKey.Values.ToList();
            return plan;
        }

        private static ParsedAnswerValue? BuildAnswerValue(string? answerType, IXLCell cell)
        {
            var rawValue = cell.GetString().Trim();
            var normalizedType = answerType?.Trim();

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return new ParsedAnswerValue();
            }

            if (string.Equals(normalizedType, "Number", StringComparison.OrdinalIgnoreCase))
            {
                if (cell.DataType == XLDataType.Number && cell.TryGetValue<double>(out var numericValue))
                {
                    return new ParsedAnswerValue { AnswerNumber = numericValue };
                }

                if (double.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsedDouble)
                    || double.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out parsedDouble))
                {
                    return new ParsedAnswerValue { AnswerNumber = parsedDouble };
                }

                return null;
            }

            if (string.Equals(normalizedType, "Currency", StringComparison.OrdinalIgnoreCase))
            {
                if (cell.DataType == XLDataType.Number && cell.TryGetValue<double>(out var numericValue))
                {
                    return new ParsedAnswerValue { AnswerCurrency = Convert.ToDecimal(numericValue) };
                }

                if (decimal.TryParse(rawValue, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.InvariantCulture, out var parsedDecimal)
                    || decimal.TryParse(rawValue, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.CurrentCulture, out parsedDecimal))
                {
                    return new ParsedAnswerValue { AnswerCurrency = parsedDecimal };
                }

                return null;
            }

            return new ParsedAnswerValue { AnswerText = rawValue };
        }

        private sealed class ParsedAnswerValue
        {
            public string? AnswerText { get; set; }
            public double? AnswerNumber { get; set; }
            public decimal? AnswerCurrency { get; set; }
        }

        private sealed class ImportAnswerOperation
        {
            public int CompanySurveyId { get; set; }
            public int QuestionId { get; set; }
            public int? ExistingAnswerId { get; set; }
            public ParsedAnswerValue Value { get; set; } = new();
        }

        private sealed class ImportPlanResult
        {
            public int InsertedCount { get; set; }
            public int UpdatedCount { get; set; }
            public HashSet<string> MatchedExternalIds { get; } = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> UnmatchedExternalIds { get; } = new(StringComparer.OrdinalIgnoreCase);
            public List<string> Errors { get; } = new();
            public List<ImportAnswerOperation> Operations { get; set; } = new();
        }

        public class AnswerImportResult
        {
            public int InsertedCount { get; set; }
            public int UpdatedCount { get; set; }
            public List<string> Errors { get; } = new();
        }

        public class AnswerImportPreviewResult
        {
            public int FinancialYear { get; set; }
            public int InsertedCount { get; set; }
            public int UpdatedCount { get; set; }
            public List<string> MatchedExternalIds { get; set; } = new();
            public List<string> UnmatchedExternalIds { get; set; } = new();
            public List<string> Errors { get; set; } = new();
        }

        public class AnswerListRow
        {
            public int Id { get; set; }
            public int CompanySurveyId { get; set; }
            public int CompanyId { get; set; }
            public string? CompanyName { get; set; }
            public int FinancialYear { get; set; }
            public int QuestionId { get; set; }
            public int? QuestionOrderNumber { get; set; }
            public string? QuestionText { get; set; }
            public string? AnswerText { get; set; }
            public double? AnswerNumber { get; set; }
            public decimal? AnswerCurrency { get; set; }
        }

        public class AnswerEditRow
        {
            public int Id { get; set; }
            public int CompanySurveyId { get; set; }
            public int QuestionId { get; set; }
            public int CompanyId { get; set; }
            public string? CompanyName { get; set; }
            public int FinancialYear { get; set; }
            public string? QuestionText { get; set; }
            public string? AnswerType { get; set; }
            public string? Multi1 { get; set; }
            public string? Multi2 { get; set; }
            public string? Multi3 { get; set; }
            public string? Multi4 { get; set; }
            public string? Multi5 { get; set; }
            public string? Multi6 { get; set; }
            public string? Multi7 { get; set; }
            public string? Multi8 { get; set; }
            public List<string> ChoiceOptions { get; set; } = new();
            public string? AnswerText { get; set; }
            public double? AnswerNumber { get; set; }
            public decimal? AnswerCurrency { get; set; }
        }

        public class AnswerEditInput
        {
            public int Id { get; set; }
            public int CompanySurveyId { get; set; }
            public string? AnswerType { get; set; }
            public List<string> ChoiceOptions { get; set; } = new();
            public List<string> SelectedChoices { get; set; } = new();
            public string? AnswerText { get; set; }
            public double? AnswerNumber { get; set; }
            public decimal? AnswerCurrency { get; set; }
        }

        public class AnswerExportRow
        {
            public string? CompanyExternalId { get; set; }
            public int CompanyId { get; set; }
            public string? CompanyName { get; set; }
            public string? CompanyEmail { get; set; }
            public int AnswerId { get; set; }
            public int CompanySurveyId { get; set; }
            public int QuestionId { get; set; }
            public string? QuestionTitle { get; set; }
            public string? AnswerText { get; set; }
            public decimal? AnswerCurrency { get; set; }
            public double? AnswerNumber { get; set; }
            public int FinancialYear { get; set; }
        }

        public class CompanySurveyOption
        {
            public int CompanySurveyId { get; set; }
            public int CompanyId { get; set; }
            public string? CompanyName { get; set; }
            public int FinancialYear { get; set; }
            public int AnswerCount { get; set; }
            public bool Saved { get; set; }
            public bool Submitted { get; set; }
            public bool Requested { get; set; }
            public DateTime? SavedDate { get; set; }
            public DateTime? SubmittedDate { get; set; }
            public DateTime? RequestedDate { get; set; }
        }
    }
}
