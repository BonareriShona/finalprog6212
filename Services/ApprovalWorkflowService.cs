using CMCSWeb.Data;
using CMCSWeb.Models;
using CMCSWeb.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CMCSWeb.Services
{
    public class ApprovalWorkflowService : IApprovalWorkflowService
    {
        private readonly ApplicationDbContext _context;
        private readonly IClaimValidationService _validationService;

        public ApprovalWorkflowService(ApplicationDbContext context, IClaimValidationService validationService)
        {
            _context = context;
            _validationService = validationService;
        }

        public async Task<WorkflowResult> ProcessClaimSubmissionAsync(Claim claim)
        {
            // Create workflow record
            var workflow = new ApprovalWorkflow
            {
                ClaimId = claim.Id,
                CurrentStage = "Submitted",
                NextApproverRole = "Coordinator",
                IsAutomaticallyApproved = false,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            // Validate the claim
            var validation = await _validationService.ValidateClaimAsync(claim);

            if (!validation.IsValid)
            {
                workflow.CurrentStage = "Rejected";
                workflow.NextApproverRole = "None"; // Changed from null to "None"
                workflow.WorkflowNotes = $"Auto-rejected: {string.Join(", ", validation.Errors)}";

                await _context.ApprovalWorkflows.AddAsync(workflow);
                await _context.SaveChangesAsync();

                return new WorkflowResult
                {
                    Success = false,
                    Message = "Claim validation failed",
                    NewStatus = ClaimStatus.Rejected
                };
            }

            // Check for auto-approval
            if (validation.CanAutoApprove)
            {
                workflow.IsAutomaticallyApproved = true;
                workflow.CurrentStage = "Approved";
                workflow.NextApproverRole = "None"; // Changed from null to "None"
                workflow.WorkflowNotes = "Auto-approved: Claim under threshold";

                claim.Status = ClaimStatus.Approved;
                claim.ApprovedAt = DateTime.Now;
            }
            else
            {
                workflow.CurrentStage = "UnderReview";
                workflow.NextApproverRole = "Coordinator";
                workflow.WorkflowNotes = "Pending coordinator review";

                claim.Status = ClaimStatus.Pending;
            }

            await _context.ApprovalWorkflows.AddAsync(workflow);
            await _context.SaveChangesAsync();

            // Add to history
            await AddWorkflowHistoryAsync(workflow.Id, "Submitted", claim.UserId, "Lecturer",
                validation.CanAutoApprove ? "Auto-approved" : "Sent for review");

            return new WorkflowResult
            {
                Success = true,
                Message = validation.CanAutoApprove ? "Claim auto-approved" : "Claim submitted for review",
                NextAction = validation.CanAutoApprove ? "Ready for payment" : "Pending coordinator review",
                NextRole = validation.CanAutoApprove ? "None" : "Coordinator", // Changed from null to "None"
                NewStatus = validation.CanAutoApprove ? ClaimStatus.Approved : ClaimStatus.Pending
            };
        }

        public async Task<WorkflowResult> ProcessCoordinatorReviewAsync(int claimId, string coordinatorId, bool isApproved, string comments)
        {
            var workflow = await _context.ApprovalWorkflows
                .Include(w => w.Claim)
                .FirstOrDefaultAsync(w => w.ClaimId == claimId);

            if (workflow == null)
            {
                return new WorkflowResult { Success = false, Message = "Workflow not found" };
            }

            var claim = workflow.Claim;

            if (!isApproved)
            {
                // Reject the claim
                workflow.CurrentStage = "Rejected";
                workflow.NextApproverRole = "None"; // Changed from null to "None"
                workflow.UpdatedAt = DateTime.Now;
                workflow.WorkflowNotes = $"Rejected by coordinator: {comments}";

                claim.Status = ClaimStatus.Rejected;
                claim.VerifiedBy = coordinatorId;
                claim.VerifiedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                await AddWorkflowHistoryAsync(workflow.Id, "Rejected", coordinatorId, "Coordinator", comments);

                return new WorkflowResult
                {
                    Success = true,
                    Message = "Claim rejected",
                    NewStatus = ClaimStatus.Rejected
                };
            }

            // Claim approved by coordinator
            workflow.CurrentStage = "Verified";
            workflow.NextApproverRole = "Manager";
            workflow.UpdatedAt = DateTime.Now;
            workflow.WorkflowNotes = $"Verified by coordinator: {comments}";

            claim.Status = ClaimStatus.Verified;
            claim.VerifiedBy = coordinatorId;
            claim.VerifiedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            await AddWorkflowHistoryAsync(workflow.Id, "Verified", coordinatorId, "Coordinator", comments);

            return new WorkflowResult
            {
                Success = true,
                Message = "Claim verified, sent to manager for final approval",
                NextAction = "Pending manager approval",
                NextRole = "Manager",
                NewStatus = ClaimStatus.Verified
            };
        }

        public async Task<WorkflowResult> ProcessManagerReviewAsync(int claimId, string managerId, bool isApproved, string comments)
        {
            var workflow = await _context.ApprovalWorkflows
                .Include(w => w.Claim)
                .FirstOrDefaultAsync(w => w.ClaimId == claimId);

            if (workflow == null)
            {
                return new WorkflowResult { Success = false, Message = "Workflow not found" };
            }

            var claim = workflow.Claim;

            if (!isApproved)
            {
                // Reject the claim
                workflow.CurrentStage = "Rejected";
                workflow.NextApproverRole = "None"; // Changed from null to "None"
                workflow.UpdatedAt = DateTime.Now;
                workflow.WorkflowNotes = $"Rejected by manager: {comments}";

                claim.Status = ClaimStatus.Rejected;
                claim.ApprovedBy = managerId;
                claim.ApprovedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                await AddWorkflowHistoryAsync(workflow.Id, "Rejected", managerId, "Manager", comments);

                return new WorkflowResult
                {
                    Success = true,
                    Message = "Claim rejected by manager",
                    NewStatus = ClaimStatus.Rejected
                };
            }

            // Final approval by manager
            workflow.CurrentStage = "Approved";
            workflow.NextApproverRole = "None"; // Changed from null to "None"
            workflow.UpdatedAt = DateTime.Now;
            workflow.WorkflowNotes = $"Approved by manager: {comments}";

            claim.Status = ClaimStatus.Approved;
            claim.ApprovedBy = managerId;
            claim.ApprovedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            await AddWorkflowHistoryAsync(workflow.Id, "Approved", managerId, "Manager", comments);

            return new WorkflowResult
            {
                Success = true,
                Message = "Claim fully approved",
                NextAction = "Ready for payment processing",
                NewStatus = ClaimStatus.Approved
            };
        }

        public async Task<bool> CanAutoApproveAsync(Claim claim)
        {
            var validation = await _validationService.ValidateClaimAsync(claim);
            return validation.CanAutoApprove;
        }

        public async Task<string> GetNextApproverRoleAsync(Claim claim)
        {
            var workflow = await _context.ApprovalWorkflows
                .FirstOrDefaultAsync(w => w.ClaimId == claim.Id);

            return workflow?.NextApproverRole ?? "Coordinator";
        }

        private async Task AddWorkflowHistoryAsync(int workflowId, string action, string performedBy, string role, string notes)
        {
            var history = new WorkflowHistory
            {
                ApprovalWorkflowId = workflowId,
                Action = action,
                PerformedBy = performedBy,
                PerformedByRole = role,
                ActionDate = DateTime.Now,
                Notes = notes
            };

            await _context.WorkflowHistories.AddAsync(history);
            await _context.SaveChangesAsync();
        }
    }
}