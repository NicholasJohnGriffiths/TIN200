using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Models;
using System.Data;

namespace TINWeb.Services
{
    public class CompanyService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CompanyService> _logger;

        public CompanyService(ApplicationDbContext context, ILogger<CompanyService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<Tin200>> GetAllCompaniesAsync(int? financialYear = null)
        {
            try
            {
                var query = _context.Tin200.AsQueryable();
                if (financialYear.HasValue && financialYear.Value > 0)
                {
                    var year = financialYear.Value;
                    query = year switch
                    {
                        2025 => query.Where(t => t.FinancialYear == year || (!t.FinancialYear.HasValue && t.Fye2025 != null)),
                        2024 => query.Where(t => t.FinancialYear == year || (!t.FinancialYear.HasValue && t.Fye2024 != null)),
                        2023 => query.Where(t => t.FinancialYear == year || (!t.FinancialYear.HasValue && t.Fye2023 != null)),
                        _ => query.Where(t => t.FinancialYear == year)
                    };
                }
                return await query.OrderByDescending(t => t.Id).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Primary TIN200 query failed.");
                try
                {
                    return await GetAllCompaniesFallbackAsync(financialYear);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Fallback TIN200 query failed.");
                    return new List<Tin200>();
                }
            }
        }

        public async Task<List<int>> GetAvailableFinancialYearsAsync()
        {
            try
            {
                var years = await _context.Tin200
                    .Where(t => t.FinancialYear.HasValue && t.FinancialYear.Value > 0)
                    .Select(t => t.FinancialYear!.Value)
                    .Distinct()
                    .OrderByDescending(y => y)
                    .ToListAsync();

                if (years.Any())
                {
                    return years;
                }

                var fallbackYears = new List<int>();
                if (await _context.Tin200.AnyAsync(t => t.Fye2025 != null)) fallbackYears.Add(2025);
                if (await _context.Tin200.AnyAsync(t => t.Fye2024 != null)) fallbackYears.Add(2024);
                if (await _context.Tin200.AnyAsync(t => t.Fye2023 != null)) fallbackYears.Add(2023);
                return fallbackYears;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read available financial years from TIN200.");
                try
                {
                    var rows = await GetAllCompaniesFallbackAsync();
                    var years = new List<int>();
                    if (rows.Any(r => r.Fye2025 != null)) years.Add(2025);
                    if (rows.Any(r => r.Fye2024 != null)) years.Add(2024);
                    if (rows.Any(r => r.Fye2023 != null)) years.Add(2023);
                    return years;
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Fallback failed to derive financial years from TIN200.");
                    return new List<int>();
                }
            }
        }

        public async Task<Tin200?> GetCompanyByIdAsync(int id)
        {
            try
            {
                return await _context.Tin200.FindAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load TIN200 record by id {Id}.", id);
                try
                {
                    var all = await GetAllCompaniesFallbackAsync();
                    return all.FirstOrDefault(x => x.Id == id);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Fallback failed to load TIN200 record by id {Id}.", id);
                    return null;
                }
            }
        }

        public async Task<Tin200> CreateCompanyAsync(Tin200 company)
        {
            _context.Tin200.Add(company);
            await _context.SaveChangesAsync();
            return company;
        }

        public async Task<Tin200> UpdateCompanyAsync(Tin200 company)
        {
            _context.Tin200.Update(company);
            await _context.SaveChangesAsync();
            return company;
        }

        public async Task DeleteCompanyAsync(int id)
        {
            var company = await GetCompanyByIdAsync(id);
            if (company != null)
            {
                _context.Tin200.Remove(company);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> CompanyExistsAsync(int id)
        {
            return await _context.Tin200.AnyAsync(t => t.Id == id);
        }

        private async Task<List<Tin200>> GetAllCompaniesFallbackAsync(int? financialYear = null)
        {
            var map = await GetCompanyColumnMapAsync();
            var rows = new List<Tin200>();

            var db = _context.Database.GetDbConnection();
            var shouldClose = db.State != ConnectionState.Open;
            if (shouldClose)
            {
                await db.OpenAsync();
            }

            try
            {
                using var cmd = db.CreateCommand();

                var sql = $@"
SELECT
    [{map["Id"]}] AS Id,
    [{map["CeoFirstName"]}] AS CeoFirstName,
    [{map["CeoLastName"]}] AS CeoLastName,
    [{map["Email"]}] AS Email,
    [{map["ExternalId"]}] AS ExternalId,
    [{map["CompanyName"]}] AS CompanyName,
    [{map["CompanyDescription"]}] AS CompanyDescription,
    [{map["Fye2025"]}] AS Fye2025,
    [{map["Fye2024"]}] AS Fye2024,
    [{map["Fye2023"]}] AS Fye2023
FROM [Company]";

                if (financialYear.HasValue)
                {
                    var yearColumn = financialYear.Value switch
                    {
                        2025 => map["Fye2025"],
                        2024 => map["Fye2024"],
                        2023 => map["Fye2023"],
                        _ => string.Empty
                    };

                    if (!string.IsNullOrWhiteSpace(yearColumn))
                    {
                        sql += $" WHERE [{yearColumn}] IS NOT NULL";
                    }
                }

                sql += $" ORDER BY [{map["Id"]}] DESC";
                cmd.CommandText = sql;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new Tin200
                    {
                        Id = GetInt32(reader, "Id"),
                        CeoFirstName = GetString(reader, "CeoFirstName"),
                        CeoLastName = GetString(reader, "CeoLastName"),
                        Email = GetString(reader, "Email"),
                        ExternalId = GetString(reader, "ExternalId"),
                        CompanyName = GetString(reader, "CompanyName"),
                        CompanyDescription = GetString(reader, "CompanyDescription"),
                        Fye2025 = GetDecimal(reader, "Fye2025"),
                        Fye2024 = GetDecimal(reader, "Fye2024"),
                        Fye2023 = GetDecimal(reader, "Fye2023")
                    });
                }

                return rows;
            }
            finally
            {
                if (shouldClose)
                {
                    await db.CloseAsync();
                }
            }
        }

        private async Task<Dictionary<string, string>> GetCompanyColumnMapAsync()
        {
            var db = _context.Database.GetDbConnection();
            var shouldClose = db.State != ConnectionState.Open;
            if (shouldClose)
            {
                await db.OpenAsync();
            }

            try
            {
                using var cmd = db.CreateCommand();
                cmd.CommandText = @"
SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'Company'";

                var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    existing.Add(reader.GetString(0));
                }

                string Pick(params string[] names)
                {
                    foreach (var name in names)
                    {
                        if (existing.Contains(name))
                        {
                            return name;
                        }
                    }
                    throw new InvalidOperationException($"None of the expected columns were found: {string.Join(", ", names)}");
                }

                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Id"] = Pick("Id"),
                    ["CeoFirstName"] = Pick("CEOFirstName", "CeoFirstName", "CEO First Name", "CEO First Name "),
                    ["CeoLastName"] = Pick("CEOLastName", "CeoLastName", "CEO Last Name", "CEO Last Name "),
                    ["Email"] = Pick("Email", "Email "),
                    ["ExternalId"] = Pick("ExternalID", "ExternalId", "External ID"),
                    ["CompanyName"] = Pick("CompanyName", "Company Name"),
                    ["CompanyDescription"] = Pick("CompanyDescription", "Company Description"),
                    ["Fye2025"] = Pick("FYE2025", "Fye2025", "FYE 2025"),
                    ["Fye2024"] = Pick("FYE2024", "Fye2024", "FYE 2024"),
                    ["Fye2023"] = Pick("FYE2023", "Fye2023", "FYE 2023")
                };
            }
            finally
            {
                if (shouldClose)
                {
                    await db.CloseAsync();
                }
            }
        }

        private static string? GetString(IDataRecord record, string name)
        {
            var ordinal = record.GetOrdinal(name);
            return record.IsDBNull(ordinal) ? null : record.GetString(ordinal);
        }

        private static int GetInt32(IDataRecord record, string name)
        {
            var ordinal = record.GetOrdinal(name);
            return record.GetInt32(ordinal);
        }

        private static decimal? GetDecimal(IDataRecord record, string name)
        {
            var ordinal = record.GetOrdinal(name);
            if (record.IsDBNull(ordinal))
            {
                return null;
            }

            return Convert.ToDecimal(record.GetValue(ordinal));
        }
    }

    public class Tin200Service : CompanyService
    {
        public Tin200Service(ApplicationDbContext context, ILogger<CompanyService> logger)
            : base(context, logger)
        {
        }

        public Task<List<Tin200>> GetAllTin200Async(int? financialYear = null) => GetAllCompaniesAsync(financialYear);
        public Task<Tin200?> GetTin200ByIdAsync(int id) => GetCompanyByIdAsync(id);
        public Task<Tin200> CreateTin200Async(Tin200 tin200) => CreateCompanyAsync(tin200);
        public Task<Tin200> UpdateTin200Async(Tin200 tin200) => UpdateCompanyAsync(tin200);
        public Task DeleteTin200Async(int id) => DeleteCompanyAsync(id);
        public Task<bool> Tin200ExistsAsync(int id) => CompanyExistsAsync(id);
    }
}
