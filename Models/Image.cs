using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TINWeb.Models
{
    [Table("Image")]
    public class Image
    {
        [Key]
        public int Id { get; set; }

        [StringLength(50)]
        public string EntityType { get; set; } = string.Empty;

        public int EntityId { get; set; }

        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        [StringLength(500)]
        public string FilePath { get; set; } = string.Empty;

        [StringLength(50)]
        public string FileType { get; set; } = string.Empty;

        public int? FileSize { get; set; }

        public DateTime CreatedDate { get; set; }
    }
}
