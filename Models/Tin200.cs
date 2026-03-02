using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TINWorkspaceTemp.Models
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

        [Display(Name = "FYE 2025")]
        [Column(TypeName = "decimal(18, 0)")]
        public decimal? Fye2025 { get; set; }

        [Display(Name = "FYE 2024")]
        [Column(TypeName = "decimal(18, 0)")]
        public decimal? Fye2024 { get; set; }

        [Display(Name = "FYE 2023")]
        [Column(TypeName = "decimal(18, 0)")]
        public decimal? Fye2023 { get; set; }

        [Display(Name = "Financial Year")]
        public int? FinancialYear { get; set; }

        // TIN200 identity/data column intentionally not exposed in the model
    }
}
