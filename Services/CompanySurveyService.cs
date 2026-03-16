using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Models;

namespace TINWeb.Services
{
    public class CompanySurveyService
    {
        private readonly ApplicationDbContext _context;

        public CompanySurveyService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<CompanySurvey>> GetAllAsync()
        {
            return await _context.CompanySurvey
                .OrderByDescending(x => x.Id)
                .ToListAsync();
        }

        public async Task<List<CompanySurveyListRow>> GetListRowsAsync()
        {
            return await GetListRowsAsync(null);
        }

        public async Task<List<CompanySurveyListRow>> GetListRowsAsync(int? financialYear)
        {
            var query =
                from companySurvey in _context.CompanySurvey
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                join company in _context.Tin200 on companySurvey.CompanyId equals company.Id into companyJoin
                from company in companyJoin.DefaultIfEmpty()
                select new CompanySurveyListRow
                {
                    Id = companySurvey.Id,
                    CompanyId = companySurvey.CompanyId,
                    CompanyName = company.CompanyName,
                    FinancialYear = survey.FinancialYear,
                    Saved = companySurvey.Saved,
                    Submitted = companySurvey.Submitted,
                    Requested = companySurvey.Requested,
                    Estimate = companySurvey.Estimate ?? false,
                    SavedDate = companySurvey.SavedDate,
                    SubmittedDate = companySurvey.SubmittedDate,
                    RequestedDate = companySurvey.RequestedDate,
                    AnswerCount = _context.Answer.Count(a =>
                        a.CompanySurveyId == companySurvey.Id &&
                        (a.AnswerText != null || a.AnswerCurrency != null || a.AnswerNumber != null))
                };

            if (financialYear.HasValue)
            {
                query = query.Where(x => x.FinancialYear == financialYear.Value);
            }

            return await query
                .OrderBy(x => x.CompanyName)
                .ThenBy(x => x.Id)
                .ToListAsync();
        }

        public async Task<List<int>> GetAvailableFinancialYearsAsync()
        {
            return await _context.Survey
                .Select(s => s.FinancialYear)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();
        }

        public async Task<int?> GetCurrentSurveyFinancialYearAsync()
        {
            return await _context.Survey
                .Where(s => s.CurrentSurvey)
                .Select(s => (int?)s.FinancialYear)
                .OrderByDescending(y => y)
                .FirstOrDefaultAsync();
        }

        public async Task<CompanySurvey?> GetByIdAsync(int id)
        {
            return await _context.CompanySurvey.FindAsync(id);
        }

        public async Task<CompanySurvey> CreateAsync(CompanySurvey record)
        {
            _context.CompanySurvey.Add(record);
            await _context.SaveChangesAsync();
            return record;
        }

        public async Task<CompanySurvey> UpdateAsync(CompanySurvey record)
        {
            _context.CompanySurvey.Update(record);
            await _context.SaveChangesAsync();
            return record;
        }

        public async Task DeleteAsync(int id)
        {
            var record = await GetByIdAsync(id);
            if (record != null)
            {
                _context.CompanySurvey.Remove(record);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.CompanySurvey.AnyAsync(x => x.Id == id);
        }

        public async Task<int> BulkSubmitWithAnswersAsync(int? financialYear)
        {
            var surveyIds = financialYear.HasValue
                ? await _context.Survey
                    .Where(s => s.FinancialYear == financialYear.Value)
                    .Select(s => s.Id)
                    .ToListAsync()
                : null;

            var query = _context.CompanySurvey
                .Where(cs => !cs.Submitted)
                .Where(cs => _context.Answer.Any(a =>
                    a.CompanySurveyId == cs.Id &&
                    (a.AnswerText != null || a.AnswerCurrency != null || a.AnswerNumber != null)));

            if (surveyIds != null)
                query = query.Where(cs => surveyIds.Contains(cs.SurveyId));

            var records = await query.ToListAsync();
            var submittedDate = new DateTime(2025, 12, 1);

            foreach (var r in records)
            {
                r.Submitted = true;
                r.SubmittedDate = submittedDate;
            }

            await _context.SaveChangesAsync();
            return records.Count;
        }

        public class CompanySurveyListRow
        {
            public int Id { get; set; }
            public int CompanyId { get; set; }
            public string? CompanyName { get; set; }
            public int FinancialYear { get; set; }
            public bool Saved { get; set; }
            public bool Submitted { get; set; }
            public bool Requested { get; set; }
            public bool Estimate { get; set; }
            public DateTime? SavedDate { get; set; }
            public DateTime? SubmittedDate { get; set; }
            public DateTime? RequestedDate { get; set; }
            public int AnswerCount { get; set; }
        }
    }
}
