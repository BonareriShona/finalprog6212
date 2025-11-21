using Microsoft.AspNetCore.Mvc;
using CMCSWeb.Data;
using CMCSWeb.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using CMCSWeb.Services.Interfaces;
using CMCSWeb.Validators;
using FluentValidation;

namespace CMCSWeb.Controllers
{
    [Authorize]
    public class ClaimController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IClaimValidationService _validationService;
        private readonly IApprovalWorkflowService _workflowService;
        private readonly IValidator<Claim> _validator;

        public ClaimController(ApplicationDbContext context,
                             UserManager<ApplicationUser> userManager,
                             IClaimValidationService validationService,
                             IApprovalWorkflowService workflowService,
                             IValidator<Claim> validator)
        {
            _context = context;
            _userManager = userManager;
            _validationService = validationService;
            _workflowService = workflowService;
            _validator = validator;
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var claim = new Claim
            {
                UserId = user.Id,
                HourlyRate = (double)(user.HourlyRate ?? 0)
            };

            ViewBag.UserName = user.FullName;
            ViewBag.UserHourlyRate = user.HourlyRate;

            return View(claim);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Claim claim, IFormFile document)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // 1. FluentValidation
            var validationResult = await _validator.ValidateAsync(claim);
            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                {
                    ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                }

                ViewBag.UserName = user.FullName;
                ViewBag.UserHourlyRate = user.HourlyRate;
                return View(claim);
            }

            // 2. Business Logic Validation
            var businessValidation = await _validationService.ValidateClaimAsync(claim);
            if (!businessValidation.IsValid)
            {
                foreach (var error in businessValidation.Errors)
                {
                    ModelState.AddModelError("", error);
                }

                ViewBag.UserName = user.FullName;
                ViewBag.UserHourlyRate = user.HourlyRate;
                return View(claim);
            }

            if (ModelState.IsValid)
            {
                // Auto-populate user data
                claim.UserId = user.Id;
                claim.Status = ClaimStatus.Pending;
                claim.SubmittedAt = DateTime.Now;
                claim.ClaimMonth = DateTime.Now.Month;
                claim.ClaimYear = DateTime.Now.Year;

                // Handle file upload
                if (document != null && document.Length > 0)
                {
                    var allowedExtensions = new[] { ".pdf", ".docx", ".xlsx", ".jpg", ".jpeg" };
                    var fileExtension = Path.GetExtension(document.FileName).ToLower();

                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        ModelState.AddModelError("DocumentPath", "Only .pdf, .docx, .xlsx, .jpg, or .jpeg files are allowed.");
                        ViewBag.UserName = user.FullName;
                        ViewBag.UserHourlyRate = user.HourlyRate;
                        return View(claim);
                    }

                    const long maxFileSize = 5 * 1024 * 1024;
                    if (document.Length > maxFileSize)
                    {
                        ModelState.AddModelError("DocumentPath", "File size cannot exceed 5 MB.");
                        ViewBag.UserName = user.FullName;
                        ViewBag.UserHourlyRate = user.HourlyRate;
                        return View(claim);
                    }

                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(document.FileName)}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await document.CopyToAsync(stream);
                    }

                    claim.DocumentPath = uniqueFileName;
                }

                // Save to DB
                _context.Claims.Add(claim);
                await _context.SaveChangesAsync();

                // 3. Process through automated workflow
                var workflowResult = await _workflowService.ProcessClaimSubmissionAsync(claim);

                if (workflowResult.Success)
                {
                    TempData["SuccessMessage"] = workflowResult.Message;
                }
                else
                {
                    TempData["ErrorMessage"] = workflowResult.Message;
                }

                return RedirectToAction(nameof(Status));
            }

            ViewBag.UserName = user.FullName;
            ViewBag.UserHourlyRate = user.HourlyRate;
            return View(claim);
        }

        [HttpGet]
        public async Task<IActionResult> Status()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var claims = await _context.Claims
                .Include(c => c.User)
                .Include(c => c.Workflow) // Include workflow data
                .Where(c => c.UserId == user.Id)
                .OrderByDescending(c => c.SubmittedAt)
                .ToListAsync();

            if (!claims.Any())
            {
                ViewBag.InfoMessage = "You have not submitted any claims yet.";
            }

            return View(claims);
        }
    }
}