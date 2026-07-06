using System.ComponentModel.DataAnnotations;

namespace TINWeb.Models
{
    public class EmailContent
    {
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Subject { get; set; }

        [DataType(DataType.MultilineText)]
        public string? Template { get; set; }

        public bool Active { get; set; } = true;

        public DateTime CreatedUtc { get; set; }

        [StringLength(255)]
        public string? CreatedBy { get; set; }

        public DateTime UpdatedUtc { get; set; }

        [StringLength(255)]
        public string? UpdatedBy { get; set; }
    }
}
