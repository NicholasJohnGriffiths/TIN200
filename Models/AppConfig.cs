using System.ComponentModel.DataAnnotations;

namespace TINWeb.Models
{
    public class AppConfig
    {
        public int Id { get; set; }

        [Display(Name = "Admin Email")]
        public string AdminEmail { get; set; } = string.Empty;

        [Display(Name = "Survey Email Subject")]
        [StringLength(255)]
        public string? SurveyEmailSubject { get; set; }

        [Display(Name = "Survey Email Template")]
        [DataType(DataType.MultilineText)]
        public string? SurveyEmailTemplate { get; set; }
    }
}
