using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Models;

namespace TINWeb.Services
{
    public class SurveyService
    {
        private readonly ApplicationDbContext _context;

        public SurveyService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Survey>> GetAllAsync()
        {
            return await _context.Survey
                .OrderByDescending(x => x.Id)
                .ToListAsync();
        }

        public async Task<Survey?> GetByIdAsync(int id)
        {
            return await _context.Survey.FindAsync(id);
        }

        public async Task<Survey> CreateAsync(Survey survey)
        {
            _context.Survey.Add(survey);
            await _context.SaveChangesAsync();
            return survey;
        }

        public async Task<Survey> UpdateAsync(Survey survey)
        {
            _context.Survey.Update(survey);
            await _context.SaveChangesAsync();
            return survey;
        }

        public async Task DeleteAsync(int id)
        {
            var survey = await GetByIdAsync(id);
            if (survey != null)
            {
                _context.Survey.Remove(survey);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Survey.AnyAsync(x => x.Id == id);
        }

        public async Task<List<CurrentSurveyCompanyRow>> GetCurrentSurveyCompanyRowsAsync()
        {
            var query =
                from survey in _context.Survey
                where survey.CurrentSurvey
                join companySurvey in _context.CompanySurvey on survey.Id equals companySurvey.SurveyId
                join company in _context.Tin200 on companySurvey.CompanyId equals company.Id
                join answer in _context.Answer on companySurvey.Id equals answer.CompanySurveyId into answers
                orderby company.CompanyName, companySurvey.Id
                select new CurrentSurveyCompanyRow
                {
                    CompanySurveyId = companySurvey.Id,
                    CompanyName = company.CompanyName,
                    FinancialYear = survey.FinancialYear,
                    AnswerCount = answers.Count()
                };

            return await query.ToListAsync();
        }

        public class CurrentSurveyCompanyRow
        {
            public int CompanySurveyId { get; set; }
            public string? CompanyName { get; set; }
            public int FinancialYear { get; set; }
            public int AnswerCount { get; set; }
        }
    }
}
