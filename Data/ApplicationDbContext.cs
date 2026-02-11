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
                    .HasColumnName("CEO First Name ")
                    .HasColumnType("varchar(255)")
                    .HasMaxLength(255);
                
                entity.Property(e => e.CeoLastName)
                    .HasColumnName("CEO Last Name ")
                    .HasColumnType("varchar(255)")
                    .HasMaxLength(255);
                
                entity.Property(e => e.Email)
                    .HasColumnName("Email ")
                    .HasColumnType("varchar(255)")
                    .HasMaxLength(255);
                
                entity.Property(e => e.ExternalId)
                    .HasColumnName("External ID")
                    .HasColumnType("varchar(50)")
                    .HasMaxLength(50);
                
                entity.Property(e => e.CompanyName)
                    .HasColumnName("Company Name")
                    .HasColumnType("varchar(255)")
                    .HasMaxLength(255);
                
                entity.Property(e => e.CompanyDescription)
                    .HasColumnName("Company Description")
                    .HasColumnType("varchar(255)")
                    .HasMaxLength(255);
                
                entity.Property(e => e.Fye2025)
                    .HasColumnName("FYE 2025")
                    .HasColumnType("decimal(18, 0)");
                
                entity.Property(e => e.Fye2024)
                    .HasColumnName("FYE 2024")
                    .HasColumnType("decimal(18, 0)");
                
                entity.Property(e => e.Fye2023)
                    .HasColumnName("FYE 2023")
                    .HasColumnType("decimal(18, 0)");

                entity.Property(e => e.FinancialYear)
                    .HasColumnName("FinancialYear")
                    .HasColumnType("int");
                
                // TIN200 column exists in the database but is intentionally not mapped to the model
            });
        }
    }
}
