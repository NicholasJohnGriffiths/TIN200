using Microsoft.EntityFrameworkCore;
using TINWeb.Models;

namespace TINWeb.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Tin200> Tin200 { get; set; } = null!;
        public DbSet<Survey> Survey { get; set; } = null!;
        public DbSet<CompanySurvey> CompanySurvey { get; set; } = null!;
        public DbSet<Question> Question { get; set; } = null!;
        public DbSet<Answer> Answer { get; set; } = null!;
        public DbSet<CompanyFinancialAnalytics> CompanyFinancialAnalytics { get; set; } = null!;
        public DbSet<FinancialYearComparison> FinancialYearComparison { get; set; } = null!;
        public DbSet<RevenueSummaryBySize> RevenueSummaryBySize { get; set; } = null!;
        public DbSet<TopPerformersAnalytics> TopPerformersAnalytics { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Tin200>(entity =>
            {
                entity.ToTable("Company");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id)
                    .HasColumnName("Id")
                    .ValueGeneratedOnAdd();
                
                entity.Property(e => e.CeoFirstName)
                    .HasColumnName("CEOFirstName")
                    .HasColumnType("varchar(255)")
                    .HasMaxLength(255);
                
                entity.Property(e => e.CeoLastName)
                    .HasColumnName("CEOLastName")
                    .HasColumnType("varchar(255)")
                    .HasMaxLength(255);
                
                entity.Property(e => e.Email)
                    .HasColumnName("Email")
                    .HasColumnType("varchar(255)")
                    .HasMaxLength(255);
                
                entity.Property(e => e.ExternalId)
                    .HasColumnName("ExternalID")
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
                    .HasColumnName("FYE2025")
                    .HasColumnType("decimal(18, 0)");
                
                entity.Property(e => e.Fye2024)
                    .HasColumnName("FYE2024")
                    .HasColumnType("decimal(18, 0)");
                
                entity.Property(e => e.Fye2023)
                    .HasColumnName("FYE2023")
                    .HasColumnType("decimal(18, 0)");

                entity.Property(e => e.FinancialYear)
                    .HasColumnName("FinancialYear")
                    .HasColumnType("int");

                // Company column exists in the database but is intentionally not mapped to the model
            });

            modelBuilder.Entity<Survey>(entity =>
            {
                entity.ToTable("Survey");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id)
                    .HasColumnName("Id")
                    .ValueGeneratedOnAdd();

                entity.Property(e => e.FinancialYear)
                    .HasColumnName("FinancialYear")
                    .HasColumnType("int")
                    .IsRequired();

                entity.Property(e => e.CurrentSurvey)
                    .HasColumnName("CurrentSurvey")
                    .HasColumnType("bit")
                    .IsRequired();
            });

            modelBuilder.Entity<CompanySurvey>(entity =>
            {
                entity.ToTable("CompanySurvey");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("Id")
                    .ValueGeneratedOnAdd();

                entity.Property(e => e.CompanyId)
                    .HasColumnName("CompanyId")
                    .HasColumnType("int")
                    .IsRequired();

                entity.Property(e => e.SurveyId)
                    .HasColumnName("SurveyId")
                    .HasColumnType("int")
                    .IsRequired();

                entity.Property(e => e.Saved)
                    .HasColumnName("Saved")
                    .HasColumnType("bit")
                    .IsRequired();

                entity.Property(e => e.Submitted)
                    .HasColumnName("Submitted")
                    .HasColumnType("bit")
                    .IsRequired();

                entity.Property(e => e.Requested)
                    .HasColumnName("Requested")
                    .HasColumnType("bit")
                    .IsRequired();

                entity.HasOne<Tin200>()
                    .WithMany()
                    .HasForeignKey(e => e.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_CompanySurvey_TIN200");

                entity.HasOne<Survey>()
                    .WithMany()
                    .HasForeignKey(e => e.SurveyId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_CompanySurvey_Survey");
            });

            modelBuilder.Entity<Question>(entity =>
            {
                entity.ToTable("Question");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("Id")
                    .ValueGeneratedOnAdd();

                entity.Property(e => e.Title)
                    .HasColumnName("Title")
                    .HasColumnType("varchar(255)");

                entity.Property(e => e.Description)
                    .HasColumnName("Description")
                    .HasColumnType("varchar(max)");

                entity.Property(e => e.GroupTitle)
                    .HasColumnName("GroupTitle")
                    .HasColumnType("varchar(max)");

                entity.Property(e => e.QuestionText)
                    .HasColumnName("Question")
                    .HasColumnType("varchar(max)");

                entity.Property(e => e.OrderNumber)
                    .HasColumnName("OrderNumber")
                    .HasColumnType("int");

                entity.Property(e => e.Multi1).HasColumnName("Multi_1").HasColumnType("varchar(255)");
                entity.Property(e => e.Multi2).HasColumnName("Multi_2").HasColumnType("varchar(255)");
                entity.Property(e => e.Multi3).HasColumnName("Multi_3").HasColumnType("varchar(255)");
                entity.Property(e => e.Multi4).HasColumnName("Multi_4").HasColumnType("varchar(255)");
                entity.Property(e => e.Multi5).HasColumnName("Multi_5").HasColumnType("varchar(255)");
                entity.Property(e => e.Multi6).HasColumnName("Multi_6").HasColumnType("varchar(255)");
                entity.Property(e => e.Multi7).HasColumnName("Multi_7").HasColumnType("varchar(255)");
                entity.Property(e => e.Multi8).HasColumnName("Multi_8").HasColumnType("varchar(255)");

                entity.Property(e => e.AnswerType)
                    .HasColumnName("AnswerType")
                    .HasColumnType("nvarchar(50)");
            });

            modelBuilder.Entity<Answer>(entity =>
            {
                entity.ToTable("Answer");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("Id")
                    .ValueGeneratedOnAdd();

                entity.Property(e => e.CompanySurveyId)
                    .HasColumnName("CompanySurveyId")
                    .HasColumnType("int")
                    .IsRequired();

                entity.Property(e => e.QuestionId)
                    .HasColumnName("QuestionId")
                    .HasColumnType("int")
                    .IsRequired();

                entity.Property(e => e.AnswerText)
                    .HasColumnName("AnswerText")
                    .HasColumnType("varchar(max)");

                entity.Property(e => e.AnswerCurrency)
                    .HasColumnName("AnswerCurrency")
                    .HasColumnType("money");

                entity.Property(e => e.AnswerNumber)
                    .HasColumnName("AnswerNumber")
                    .HasColumnType("int");

                entity.HasOne<CompanySurvey>()
                    .WithMany()
                    .HasForeignKey(e => e.CompanySurveyId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<Question>()
                    .WithMany()
                    .HasForeignKey(e => e.QuestionId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure view models - no key required for views
            modelBuilder.Entity<CompanyFinancialAnalytics>().HasNoKey().ToView("vw_CompanyFinancialAnalytics");
            modelBuilder.Entity<FinancialYearComparison>().HasNoKey().ToView("vw_FinancialYearComparison");
            modelBuilder.Entity<RevenueSummaryBySize>().HasNoKey().ToView("vw_RevenueSummaryBySize");
            modelBuilder.Entity<TopPerformersAnalytics>().HasNoKey().ToView("vw_TopPerformersAnalytics");
        }
    }
}
