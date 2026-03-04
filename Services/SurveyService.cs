using Microsoft.EntityFrameworkCore;
using TINWorkspaceTemp.Data;
using TINWorkspaceTemp.Models;

namespace TINWorkspaceTemp.Services
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
    }
}
