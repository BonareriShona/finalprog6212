using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CMCSWeb.Services;
using CMCSWeb.Models;
using System.Threading.Tasks;

namespace CMCSWeb.Controllers
{
    [Authorize(Roles = "HR,Manager,Coordinator")]
    public class ReportController : Controller
    {
        private readonly ReportService _reportService;

        public ReportController(ReportService reportService)
        {
            _reportService = reportService;
        }

        // GET: Report/Index
        public IActionResult Index()
        {
            return View(new ReportParameters
            {
                StartDate = DateTime.Now.AddMonths(-1),
                EndDate = DateTime.Now
            });
        }

        // POST: Report/Generate
        [HttpPost]
        public async Task<IActionResult> Generate(ReportParameters parameters)
        {
            if (!ModelState.IsValid)
            {
                return View("Index", parameters);
            }

            var pdfBytes = await _reportService.GenerateClaimsReportAsync(parameters);

            if (pdfBytes == null || pdfBytes.Length == 0)
            {
                TempData["ErrorMessage"] = "No data found for the selected criteria.";
                return View("Index", parameters);
            }

            var fileName = $"ClaimsReport_{DateTime.Now:yyyyMMddHHmmss}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        // GET: Report/Invoice/5
        public async Task<IActionResult> Invoice(int id)
        {
            var pdfBytes = await _reportService.GenerateInvoiceAsync(id);

            if (pdfBytes == null)
            {
                TempData["ErrorMessage"] = "Claim not found.";
                return RedirectToAction("Index");
            }

            var fileName = $"Invoice_CMCS-{id:00000}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        // GET: Report/Statistics
        public async Task<IActionResult> Statistics()
        {
            // You can add statistical reports here
            return View();
        }
    }
}