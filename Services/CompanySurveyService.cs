using Microsoft.EntityFrameworkCore;
using TINWorkspaceTemp.Data;
using TINWorkspaceTemp.Models;

namespace TINWorkspaceTemp.Services
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
    }
}
