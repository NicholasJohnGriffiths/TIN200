using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TINWeb.Models
{
    [Table("QuestionSubgroupQuestion")]
    public class QuestionSubgroupQuestion
    {
        [Key]
        public int Id { get; set; }

        public int QuestionSubgroupId { get; set; }

        public int QuestionId { get; set; }

        public int? OrderNumber { get; set; }
    }
}
