using Microsoft.EntityFrameworkCore;
using TINWorkspaceTemp.Models;

namespace TINWorkspaceTemp.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Tin200> Tin200 { get; set; } = null!;
        public DbSet<CompanyFinancialAnalytics> CompanyFinancialAnalytics { get; set; } = null!;
        public DbSet<FinancialYearComparison> FinancialYearComparison { get; set; } = null!;
        public DbSet<RevenueSummaryBySize> RevenueSummaryBySize { get; set; } = null!;
        public DbSet<TopPerformersAnalytics> TopPerformersAnalytics { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Tin200>(entity =>
            {
                entity.ToTable("TIN200");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id)
                    .HasColumnName("Id")
                    .ValueGeneratedOnAdd();
                
                entity.Property(e => e.CeoFirstName)
                    .HasColumnName("CeoFirstName")
                    .HasColumnType("varchar(255)")
                    .HasMaxLength(255);
                
                entity.Property(e => e.CeoLastName)
                    .HasColumnName("CeoLastName")
                    .HasColumnType("varchar(255)")
                    .HasMaxLength(255);
                
                entity.Property(e => e.Email)
                    .HasColumnName("Email")
                    .HasColumnType("varchar(255)")
                    .HasMaxLength(255);
                
                entity.Property(e => e.ExternalId)
                    .HasColumnName("ExternalId")
                    .HasColumnType("varchar(50)")
                    .HasMaxLength(50);
                
                entity.Property(e => e.CompanyName)
                    .HasColumnName("CompanyName")
                    .HasColumnType("varchar(255)")
                    .HasMaxLength(255);
                
                entity.Property(e => e.CompanyDescription)
                    .HasColumnName("CompanyDescription")
                    .HasColumnType("varchar(255)")
                    .HasMaxLength(255);
                
                entity.Property(e => e.Fye2025)
                    .HasColumnName("Fye2025")
                    .HasColumnType("decimal(18, 0)");
                
                entity.Property(e => e.Fye2024)
                    .HasColumnName("Fye2024")
                    .HasColumnType("decimal(18, 0)");
                
                entity.Property(e => e.Fye2023)
                    .HasColumnName("Fye2023")
                    .HasColumnType("decimal(18, 0)");

                // TIN200 column exists in the database but is intentionally not mapped to the model
            });

            // Configure view models - no key required for views
            modelBuilder.Entity<CompanyFinancialAnalytics>().HasNoKey().ToView("vw_CompanyFinancialAnalytics");
            modelBuilder.Entity<FinancialYearComparison>().HasNoKey().ToView("vw_FinancialYearComparison");
            modelBuilder.Entity<RevenueSummaryBySize>().HasNoKey().ToView("vw_RevenueSummaryBySize");
            modelBuilder.Entity<TopPerformersAnalytics>().HasNoKey().ToView("vw_TopPerformersAnalytics");
        }
    }
}
