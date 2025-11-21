using Microsoft.AspNetCore.Mvc;
using CMCSWeb.Data;
using CMCSWeb.Models;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System;
using CMCSWeb.Services.Interfaces;

namespace CMCSWeb.Controllers
{
    [Authorize(Roles = "Manager")]
    public class ManagerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IApprovalWorkflowService _workflowService;

        public ManagerController(ApplicationDbContext context, IApprovalWorkflowService workflowService)
        {
            _context = context;
            _workflowService = workflowService;
        }

        [HttpGet]
        public async Task<IActionResult> Manage()
        {
            HttpContext.Session.SetString("UserRole", "Manager");
            HttpContext.Session.SetString("UserName", User.Identity.Name);

            var verifiedClaims = await _context.Claims
                .Include(c => c.User)
                .Include(c => c.Workflow) // Include workflow data
                .Where(c => c.Status == ClaimStatus.Verified)
                .OrderByDescending(c => c.VerifiedAt)
                .ToListAsync();

            return View(verifiedClaims);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, string comments = "")
        {
            if (HttpContext.Session.GetString("UserRole") != "Manager")
            {
                TempData["ErrorMessage"] = "Access denied. Please log in as Manager.";
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
                    CurrentStage = "Verified",
                    NextApproverRole = "Manager",
                    IsAutomaticallyApproved = false,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    WorkflowNotes = "Workflow created for existing verified claim"
                };
                _context.ApprovalWorkflows.Add(existingWorkflow);
                await _context.SaveChangesAsync();
            }

            // Use workflow service for approval
            var workflowResult = await _workflowService.ProcessManagerReviewAsync(
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
            if (HttpContext.Session.GetString("UserRole") != "Manager")
            {
                TempData["ErrorMessage"] = "Access denied. Please log in as Manager.";
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
                    CurrentStage = "Verified",
                    NextApproverRole = "Manager",
                    IsAutomaticallyApproved = false,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    WorkflowNotes = "Workflow created for existing verified claim"
                };
                _context.ApprovalWorkflows.Add(existingWorkflow);
                await _context.SaveChangesAsync();
            }

            // Use workflow service for rejection
            var workflowResult = await _workflowService.ProcessManagerReviewAsync(
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

        [HttpGet]
        public async Task<IActionResult> Approved()
        {
            var approvedClaims = await _context.Claims
                .Include(c => c.User)
                .Include(c => c.Workflow)
                .Where(c => c.Status == ClaimStatus.Approved)
                .OrderByDescending(c => c.ApprovedAt)
                .ToListAsync();

            return View(approvedClaims);
        }

        // New: Dashboard with automation statistics
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var totalClaims = await _context.Claims.CountAsync();
            var autoApprovedClaims = await _context.ApprovalWorkflows
                .CountAsync(w => w.IsAutomaticallyApproved);
            var pendingClaims = await _context.Claims
                .CountAsync(c => c.Status == ClaimStatus.Pending);
            var approvedClaims = await _context.Claims
                .CountAsync(c => c.Status == ClaimStatus.Approved);

            ViewBag.TotalClaims = totalClaims;
            ViewBag.AutoApprovedClaims = autoApprovedClaims;
            ViewBag.PendingClaims = pendingClaims;
            ViewBag.ApprovedClaims = approvedClaims;
            ViewBag.AutoApprovalRate = totalClaims > 0 ?
                (double)autoApprovedClaims / totalClaims * 100 : 0;

            return View();
        }
    }
}