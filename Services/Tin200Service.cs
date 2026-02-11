using Microsoft.EntityFrameworkCore;
using TINWorkspaceTemp.Data;
using TINWorkspaceTemp.Models;

namespace TINWorkspaceTemp.Services
{
    public class Tin200Service
    {
        private readonly ApplicationDbContext _context;

        public Tin200Service(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Tin200>> GetAllTin200Async(int? financialYear = null)
        {
            var query = _context.Tin200.AsQueryable();
            if (financialYear.HasValue)
            {
                query = query.Where(t => t.FinancialYear == financialYear.Value);
            }
            return await query.OrderByDescending(t => t.Id).ToListAsync();
        }

        public async Task<List<int>> GetAvailableFinancialYearsAsync()
        {
            return await _context.Tin200
                .Where(t => t.FinancialYear != null)
                .Select(t => t.FinancialYear!.Value)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();
        }

        public async Task<Tin200?> GetTin200ByIdAsync(int id)
        {
            return await _context.Tin200.FindAsync(id);
        }

        public async Task<Tin200> CreateTin200Async(Tin200 tin200)
        {
            _context.Tin200.Add(tin200);
            await _context.SaveChangesAsync();
            return tin200;
        }

        public async Task<Tin200> UpdateTin200Async(Tin200 tin200)
        {
            _context.Tin200.Update(tin200);
            await _context.SaveChangesAsync();
            return tin200;
        }

        public async Task DeleteTin200Async(int id)
        {
            var tin200 = await GetTin200ByIdAsync(id);
            if (tin200 != null)
            {
                _context.Tin200.Remove(tin200);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> Tin200ExistsAsync(int id)
        {
            return await _context.Tin200.AnyAsync(t => t.Id == id);
        }
    }
}
