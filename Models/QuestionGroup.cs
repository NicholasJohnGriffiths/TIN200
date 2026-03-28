using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TINWeb.Models
{
    [Table("QuestionGroup")]
    public class QuestionGroup
    {
        [Key]
        public int Id { get; set; }

        public string? Title { get; set; }

        public string? Description { get; set; }

        public int? OrderNumber { get; set; }

        public int? ImageId1 { get; set; }

        public int? ImageId2 { get; set; }

        public int? ImageId3 { get; set; }

        public bool? IncludeInEstimation { get; set; }

        public bool TableFormat { get; set; }
    }
}
