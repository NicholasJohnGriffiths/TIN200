using Microsoft.EntityFrameworkCore;
using TINWeb.Data;

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
                    AnswerCount = answerJoin.Select(a => a.QuestionId).Distinct().Count()
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
                    QuestionText = question.QuestionText,
                    AnswerText = answer != null ? answer.AnswerText : null,
                    AnswerNumber = answer != null ? answer.AnswerNumber : null,
                    AnswerCurrency = answer != null ? answer.AnswerCurrency : null
                };

            return await query.ToListAsync();
        }

        public async Task<AnswerEditRow?> GetAnswerForEditAsync(int answerId)
        {
            return await (
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
                    AnswerText = answer.AnswerText,
                    AnswerNumber = answer.AnswerNumber,
                    AnswerCurrency = answer.AnswerCurrency
                }
            ).FirstOrDefaultAsync();
        }

        public async Task<bool> UpdateAnswerAsync(AnswerEditInput input)
        {
            var answer = await _context.Answer.FirstOrDefaultAsync(x => x.Id == input.Id);
            if (answer == null)
            {
                return false;
            }

            answer.AnswerText = input.AnswerText;
            answer.AnswerNumber = input.AnswerNumber;
            answer.AnswerCurrency = input.AnswerCurrency;

            await _context.SaveChangesAsync();
            return true;
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
    [AnswerNumber] [int] NULL,
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

        public class AnswerListRow
        {
            public int Id { get; set; }
            public int CompanySurveyId { get; set; }
            public int CompanyId { get; set; }
            public string? CompanyName { get; set; }
            public int FinancialYear { get; set; }
            public int QuestionId { get; set; }
            public string? QuestionText { get; set; }
            public string? AnswerText { get; set; }
            public int? AnswerNumber { get; set; }
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
            public string? AnswerText { get; set; }
            public int? AnswerNumber { get; set; }
            public decimal? AnswerCurrency { get; set; }
        }

        public class AnswerEditInput
        {
            public int Id { get; set; }
            public int CompanySurveyId { get; set; }
            public string? AnswerText { get; set; }
            public int? AnswerNumber { get; set; }
            public decimal? AnswerCurrency { get; set; }
        }

        public class CompanySurveyOption
        {
            public int CompanySurveyId { get; set; }
            public int CompanyId { get; set; }
            public string? CompanyName { get; set; }
            public int FinancialYear { get; set; }
            public int AnswerCount { get; set; }
        }
    }
}
