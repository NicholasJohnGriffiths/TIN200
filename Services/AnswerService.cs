using Microsoft.EntityFrameworkCore;
using TINWorkspaceTemp.Data;

namespace TINWorkspaceTemp.Services
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
            return await (
                from answer in _context.Answer
                join companySurvey in _context.CompanySurvey on answer.ClientSurveyId equals companySurvey.Id
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                select survey.FinancialYear
            )
            .Distinct()
            .OrderByDescending(year => year)
            .ToListAsync();
        }

        public async Task<List<CompanySurveyOption>> GetCompanySurveyOptionsAsync(int? financialYear)
        {
            var query =
                from companySurvey in _context.CompanySurvey
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                join company in _context.Tin200 on companySurvey.CompanyId equals company.Id
                select new CompanySurveyOption
                {
                    CompanySurveyId = companySurvey.Id,
                    CompanyId = company.Id,
                    CompanyName = company.CompanyName,
                    FinancialYear = survey.FinancialYear
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
            var query =
                from answer in _context.Answer
                join companySurvey in _context.CompanySurvey on answer.ClientSurveyId equals companySurvey.Id
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                join company in _context.Tin200 on companySurvey.CompanyId equals company.Id
                join question in _context.Question on answer.QuestionId equals question.Id
                select new AnswerListRow
                {
                    Id = answer.Id,
                    ClientSurveyId = answer.ClientSurveyId,
                    CompanyId = company.Id,
                    CompanyName = company.CompanyName,
                    FinancialYear = survey.FinancialYear,
                    QuestionId = question.Id,
                    QuestionText = question.QuestionText,
                    AnswerText = answer.AnswerText,
                    AnswerNumber = answer.AnswerNumber,
                    AnswerCurrency = answer.AnswerCurrency
                };

            query = query.Where(x => x.ClientSurveyId == companySurveyId);

            return await query
                .OrderBy(x => x.CompanyName)
                .ThenBy(x => x.QuestionId)
                .ToListAsync();
        }

        public async Task<AnswerEditRow?> GetAnswerForEditAsync(int answerId)
        {
            return await (
                from answer in _context.Answer
                join companySurvey in _context.CompanySurvey on answer.ClientSurveyId equals companySurvey.Id
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                join company in _context.Tin200 on companySurvey.CompanyId equals company.Id
                join question in _context.Question on answer.QuestionId equals question.Id
                where answer.Id == answerId
                select new AnswerEditRow
                {
                    Id = answer.Id,
                    ClientSurveyId = answer.ClientSurveyId,
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

        public class AnswerListRow
        {
            public int Id { get; set; }
            public int ClientSurveyId { get; set; }
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
            public int ClientSurveyId { get; set; }
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
            public int ClientSurveyId { get; set; }
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
        }
    }
}
