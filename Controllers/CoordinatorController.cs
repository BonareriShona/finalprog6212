using Microsoft.AspNetCore.Mvc;
using CMCSWeb.Data;
using CMCSWeb.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System;
using CMCSWeb.Services.Interfaces;

namespace CMCSWeb.Controllers
{
    [Authorize(Roles = "Coordinator")]
    public class CoordinatorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IApprovalWorkflowService _workflowService;

        public CoordinatorController(ApplicationDbContext context, IApprovalWorkflowService workflowService)
        {
            _context = context;
            _workflowService = workflowService;
        }

        [HttpGet]
        public async Task<IActionResult> Manage()
        {
            HttpContext.Session.SetString("UserRole", "Coordinator");
            HttpContext.Session.SetString("UserName", User.Identity.Name ?? "Coordinator");

            var pendingClaims = await _context.Claims
                .Include(c => c.User)
                .Include(c => c.Workflow) // Include workflow data
                .Where(c => c.Status == ClaimStatus.Pending)
                .OrderByDescending(c => c.SubmittedAt)
                .ToListAsync();

            if (!pendingClaims.Any())
                ViewBag.InfoMessage = "There are no pending claims awaiting verification.";

            return View(pendingClaims);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify(int id, string comments = "")
        {
            if (HttpContext.Session.GetString("UserRole") != "Coordinator")
            {
                TempData["ErrorMessage"] = "Access denied. Please log in as Coordinator.";
                return RedirectToAction("Login", "Account");
            }

            var claim = await _context.Claims
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (claim == null)
            {
                TempData["ErrorMessage"] = "Claim not found.";
                return RedirectToAction(nameof(Manage));
            }

            // Check if workflow exists, if not create one
            var existingWorkflow = await _context.ApprovalWorkflows
                .FirstOrDefaultAsync(w => w.ClaimId == id);

            if (existingWorkflow == null)
            {
                // Create workflow for existing claim
                existingWorkflow = new ApprovalWorkflow
                {
                    ClaimId = id,
                    CurrentStage = "UnderReview",
                    NextApproverRole = "Coordinator",
                    IsAutomaticallyApproved = false,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    WorkflowNotes = "Workflow created for existing claim"
                };
                _context.ApprovalWorkflows.Add(existingWorkflow);
                await _context.SaveChangesAsync();
            }

            // Use workflow service instead of direct database update
            var workflowResult = await _workflowService.ProcessCoordinatorReviewAsync(
                id, User.Identity.Name, true, comments);

            if (workflowResult.Success)
            {
                TempData["SuccessMessage"] = workflowResult.Message;
            }
            else
            {
                TempData["ErrorMessage"] = workflowResult.Message;
            }

            return RedirectToAction(nameof(Manage));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string comments = "")
        {
            if (HttpContext.Session.GetString("UserRole") != "Coordinator")
            {
                TempData["ErrorMessage"] = "Access denied. Please log in as Coordinator.";
                return RedirectToAction("Login", "Account");
            }

            var claim = await _context.Claims
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (claim == null)
            {
                TempData["ErrorMessage"] = "Claim not found.";
                return RedirectToAction(nameof(Manage));
            }

            // Check if workflow exists, if not create one
            var existingWorkflow = await _context.ApprovalWorkflows
                .FirstOrDefaultAsync(w => w.ClaimId == id);

            if (existingWorkflow == null)
            {
                // Create workflow for existing claim
                existingWorkflow = new ApprovalWorkflow
                {
                    ClaimId = id,
                    CurrentStage = "UnderReview",
                    NextApproverRole = "Coordinator",
                    IsAutomaticallyApproved = false,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    WorkflowNotes = "Workflow created for existing claim"
                };
                _context.ApprovalWorkflows.Add(existingWorkflow);
                await _context.SaveChangesAsync();
            }

            // Use workflow service for rejection
            var workflowResult = await _workflowService.ProcessCoordinatorReviewAsync(
                id, User.Identity.Name, false, comments);

            if (workflowResult.Success)
            {
                TempData["ErrorMessage"] = workflowResult.Message;
            }
            else
            {
                TempData["ErrorMessage"] = workflowResult.Message;
            }

            return RedirectToAction(nameof(Manage));
        }

        // New: View workflow history for a claim
        [HttpGet]
        public async Task<IActionResult> WorkflowHistory(int claimId)
        {
            var workflow = await _context.ApprovalWorkflows
                .Include(w => w.Claim)
                .Include(w => w.History)
                .FirstOrDefaultAsync(w => w.ClaimId == claimId);

            if (workflow == null)
            {
                TempData["ErrorMessage"] = "Workflow not found for this claim.";
                return RedirectToAction(nameof(Manage));
            }

            return View(workflow);
        }
    }
}