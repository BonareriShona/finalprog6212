using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMCSWeb.Models
{
    public class ApprovalWorkflow
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ClaimId { get; set; }

        [ForeignKey("ClaimId")]
        public virtual Claim Claim { get; set; }

        [Required]
        [Display(Name = "Current Stage")]
        public string CurrentStage { get; set; } = "Submitted"; // Submitted, UnderReview, Approved, Rejected

        [Display(Name = "Next Approver Role")]
        public string NextApproverRole { get; set; } = "Coordinator"; // Coordinator, Manager

        [Display(Name = "Automatically Approved")]
        public bool IsAutomaticallyApproved { get; set; } = false;

        [Display(Name = "Workflow Created")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Last Updated")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Current Approver User ID")]
        public string? CurrentApproverId { get; set; }

        [Display(Name = "Workflow Notes")]
        [MaxLength(1000)]
        public string? WorkflowNotes { get; set; }

        // Navigation property
        public virtual ICollection<WorkflowHistory> History { get; set; } = new List<WorkflowHistory>();
    }

    public class WorkflowHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ApprovalWorkflowId { get; set; }

        [ForeignKey("ApprovalWorkflowId")]
        public virtual ApprovalWorkflow ApprovalWorkflow { get; set; }

        [Required]
        public string Action { get; set; } // Submitted, Reviewed, Approved, Rejected

        [Required]
        public string PerformedBy { get; set; } // User ID

        [Display(Name = "Performed By Role")]
        public string PerformedByRole { get; set; } // Lecturer, Coordinator, Manager

        [Display(Name = "Action Date")]
        public DateTime ActionDate { get; set; } = DateTime.Now;

        [MaxLength(500)]
        public string? Notes { get; set; }

        public string? PreviousStage { get; set; }
        public string? NewStage { get; set; }
    }
}