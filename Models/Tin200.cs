using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TINWeb.Models
{
    [Table("TIN200")]
    public class Tin200
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "CEO First Name")]
        [StringLength(255)]
        public string? CeoFirstName { get; set; }

        [Display(Name = "CEO Last Name")]
        [StringLength(255)]
        public string? CeoLastName { get; set; }

        [Display(Name = "Contact First Name")]
        [StringLength(255)]
        public string? ContactFirstName { get; set; }

        [Display(Name = "Contact Last Name")]
        [StringLength(255)]
        public string? ContactLastName { get; set; }

        [Display(Name = "Contact Email")]
        [StringLength(255)]
        [EmailAddress]
        public string? ContactEmail { get; set; }

        [Display(Name = "Email")]
        [StringLength(255)]
        [EmailAddress]
        public string? Email { get; set; }

        [Display(Name = "External ID")]
        [StringLength(50)]
        public string? ExternalId { get; set; }

        [Display(Name = "Company Name")]
        [StringLength(255)]
        public string? CompanyName { get; set; }

        [Display(Name = "Company Description")]
        [StringLength(255)]
        public string? CompanyDescription { get; set; }

        [Display(Name = "Street")]
        [StringLength(255)]
        public string? AddStreet { get; set; }

        [Display(Name = "Suburb")]
        [StringLength(255)]
        public string? AddSuburb { get; set; }

        [Display(Name = "City")]
        [StringLength(50)]
        public string? AddCity { get; set; }

        [Display(Name = "Postcode")]
        [StringLength(50)]
        public string? AddPostcode { get; set; }

        [Display(Name = "Phone")]
        [StringLength(50)]
        public string? Phone { get; set; }

        [Display(Name = "Website")]
        [StringLength(255)]
        public string? Website { get; set; }

        [Display(Name = "External ID Import Column Name")]
        [StringLength(255)]
        public string? ExternalIdImportColumnName { get; set; }

        [Display(Name = "External ID Import Column Name Alt")]
        [StringLength(255)]
        public string? ExternalIdImportColumnNameAlt { get; set; }

        [Display(Name = "Company Name Import Column Name")]
        [StringLength(255)]
        public string? CompanyNameImportColumnName { get; set; }

        [Display(Name = "Company Description Import Column Name")]
        [StringLength(255)]
        public string? CompanyDescriptionImportColumnName { get; set; }

        [Display(Name = "FYE Last Financial Year")]
        [Column(TypeName = "decimal(18, 0)")]
        public decimal? Fye2025 { get; set; }

        [Display(Name = "FYE Year-1")]
        [Column(TypeName = "decimal(18, 0)")]
        public decimal? Fye2024 { get; set; }

        [Display(Name = "FYE Year-2")]
        [Column(TypeName = "decimal(18, 0)")]
        public decimal? Fye2023 { get; set; }

        [Display(Name = "Financial Year")]
        public int? FinancialYear { get; set; }

        [Display(Name = "Last TIN200 Year")]
        public int? LastTIN200Year { get; set; }

        [Display(Name = "TIN Status")]
        [Column("TINStatus")]
        public int? TinStatus { get; set; }
    }
}
