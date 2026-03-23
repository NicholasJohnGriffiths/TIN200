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
                .AsNoTracking()
                .Select(s => s.FinancialYear)
                .Distinct()
                .OrderByDescending(year => year)
                .ToListAsync();
        }

        public async Task<int> GetQuestionCountAsync()
        {
            return await _context.Question
                .AsNoTracking()
                .CountAsync();
        }

        public async Task<int?> GetCurrentSurveyFinancialYearAsync()
        {
            return await _context.Survey
                .AsNoTracking()
                .Where(s => s.CurrentSurvey)
                .Select(s => (int?)s.FinancialYear)
                .OrderByDescending(y => y)
                .FirstOrDefaultAsync();
        }

        public async Task<List<CompanySurveyOption>> GetCompanySurveyOptionsAsync(int? financialYear)
        {
            var answerCounts =
                from answer in _context.Answer.AsNoTracking()
                group answer by answer.CompanySurveyId into answerGroup
                select new
                {
                    CompanySurveyId = answerGroup.Key,
                    AnswerCount = answerGroup.Select(a => a.QuestionId).Distinct().Count()
                };

            var query =
                from companySurvey in _context.CompanySurvey.AsNoTracking()
                join survey in _context.Survey.AsNoTracking() on companySurvey.SurveyId equals survey.Id
                join company in _context.Tin200.AsNoTracking() on companySurvey.CompanyId equals company.Id
                join answerCount in answerCounts on companySurvey.Id equals answerCount.CompanySurveyId into answerCountJoin
                from answerCount in answerCountJoin.DefaultIfEmpty()
                select new CompanySurveyOption
                {
                    CompanySurveyId = companySurvey.Id,
                    CompanyId = company.Id,
                    CompanyName = company.CompanyName,
                    FinancialYear = survey.FinancialYear,
                    AnswerCount = answerCount != null ? answerCount.AnswerCount : 0,
                    Saved = companySurvey.Saved,
                    Submitted = companySurvey.Submitted,
                    Requested = companySurvey.Requested,
                    Estimate = companySurvey.Estimate == true,
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
                from companySurvey in _context.CompanySurvey.AsNoTracking()
                join survey in _context.Survey.AsNoTracking() on companySurvey.SurveyId equals survey.Id
                join company in _context.Tin200.AsNoTracking() on companySurvey.CompanyId equals company.Id
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
                .AsNoTracking()
                .Where(a => a.CompanySurveyId == companySurveyId)
                .GroupBy(a => a.QuestionId)
                .Select(g => g.Max(a => a.Id));

            var latestAnswers = _context.Answer
                .AsNoTracking()
                .Where(a => latestAnswerIds.Contains(a.Id));

            var query =
                from question in _context.Question.AsNoTracking()
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
                    AnswerType = question.AnswerType,
                    AnswerText = answer != null ? answer.AnswerText : null,
                    AnswerNumber = answer != null ? answer.AnswerNumber : null,
                    AnswerCurrency = answer != null ? answer.AnswerCurrency : null
                };

            return await query.ToListAsync();
        }

        public async Task<CompanySurveyHistoryResult?> GetCompanySurveyHistoryAsync(int companyId)
        {
            var company = await _context.Tin200
                .AsNoTracking()
                .Where(x => x.Id == companyId)
                .Select(x => new
                {
                    x.Id,
                    x.CompanyName,
                    x.ExternalId
                })
                .FirstOrDefaultAsync();

            if (company == null)
            {
                return null;
            }

            var years = await (
                from companySurvey in _context.CompanySurvey.AsNoTracking()
                join survey in _context.Survey.AsNoTracking() on companySurvey.SurveyId equals survey.Id
                where companySurvey.CompanyId == companyId
                select survey.FinancialYear
            )
            .Distinct()
            .OrderByDescending(year => year)
            .ToListAsync();

            var estimateRows = await (
                from companySurvey in _context.CompanySurvey.AsNoTracking()
                join survey in _context.Survey.AsNoTracking() on companySurvey.SurveyId equals survey.Id
                where companySurvey.CompanyId == companyId
                select new
                {
                    survey.FinancialYear,
                    companySurvey.Id,
                    Estimate = EF.Property<bool?>(companySurvey, nameof(CompanySurvey.Estimate))
                }
            ).ToListAsync();

            var estimateByYear = estimateRows
                .GroupBy(x => x.FinancialYear)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.Id).First().Estimate == true);

            var latestAnswerIds = await (
                from answer in _context.Answer.AsNoTracking()
                join companySurvey in _context.CompanySurvey.AsNoTracking() on answer.CompanySurveyId equals companySurvey.Id
                join survey in _context.Survey.AsNoTracking() on companySurvey.SurveyId equals survey.Id
                where companySurvey.CompanyId == companyId
                group answer.Id by new { answer.QuestionId, survey.FinancialYear } into grouped
                select grouped.Max()
            ).ToListAsync();

            var latestAnswers = latestAnswerIds.Count == 0
                ? new List<CompanySurveyHistoryAnswerValue>()
                : await (
                from answer in _context.Answer.AsNoTracking()
                join companySurvey in _context.CompanySurvey.AsNoTracking() on answer.CompanySurveyId equals companySurvey.Id
                join survey in _context.Survey.AsNoTracking() on companySurvey.SurveyId equals survey.Id
                where companySurvey.CompanyId == companyId
                    && latestAnswerIds.Contains(answer.Id)
                select new CompanySurveyHistoryAnswerValue
                {
                    QuestionId = answer.QuestionId,
                    FinancialYear = survey.FinancialYear,
                    AnswerText = answer.AnswerText,
                    AnswerNumber = answer.AnswerNumber,
                    AnswerCurrency = answer.AnswerCurrency
                }
            ).ToListAsync();

            var questions = await (
                from question in _context.Question.AsNoTracking()
                join questionGroup in _context.QuestionGroup.AsNoTracking()
                    on question.GroupId equals questionGroup.Id into questionGroupJoin
                from questionGroup in questionGroupJoin.DefaultIfEmpty()
                orderby questionGroup == null ? 1 : 0,
                        questionGroup!.OrderNumber,
                        questionGroup.Id,
                        question.OrderNumber,
                        question.Id
                select new
                {
                    question.Id,
                    question.OrderNumber,
                    GroupTitle = questionGroup != null ? questionGroup.Title : question.GroupTitle,
                    question.QuestionText
                }
            ).ToListAsync();

            var answerLookup = latestAnswers
                .GroupBy(x => x.QuestionId)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToDictionary(
                        item => item.FinancialYear,
                        item => FormatAnswerValue(item.AnswerText, item.AnswerNumber, item.AnswerCurrency)));

            var rows = questions
                .Select(question => new CompanySurveyHistoryRow
                {
                    QuestionId = question.Id,
                    QuestionOrderNumber = question.OrderNumber,
                    GroupTitle = question.GroupTitle,
                    QuestionText = question.QuestionText,
                    AnswersByYear = years.ToDictionary(
                        year => year,
                        year => answerLookup.TryGetValue(question.Id, out var answersByYear)
                            && answersByYear.TryGetValue(year, out var value)
                            ? value
                            : string.Empty)
                })
                .ToList();

            return new CompanySurveyHistoryResult
            {
                CompanyId = company.Id,
                CompanyName = company.CompanyName,
                ExternalId = company.ExternalId,
                LatestEstimate = years.Any()
                    && estimateByYear.TryGetValue(years[0], out var latestEstimate)
                    && latestEstimate,
                EstimateByYear = years.ToDictionary(
                    year => year,
                    year => estimateByYear.TryGetValue(year, out var estimate) && estimate),
                Years = years,
                Rows = rows
            };
        }

        public async Task<List<AnswerExportRow>> GetAnswerExportRowsAsync(int? financialYear)
        {
            var query =
                from answer in _context.Answer.AsNoTracking()
                join companySurvey in _context.CompanySurvey.AsNoTracking() on answer.CompanySurveyId equals companySurvey.Id
                join survey in _context.Survey.AsNoTracking() on companySurvey.SurveyId equals survey.Id
                join company in _context.Tin200.AsNoTracking() on companySurvey.CompanyId equals company.Id
                join question in _context.Question.AsNoTracking() on answer.QuestionId equals question.Id
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
                from answer in _context.Answer.AsNoTracking()
                join companySurvey in _context.CompanySurvey.AsNoTracking() on answer.CompanySurveyId equals companySurvey.Id
                join survey in _context.Survey.AsNoTracking() on companySurvey.SurveyId equals survey.Id
                join company in _context.Tin200.AsNoTracking() on companySurvey.CompanyId equals company.Id
                join question in _context.Question.AsNoTracking() on answer.QuestionId equals question.Id
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
                    Locked = false,
                    Estimate = false,
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
            var plan = await BuildImportPlanAsync(excelStream, financialYear, ImportPlanMode.Qualtrics, autoCreateMissingGlobalRecords: false);
            return new AnswerImportPreviewResult
            {
                FinancialYear = financialYear,
                InsertedCount = plan.InsertedCount,
                UpdatedCount = plan.UpdatedCount,
                MatchedExternalIds = plan.MatchedExternalIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                UnmatchedExternalIds = plan.UnmatchedExternalIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                SkippedSubmittedRowKeys = plan.SkippedSubmittedRowKeys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                Errors = plan.Errors.ToList(),
                AvailableHeaders = plan.AvailableHeaders.ToList(),
                MatchedFields = plan.MatchedFields.ToList(),
                UnmatchedFields = plan.UnmatchedFields.ToList(),
                IdentifierFieldMatch = plan.IdentifierFieldMatch
            };
        }

        public async Task<AnswerImportPreviewResult> PreviewGlobalAnswersImportFromExcelAsync(Stream excelStream, int financialYear)
        {
            var plan = await BuildImportPlanAsync(excelStream, financialYear, ImportPlanMode.GlobalByIdThenName, autoCreateMissingGlobalRecords: false);
            return new AnswerImportPreviewResult
            {
                FinancialYear = financialYear,
                InsertedCount = plan.InsertedCount,
                UpdatedCount = plan.UpdatedCount,
                MatchedExternalIds = plan.MatchedExternalIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                UnmatchedExternalIds = plan.UnmatchedExternalIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                SkippedSubmittedRowKeys = plan.SkippedSubmittedRowKeys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                Errors = plan.Errors.ToList(),
                AvailableHeaders = plan.AvailableHeaders.ToList(),
                MatchedFields = plan.MatchedFields.ToList(),
                UnmatchedFields = plan.UnmatchedFields.ToList(),
                IdentifierFieldMatch = plan.IdentifierFieldMatch
            };
        }

        public async Task<AnswerImportResult> ImportAnswersFromExcelAsync(Stream excelStream, int financialYear)
        {
            var result = new AnswerImportResult();
            using var bufferedStream = new MemoryStream();
            await excelStream.CopyToAsync(bufferedStream);
            bufferedStream.Position = 0;

            var plan = await BuildImportPlanAsync(bufferedStream, financialYear, ImportPlanMode.Qualtrics, autoCreateMissingGlobalRecords: false);

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

                    // Skip if the existing answer already has a value — only fill blanks
                    var alreadyHasValue = !string.IsNullOrWhiteSpace(existingAnswer.AnswerText)
                        || existingAnswer.AnswerNumber.HasValue
                        || existingAnswer.AnswerCurrency.HasValue;
                    if (alreadyHasValue)
                    {
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

            var affectedCompanySurveyIds = plan.Operations
                .Select(o => o.CompanySurveyId)
                .Distinct()
                .ToList();

            if (affectedCompanySurveyIds.Any())
            {
                var affectedCompanySurveys = await _context.CompanySurvey
                    .Where(cs => affectedCompanySurveyIds.Contains(cs.Id))
                    .ToListAsync();

                foreach (var companySurvey in affectedCompanySurveys)
                {
                    companySurvey.Estimate = true;
                }
            }

            await _context.SaveChangesAsync();
            result.InsertedCount = plan.InsertedCount;
            result.UpdatedCount = plan.UpdatedCount;

            return result;
        }

        public async Task<AnswerImportResult> ImportGlobalAnswersFromExcelAsync(Stream excelStream, int financialYear)
        {
            var result = new AnswerImportResult();
            using var bufferedStream = new MemoryStream();
            await excelStream.CopyToAsync(bufferedStream);
            bufferedStream.Position = 0;

            var plan = await BuildImportPlanAsync(bufferedStream, financialYear, ImportPlanMode.GlobalByIdThenName, autoCreateMissingGlobalRecords: true);

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

            var affectedCompanySurveyIds = plan.MatchedCompanySurveyIds.Any()
                ? plan.MatchedCompanySurveyIds.ToList()
                : plan.Operations.Select(o => o.CompanySurveyId).Distinct().ToList();

            if (affectedCompanySurveyIds.Any())
            {
                var affectedCompanySurveys = await _context.CompanySurvey
                    .Where(cs => affectedCompanySurveyIds.Contains(cs.Id))
                    .ToListAsync();

                foreach (var companySurvey in affectedCompanySurveys)
                {
                    companySurvey.Estimate = true;
                }
            }

            await _context.SaveChangesAsync();
            result.InsertedCount = plan.InsertedCount;
            result.UpdatedCount = plan.UpdatedCount;

            return result;
        }

        private async Task EnsureCompanyAndCompanySurveyRecordsAsync(
            IEnumerable<string> externalIds,
            int financialYear,
            bool createSurveyIfMissing = false,
            IEnumerable<string>? companyNames = null)
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
                if (!createSurveyIfMissing)
                {
                    throw new InvalidOperationException($"No Survey record exists for financial year {financialYear}. Cannot create CompanySurvey rows for import.");
                }

                var newSurvey = new Survey
                {
                    FinancialYear = financialYear,
                    CurrentSurvey = false,
                    HeaderImageId = null
                };

                _context.Survey.Add(newSurvey);
                await _context.SaveChangesAsync();
                surveyId = newSurvey.Id;
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

            var normalizedCompanyNames = (companyNames ?? Enumerable.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var companyIdsFromName = new List<int>();
            if (normalizedCompanyNames.Any())
            {
                var companyRowsByName = await _context.Tin200
                    .Where(c => c.CompanyName != null && normalizedCompanyNames.Contains(c.CompanyName))
                    .Select(c => new { c.Id, c.CompanyName })
                    .ToListAsync();

                companyIdsFromName = companyRowsByName
                    .Where(c => c.CompanyName != null)
                    .GroupBy(c => c.CompanyName!.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.OrderByDescending(c => c.Id).First().Id)
                    .ToList();
            }

            var companyIds = companyIdByExternalId.Values
                .Concat(companyIdsFromName)
                .Distinct()
                .ToList();

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
                    Locked = false,
                    Estimate = false,
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

        private static readonly HashSet<string> QualtricsAllowedTitles = new(StringComparer.OrdinalIgnoreCase)
        {
            "Recipient Email", "CEO First Name", "CEO Last Name", "CEO Email", "CEO Phone",
            "Survey Contact First Name", "Survey Contact Last Name", "Survey Contact Email", "Survey Contact Phone"
        };

        private async Task<ImportPlanResult> BuildImportPlanAsync(Stream excelStream, int financialYear, ImportPlanMode mode, bool autoCreateMissingGlobalRecords = false)
        {
            var plan = new ImportPlanResult();

            using var workbook = new XLWorkbook(excelStream);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
            {
                plan.Errors.Add("The Excel workbook does not contain any worksheets.");
                return plan;
            }

            var headerRowNumber = mode == ImportPlanMode.GlobalByIdThenName || mode == ImportPlanMode.Qualtrics ? 1 : 2;
            var firstRow = worksheet.Row(headerRowNumber);
            if (firstRow == null || firstRow.IsEmpty())
            {
                plan.Errors.Add($"The worksheet must contain column headers on row {headerRowNumber}.");
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

            plan.AvailableHeaders = columnByHeader.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

            var normalizedColumnByHeader = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in columnByHeader)
            {
                var normalized = NormalizeHeader(header.Key);
                if (!string.IsNullOrWhiteSpace(normalized) && !normalizedColumnByHeader.ContainsKey(normalized))
                {
                    normalizedColumnByHeader[normalized] = header.Value;
                }
            }

            var headerByColumnNumber = columnByHeader.ToDictionary(k => k.Value, v => v.Key);

            bool TryGetColumn(out int columnNumber, out string matchedHeader, params string[] aliases)
            {
                foreach (var alias in aliases)
                {
                    if (columnByHeader.TryGetValue(alias, out columnNumber))
                    {
                        matchedHeader = alias;
                        return true;
                    }

                    var normalizedAlias = NormalizeHeader(alias);
                    if (!string.IsNullOrWhiteSpace(normalizedAlias)
                        && normalizedColumnByHeader.TryGetValue(normalizedAlias, out columnNumber))
                    {
                        matchedHeader = headerByColumnNumber.TryGetValue(columnNumber, out var resolvedHeader)
                            ? resolvedHeader
                            : alias;
                        return true;
                    }
                }

                columnNumber = 0;
                matchedHeader = string.Empty;
                return false;
            }

            int externalIdColumn = 0;
            int idColumn = 0;
            var hasNameColumn = false;
            var nameColumn = 0;

            if (mode == ImportPlanMode.Standard || mode == ImportPlanMode.Qualtrics)
            {
                if (!TryGetColumn(out externalIdColumn, out var matchedExternalIdHeader,
                    "ExternalRefence", "ExternalReference", "External ID", "ExternalID", "ID"))
                {
                    var available = plan.AvailableHeaders.Any() ? string.Join(", ", plan.AvailableHeaders) : "(none)";
                    plan.Errors.Add($"Missing required column header: ExternalRefence/External ID (or ID). Available headers on row {headerRowNumber}: {available}");
                    return plan;
                }

                if (mode == ImportPlanMode.Qualtrics)
                {
                    hasNameColumn = TryGetColumn(out nameColumn, out var matchedNameHeader, "Name", "Company Name", "CompanyName");
                    plan.IdentifierFieldMatch = hasNameColumn
                        ? $"External ID match column: {matchedExternalIdHeader}; Name fallback column: {matchedNameHeader}"
                        : $"External ID match column: {matchedExternalIdHeader}; Name fallback column: (not found)";
                }
                else
                {
                    plan.IdentifierFieldMatch = $"External ID match column: {matchedExternalIdHeader}";
                }
            }
            else
            {
                if (!TryGetColumn(out idColumn, out var matchedIdHeader, "ID", "External ID", "ExternalID"))
                {
                    var available = plan.AvailableHeaders.Any() ? string.Join(", ", plan.AvailableHeaders) : "(none)";
                    plan.Errors.Add($"Missing required column header: ID (or External ID). Available headers on row {headerRowNumber}: {available}");
                    return plan;
                }

                hasNameColumn = TryGetColumn(out nameColumn, out var matchedNameHeader, "Name", "Company Name", "CompanyName");
                plan.IdentifierFieldMatch = hasNameColumn
                    ? $"ID match column: {matchedIdHeader}; Name fallback column: {matchedNameHeader}"
                    : $"ID match column: {matchedIdHeader}; Name fallback column: (not found)";
            }

            var questions = mode == ImportPlanMode.Qualtrics
                ? await _context.Question
                    .Where(q => !string.IsNullOrWhiteSpace(q.ImportColumnName) && QualtricsAllowedTitles.Contains(q.Title ?? ""))
                    .OrderBy(q => q.OrderNumber)
                    .ThenBy(q => q.Id)
                    .ToListAsync()
                : mode == ImportPlanMode.Standard
                    ? await _context.Question
                        .Where(q => !string.IsNullOrWhiteSpace(q.ImportColumnName))
                        .OrderBy(q => q.OrderNumber)
                        .ThenBy(q => q.Id)
                        .ToListAsync()
                    : await _context.Question
                        .Where(q => !string.IsNullOrWhiteSpace(q.ImportColumnNameAlt))
                        .OrderBy(q => q.OrderNumber)
                        .ThenBy(q => q.Id)
                        .ToListAsync();

            if (!questions.Any())
            {
                plan.Errors.Add(mode == ImportPlanMode.GlobalByIdThenName
                    ? "No questions have ImportColumnNameAlt configured."
                    : "No questions have ImportColumnName configured.");
                return plan;
            }

            var globalHeaderMappings = mode == ImportPlanMode.GlobalByIdThenName
                ? columnByHeader
                    .Select(h =>
                    {
                        var header = h.Key.Trim();
                        var yearOffset = ParseGlobalColumnYearOffset(header);
                        var baseHeader = RemoveGlobalColumnYearOffset(header);
                        return (
                            Header: header,
                            ColumnNumber: h.Value,
                            BaseHeader: baseHeader,
                            BaseHeaderNormalized: NormalizeHeader(baseHeader),
                            YearOffset: yearOffset);
                    })
                    .ToList()
                : new List<(string Header, int ColumnNumber, string BaseHeader, string BaseHeaderNormalized, int YearOffset)>();

            var mappedQuestions = new List<(Question Question, int ColumnNumber, string ImportColumnName, int TargetFinancialYear)>();
            foreach (var question in questions)
            {
                var configuredColumnName = mode == ImportPlanMode.GlobalByIdThenName
                    ? question.ImportColumnNameAlt
                    : question.ImportColumnName;

                if (string.IsNullOrWhiteSpace(configuredColumnName))
                {
                    continue;
                }

                var importColumnName = configuredColumnName.Trim();
                if (mode == ImportPlanMode.GlobalByIdThenName)
                {
                    var questionYearOffset = ParseGlobalColumnYearOffset(importColumnName);
                    var importBaseName = RemoveGlobalColumnYearOffset(importColumnName);
                    var normalizedImportBase = NormalizeHeader(importBaseName);

                    var questionHeaderMatches = globalHeaderMappings
                        .Where(h => h.BaseHeaderNormalized == normalizedImportBase
                            && (questionYearOffset == 0 ? h.YearOffset >= 0 : h.YearOffset == questionYearOffset))
                        .OrderBy(h => h.YearOffset)
                        .ThenBy(h => h.ColumnNumber)
                        .ToList();

                    if (questionHeaderMatches.Any())
                    {
                        foreach (var match in questionHeaderMatches)
                        {
                            var targetFinancialYear = questionYearOffset == 0
                                ? financialYear - match.YearOffset
                                : financialYear;
                            mappedQuestions.Add((question, match.ColumnNumber, importColumnName, targetFinancialYear));
                            var targetHeaderLabel = questionYearOffset == 0 ? importBaseName : importColumnName;
                            plan.MatchedFields.Add($"Q{question.Id} ({question.Title ?? question.QuestionText ?? "Untitled"}): {financialYear} {match.Header} -> {targetFinancialYear} {targetHeaderLabel} (target FY {targetFinancialYear})");
                        }
                    }
                    else
                    {
                        plan.UnmatchedFields.Add(questionYearOffset == 0
                            ? $"Q{question.Id} ({question.Title ?? question.QuestionText ?? "Untitled"}): {importColumnName} (no matching header with base '{importBaseName}')"
                            : $"Q{question.Id} ({question.Title ?? question.QuestionText ?? "Untitled"}): {importColumnName} (missing exact header offset {questionYearOffset})");
                    }
                }
                else
                {
                    if (TryGetColumn(out var columnNumber, out var matchedHeaderName, importColumnName))
                    {
                        mappedQuestions.Add((question, columnNumber, importColumnName, financialYear));
                        plan.MatchedFields.Add($"Q{question.Id} ({question.Title ?? question.QuestionText ?? "Untitled"}): {importColumnName} -> {matchedHeaderName}");
                    }
                    else
                    {
                        plan.UnmatchedFields.Add($"Q{question.Id} ({question.Title ?? question.QuestionText ?? "Untitled"}): {importColumnName}");
                    }
                }
            }

            if (!mappedQuestions.Any())
            {
                plan.Errors.Add(mode == ImportPlanMode.GlobalByIdThenName
                    ? "No question ImportColumnNameAlt values match any Excel column headers."
                    : "No question ImportColumnName values match any Excel column headers.");
                return plan;
            }

            if (mode == ImportPlanMode.GlobalByIdThenName && autoCreateMissingGlobalRecords)
            {
                var importExternalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var importCompanyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var firstDataRowNumberForGlobal = headerRowNumber + 1;
                var lastRowNumberForGlobal = worksheet.LastRowUsed()?.RowNumber() ?? firstDataRowNumberForGlobal - 1;

                for (var rowNumber = firstDataRowNumberForGlobal; rowNumber <= lastRowNumberForGlobal; rowNumber++)
                {
                    var row = worksheet.Row(rowNumber);
                    var idValue = row.Cell(idColumn).GetString().Trim();
                    var nameValue = hasNameColumn ? row.Cell(nameColumn).GetString().Trim() : string.Empty;

                    if (!string.IsNullOrWhiteSpace(idValue))
                    {
                        importExternalIds.Add(idValue);
                    }

                    if (!string.IsNullOrWhiteSpace(nameValue))
                    {
                        importCompanyNames.Add(nameValue);
                    }
                }

                var targetYears = mappedQuestions
                    .Select(q => q.TargetFinancialYear)
                    .Distinct()
                    .ToList();

                foreach (var targetYear in targetYears)
                {
                    try
                    {
                        await EnsureCompanyAndCompanySurveyRecordsAsync(
                            importExternalIds,
                            targetYear,
                            createSurveyIfMissing: true,
                            companyNames: importCompanyNames);
                    }
                    catch (Exception ex)
                    {
                        plan.Errors.Add($"Unable to prepare CompanySurvey records for FY {targetYear}: {ex.Message}");
                    }
                }
            }

            var targetFinancialYears = mode == ImportPlanMode.GlobalByIdThenName
                ? mappedQuestions.Select(q => q.TargetFinancialYear).Distinct().ToList()
                : new List<int> { financialYear };

            var companySurveyRows = await (
                from companySurvey in _context.CompanySurvey
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                join company in _context.Tin200 on companySurvey.CompanyId equals company.Id
                where targetFinancialYears.Contains(survey.FinancialYear)
                select new
                {
                    companySurvey.Id,
                    Submitted = EF.Property<bool?>(companySurvey, nameof(CompanySurvey.Submitted)),
                    company.ExternalId,
                    company.CompanyName,
                    survey.FinancialYear
                }
            )
            .ToListAsync();

            var companySurveyByExternalId = companySurveyRows
                .Where(x => !string.IsNullOrWhiteSpace(x.ExternalId))
                .GroupBy(x => x.ExternalId!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var selected = g.OrderByDescending(x => x.Id).First();
                        return (selected.Id, selected.Submitted);
                    },
                    StringComparer.OrdinalIgnoreCase);

            var companySurveyByExternalIdAndYear = companySurveyRows
                .Where(x => !string.IsNullOrWhiteSpace(x.ExternalId))
                .GroupBy(x => BuildCompanyYearLookupKey(x.ExternalId!, x.FinancialYear), StringComparer.Ordinal)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var selected = g.OrderByDescending(x => x.Id).First();
                        return (selected.Id, selected.Submitted);
                    },
                    StringComparer.Ordinal);

            var companySurveyByCompanyNameAndYear = companySurveyRows
                .Where(x => !string.IsNullOrWhiteSpace(x.CompanyName))
                .GroupBy(x => BuildCompanyYearLookupKey(x.CompanyName!, x.FinancialYear), StringComparer.Ordinal)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var selected = g.OrderByDescending(x => x.Id).First();
                        return (selected.Id, selected.Submitted);
                    },
                    StringComparer.Ordinal);

            var companySurveyByCompanyName = companySurveyRows
                .Where(x => !string.IsNullOrWhiteSpace(x.CompanyName))
                .GroupBy(x => x.CompanyName!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var selected = g.OrderByDescending(x => x.Id).First();
                        return (selected.Id, selected.Submitted);
                    },
                    StringComparer.OrdinalIgnoreCase);

            if ((mode == ImportPlanMode.Standard || mode == ImportPlanMode.Qualtrics) && !companySurveyByExternalId.Any())
            {
                plan.Errors.Add($"No CompanySurvey records found for financial year {financialYear} with matching External ID values.");
                return plan;
            }

            if (mode == ImportPlanMode.GlobalByIdThenName && !companySurveyByExternalIdAndYear.Any() && !companySurveyByCompanyNameAndYear.Any())
            {
                var yearsText = targetFinancialYears.Any()
                    ? string.Join(", ", targetFinancialYears.OrderByDescending(y => y))
                    : financialYear.ToString(CultureInfo.InvariantCulture);
                plan.Errors.Add($"No CompanySurvey records found for financial years {yearsText} with matching External ID or Company Name values.");
                return plan;
            }

            var companySurveyIds = mode == ImportPlanMode.GlobalByIdThenName
                ? companySurveyByExternalIdAndYear.Values
                    .Concat(companySurveyByCompanyNameAndYear.Values)
                    .Select(x => x.Id)
                    .Distinct()
                    .ToList()
                : companySurveyByExternalId.Values
                    .Where(x => x.Submitted != true)
                    .Select(x => x.Id)
                    .Distinct()
                    .ToList();
            var questionIds = mappedQuestions.Select(q => q.Question.Id).ToList();

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

            var firstDataRowNumber = headerRowNumber + 1;
            var lastRowNumber = worksheet.LastRowUsed()?.RowNumber() ?? firstDataRowNumber - 1;

            for (var rowNumber = firstDataRowNumber; rowNumber <= lastRowNumber; rowNumber++)
            {
                var row = worksheet.Row(rowNumber);
                var companySurveyId = 0;

                if (mode == ImportPlanMode.Standard || mode == ImportPlanMode.Qualtrics)
                {
                    var externalId = row.Cell(externalIdColumn).GetString().Trim();
                    if (mode == ImportPlanMode.Qualtrics)
                    {
                        var nameValue = hasNameColumn ? row.Cell(nameColumn).GetString().Trim() : string.Empty;

                        if (string.IsNullOrWhiteSpace(externalId) && string.IsNullOrWhiteSpace(nameValue))
                        {
                            continue;
                        }

                        var externalIdMatch = (Id: 0, Submitted: (bool?)null);
                        var nameMatch = (Id: 0, Submitted: (bool?)null);

                        var hasExternalIdMatch = !string.IsNullOrWhiteSpace(externalId)
                            && companySurveyByExternalId.TryGetValue(externalId, out externalIdMatch);
                        var hasNameMatch = !hasExternalIdMatch
                            && !string.IsNullOrWhiteSpace(nameValue)
                            && companySurveyByCompanyName.TryGetValue(nameValue, out nameMatch);

                        if (!hasExternalIdMatch && !hasNameMatch)
                        {
                            plan.UnmatchedExternalIds.Add(string.IsNullOrWhiteSpace(externalId)
                                ? nameValue
                                : $"{externalId} (name: {nameValue})");
                            continue;
                        }

                        var selectedMatch = hasExternalIdMatch ? externalIdMatch : nameMatch;
                        if (selectedMatch.Submitted == true)
                        {
                            plan.SkippedSubmittedRowKeys.Add(hasExternalIdMatch ? externalId : nameValue);
                            continue;
                        }

                        companySurveyId = selectedMatch.Id;
                        plan.MatchedCompanySurveyIds.Add(companySurveyId);
                        plan.MatchedExternalIds.Add(hasExternalIdMatch
                            ? externalId
                            : (string.IsNullOrWhiteSpace(externalId)
                                ? nameValue
                                : $"{externalId} (fallback to name: {nameValue})"));
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(externalId))
                        {
                            continue;
                        }

                        if (!companySurveyByExternalId.TryGetValue(externalId, out var externalIdMatch))
                        {
                            plan.UnmatchedExternalIds.Add(externalId);
                            continue;
                        }

                        if (externalIdMatch.Submitted == true)
                        {
                            plan.SkippedSubmittedRowKeys.Add(externalId);
                            continue;
                        }

                        companySurveyId = externalIdMatch.Id;
                        plan.MatchedCompanySurveyIds.Add(companySurveyId);

                        plan.MatchedExternalIds.Add(externalId);
                    }
                }
                else
                {
                    var idValue = row.Cell(idColumn).GetString().Trim();
                    var nameValue = hasNameColumn ? row.Cell(nameColumn).GetString().Trim() : string.Empty;

                    if (string.IsNullOrWhiteSpace(idValue) && string.IsNullOrWhiteSpace(nameValue))
                    {
                        continue;
                    }
                }

                foreach (var mappedQuestion in mappedQuestions)
                {
                    var question = mappedQuestion.Question;
                    var importColumnName = mappedQuestion.ImportColumnName;
                    var columnNumber = mappedQuestion.ColumnNumber;
                    var targetQuestionYear = mappedQuestion.TargetFinancialYear;

                    if (mode == ImportPlanMode.GlobalByIdThenName)
                    {
                        var idValue = row.Cell(idColumn).GetString().Trim();
                        var nameValue = hasNameColumn ? row.Cell(nameColumn).GetString().Trim() : string.Empty;
                        var rowKey = $"{idValue}|{nameValue}".Trim('|');
                        var externalIdYearMatch = (Id: 0, Submitted: (bool?)null);
                        var nameYearMatch = (Id: 0, Submitted: (bool?)null);

                        var hasIdMatch = !string.IsNullOrWhiteSpace(idValue)
                            && companySurveyByExternalIdAndYear.TryGetValue(BuildCompanyYearLookupKey(idValue, targetQuestionYear), out externalIdYearMatch);

                        var hasNameMatch = !hasIdMatch
                            && !string.IsNullOrWhiteSpace(nameValue)
                            && companySurveyByCompanyNameAndYear.TryGetValue(BuildCompanyYearLookupKey(nameValue, targetQuestionYear), out nameYearMatch);

                        if (!hasIdMatch && !hasNameMatch)
                        {
                            plan.UnmatchedExternalIds.Add($"{rowKey}|FY{targetQuestionYear}".Trim('|'));
                            continue;
                        }

                        if (hasIdMatch)
                        {
                            companySurveyId = externalIdYearMatch.Id;
                            plan.MatchedExternalIds.Add(idValue);
                        }
                        else
                        {
                            companySurveyId = nameYearMatch.Id;
                            plan.MatchedExternalIds.Add(string.IsNullOrWhiteSpace(idValue)
                                ? nameValue
                                : $"{idValue} (fallback to name: {nameValue})");
                        }

                        plan.MatchedCompanySurveyIds.Add(companySurveyId);
                    }

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
                        var columnDescriptor = mode == ImportPlanMode.GlobalByIdThenName
                            ? $"{importColumnName} (target FY {targetQuestionYear})"
                            : importColumnName;
                        plan.Errors.Add($"Row {rowNumber}, column '{columnDescriptor}': unable to parse value '{textValue}' for question type '{question.AnswerType}'.");

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

            static string NormalizeHeader(string input)
            {
                if (string.IsNullOrWhiteSpace(input))
                {
                    return string.Empty;
                }

                return new string(input.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            }
        }

        private static string BuildCompanyYearLookupKey(string companyLookupValue, int financialYear)
        {
            if (string.IsNullOrWhiteSpace(companyLookupValue))
            {
                return financialYear.ToString(CultureInfo.InvariantCulture);
            }

            return $"{companyLookupValue.Trim().ToUpperInvariant()}|{financialYear}";
        }

        private static int ParseGlobalColumnYearOffset(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
            {
                return 0;
            }

            var value = columnName.AsSpan();
            for (var index = 0; index < value.Length - 1; index++)
            {
                if (char.ToUpperInvariant(value[index]) != 'C' || char.ToUpperInvariant(value[index + 1]) != 'Y')
                {
                    continue;
                }

                if (index > 0 && char.IsLetterOrDigit(value[index - 1]))
                {
                    continue;
                }

                var cursor = index + 2;
                while (cursor < value.Length && char.IsWhiteSpace(value[cursor]))
                {
                    cursor++;
                }

                if (cursor >= value.Length || !IsDash(value[cursor]))
                {
                    return 0;
                }

                cursor++;
                while (cursor < value.Length && char.IsWhiteSpace(value[cursor]))
                {
                    cursor++;
                }

                var digitStart = cursor;
                while (cursor < value.Length && char.IsDigit(value[cursor]))
                {
                    cursor++;
                }

                if (digitStart == cursor)
                {
                    return 0;
                }

                return int.TryParse(value[digitStart..cursor], out var offset) ? offset : 0;
            }

            return 0;

            static bool IsDash(char c)
            {
                return c == '-'
                    || c == '\u2010' // hyphen
                    || c == '\u2011' // non-breaking hyphen
                    || c == '\u2012' // figure dash
                    || c == '\u2013' // en dash
                    || c == '\u2014' // em dash
                    || c == '\u2212'; // minus sign
            }
        }

        private static string RemoveGlobalColumnYearOffset(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
            {
                return string.Empty;
            }

            var source = columnName.Trim();
            var value = source.AsSpan();
            for (var index = 0; index < value.Length - 1; index++)
            {
                if (char.ToUpperInvariant(value[index]) != 'C' || char.ToUpperInvariant(value[index + 1]) != 'Y')
                {
                    continue;
                }

                if (index > 0 && char.IsLetterOrDigit(value[index - 1]))
                {
                    continue;
                }

                var cursor = index + 2;
                while (cursor < value.Length && char.IsWhiteSpace(value[cursor]))
                {
                    cursor++;
                }

                if (cursor >= value.Length || !IsDash(value[cursor]))
                {
                    return source;
                }

                var dashIndex = cursor;
                cursor++;
                while (cursor < value.Length && char.IsWhiteSpace(value[cursor]))
                {
                    cursor++;
                }

                var digitStart = cursor;
                while (cursor < value.Length && char.IsDigit(value[cursor]))
                {
                    cursor++;
                }

                if (digitStart == cursor)
                {
                    return source;
                }

                var prefix = source[..dashIndex].TrimEnd();
                var suffix = source[cursor..].TrimStart();
                return string.IsNullOrWhiteSpace(suffix)
                    ? prefix
                    : $"{prefix} {suffix}";
            }

            return source;

            static bool IsDash(char c)
            {
                return c == '-'
                    || c == '\u2010'
                    || c == '\u2011'
                    || c == '\u2012'
                    || c == '\u2013'
                    || c == '\u2014'
                    || c == '\u2212';
            }
        }

        private static ParsedAnswerValue? BuildAnswerValue(string? answerType, IXLCell cell)
        {
            var rawValue = cell.GetString().Trim();
            var normalizedType = answerType?.Trim();

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return new ParsedAnswerValue();
            }

            // Common export placeholders like "$-" or "N/A" should be treated as blank values.
            if (IsPlaceholderEmptyValue(rawValue))
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

            static bool IsPlaceholderEmptyValue(string value)
            {
                var normalized = value.Trim().ToLowerInvariant();
                if (normalized is "-" or "--" or "n/a" or "na" or "nil" or "none")
                {
                    return true;
                }

                var compact = new string(normalized.Where(c => c != ' ' && c != '\t' && c != ',').ToArray());
                compact = compact.Replace("$", string.Empty)
                    .Replace("nz$", string.Empty)
                    .Replace("usd", string.Empty)
                    .Replace("aud", string.Empty)
                    .Replace("eur", string.Empty)
                    .Replace("gbp", string.Empty);

                return compact is "-" or "--";
            }
        }

        private static string FormatAnswerValue(string? answerText, double? answerNumber, decimal? answerCurrency)
        {
            if (!string.IsNullOrWhiteSpace(answerText))
            {
                return answerText.Trim();
            }

            if (answerCurrency.HasValue)
            {
                return answerCurrency.Value.ToString("N0", CultureInfo.InvariantCulture);
            }

            if (answerNumber.HasValue)
            {
                return answerNumber.Value.ToString("G", CultureInfo.InvariantCulture);
            }

            return string.Empty;
        }

        private sealed class ParsedAnswerValue
        {
            public string? AnswerText { get; set; }
            public double? AnswerNumber { get; set; }
            public decimal? AnswerCurrency { get; set; }
        }

        private sealed class CompanySurveyHistoryAnswerValue
        {
            public int QuestionId { get; set; }
            public int FinancialYear { get; set; }
            public string? AnswerText { get; set; }
            public double? AnswerNumber { get; set; }
            public decimal? AnswerCurrency { get; set; }
        }

        private enum ImportPlanMode
        {
            Standard,
            Qualtrics,
            GlobalByIdThenName
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
            public HashSet<int> MatchedCompanySurveyIds { get; } = new();
            public HashSet<string> MatchedExternalIds { get; } = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> UnmatchedExternalIds { get; } = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> SkippedSubmittedRowKeys { get; } = new(StringComparer.OrdinalIgnoreCase);
            public List<string> Errors { get; } = new();
            public List<string> AvailableHeaders { get; set; } = new();
            public List<string> MatchedFields { get; set; } = new();
            public List<string> UnmatchedFields { get; set; } = new();
            public string IdentifierFieldMatch { get; set; } = string.Empty;
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
            public List<string> SkippedSubmittedRowKeys { get; set; } = new();
            public List<string> Errors { get; set; } = new();
            public List<string> AvailableHeaders { get; set; } = new();
            public List<string> MatchedFields { get; set; } = new();
            public List<string> UnmatchedFields { get; set; } = new();
            public string IdentifierFieldMatch { get; set; } = string.Empty;
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
            public string? AnswerType { get; set; }
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
            public bool Estimate { get; set; }
            public DateTime? SavedDate { get; set; }
            public DateTime? SubmittedDate { get; set; }
            public DateTime? RequestedDate { get; set; }
        }

        public class CompanySurveyHistoryResult
        {
            public int CompanyId { get; set; }
            public string? CompanyName { get; set; }
            public string? ExternalId { get; set; }
            public bool LatestEstimate { get; set; }
            public Dictionary<int, bool> EstimateByYear { get; set; } = new();
            public List<int> Years { get; set; } = new();
            public List<CompanySurveyHistoryRow> Rows { get; set; } = new();
        }

        public class CompanySurveyHistoryRow
        {
            public int QuestionId { get; set; }
            public int? QuestionOrderNumber { get; set; }
            public string? GroupTitle { get; set; }
            public string? QuestionText { get; set; }
            public Dictionary<int, string> AnswersByYear { get; set; } = new();
        }
    }
}
