using CMCSWeb.Models;

namespace CMCSWeb.Services.Interfaces
{
    public interface IApprovalWorkflowService
    {
        Task<WorkflowResult> ProcessClaimSubmissionAsync(Claim claim);
        Task<WorkflowResult> ProcessCoordinatorReviewAsync(int claimId, string coordinatorId, bool isApproved, string comments);
        Task<WorkflowResult> ProcessManagerReviewAsync(int claimId, string managerId, bool isApproved, string comments);
        Task<bool> CanAutoApproveAsync(Claim claim);
        Task<string> GetNextApproverRoleAsync(Claim claim);
    }

    public class WorkflowResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string NextAction { get; set; }
        public string NextRole { get; set; }
        public ClaimStatus NewStatus { get; set; }
    }
}