using System.ComponentModel.DataAnnotations;

namespace TINWeb.Models
{
    public enum TaskItemStatus
    {
        Active = 0,
        Pending = 1,
        Completed = 2,
        Archived = 3
    }

    public class TaskItem
    {
        public int Id { get; set; }

        [Display(Name = "Created by")]
        [StringLength(255)]
        public string? CreatedBy { get; set; }

        [Display(Name = "Created date/time")]
        public DateTime? CreatedDatetime { get; set; }

        [Display(Name = "Status")]
        public TaskItemStatus? Status { get; set; } = TaskItemStatus.Active;

        [Display(Name = "Title")]
        [StringLength(255)]
        public string? Title { get; set; }

        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Completed by")]
        [StringLength(255)]
        public string? CompletedBy { get; set; }

        [Display(Name = "Completed date/time")]
        public DateTime? CompletedDatetime { get; set; }
    }
}
