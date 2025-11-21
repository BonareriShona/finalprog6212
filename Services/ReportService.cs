using CMCSWeb.Data;
using CMCSWeb.Models;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CMCSWeb.Services
{
    public class ReportService
    {
        private readonly ApplicationDbContext _context;

        public ReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<byte[]> GenerateClaimsReportAsync(ReportParameters parameters)
        {
            // LINQ query to get report data
            var query = _context.Claims
                .Include(c => c.User)
                .Include(c => c.Workflow)
                .AsQueryable();

            // Apply filters
            if (parameters.StartDate.HasValue)
                query = query.Where(c => c.SubmittedAt >= parameters.StartDate.Value);

            if (parameters.EndDate.HasValue)
                query = query.Where(c => c.SubmittedAt <= parameters.EndDate.Value);

            if (!string.IsNullOrEmpty(parameters.Status))
                query = query.Where(c => c.Status.ToString() == parameters.Status);

            var reportData = await query
                .Select(c => new ClaimReportItem
                {
                    ClaimId = c.Id,
                    LecturerName = c.User.FullName ?? c.User.Email,
                    HoursWorked = c.HoursWorked,
                    HourlyRate = c.HourlyRate,
                    TotalAmount = c.TotalAmount,
                    SubmittedAt = c.SubmittedAt,
                    ApprovedAt = c.ApprovedAt,
                    Status = c.Status.ToString(),
                    ApprovedBy = c.ApprovedBy ?? "Auto-Approved"
                })
                .OrderByDescending(c => c.SubmittedAt)
                .ToListAsync();

            return GeneratePdfReport(reportData, parameters);
        }

        public async Task<byte[]> GenerateInvoiceAsync(int claimId)
        {
            var claim = await _context.Claims
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == claimId);

            if (claim == null) return null;

            return GeneratePdfInvoice(claim);
        }

        private byte[] GeneratePdfReport(System.Collections.Generic.List<ClaimReportItem> data, ReportParameters parameters)
        {
            using (var memoryStream = new MemoryStream())
            {
                var document = new Document(PageSize.A4, 50, 50, 25, 25);
                var writer = PdfWriter.GetInstance(document, memoryStream);

                document.Open();

                // Title
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18);
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12);
                var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);

                // Report Title
                document.Add(new Paragraph("CLAIMS MANAGEMENT SYSTEM - REPORT", titleFont));
                document.Add(new Paragraph($"Generated on: {System.DateTime.Now:yyyy-MM-dd HH:mm}", normalFont));
                document.Add(new Paragraph($"Total Records: {data.Count}", normalFont));
                document.Add(Chunk.NEWLINE);

                // Create table
                var table = new PdfPTable(8);
                table.WidthPercentage = 100;
                table.SetWidths(new float[] { 1f, 2f, 1f, 1f, 2f, 2f, 2f, 2f });

                // Table headers
                table.AddCell(new PdfPCell(new Phrase("Claim ID", headerFont)));
                table.AddCell(new PdfPCell(new Phrase("Lecturer", headerFont)));
                table.AddCell(new PdfPCell(new Phrase("Hours", headerFont)));
                table.AddCell(new PdfPCell(new Phrase("Rate", headerFont)));
                table.AddCell(new PdfPCell(new Phrase("Amount", headerFont)));
                table.AddCell(new PdfPCell(new Phrase("Submitted", headerFont)));
                table.AddCell(new PdfPCell(new Phrase("Status", headerFont)));
                table.AddCell(new PdfPCell(new Phrase("Approved By", headerFont)));

                // Table data
                foreach (var item in data)
                {
                    table.AddCell(new PdfPCell(new Phrase(item.ClaimId.ToString(), normalFont)));
                    table.AddCell(new PdfPCell(new Phrase(item.LecturerName, normalFont)));
                    table.AddCell(new PdfPCell(new Phrase(item.HoursWorked.ToString("F1"), normalFont)));
                    table.AddCell(new PdfPCell(new Phrase("R" + item.HourlyRate.ToString("F2"), normalFont)));
                    table.AddCell(new PdfPCell(new Phrase("R" + item.TotalAmount.ToString("F2"), normalFont)));
                    table.AddCell(new PdfPCell(new Phrase(item.SubmittedAt.ToString("yyyy-MM-dd"), normalFont)));
                    table.AddCell(new PdfPCell(new Phrase(item.Status, normalFont)));
                    table.AddCell(new PdfPCell(new Phrase(item.ApprovedBy, normalFont)));
                }

                document.Add(table);

                // Summary
                document.Add(Chunk.NEWLINE);
                var totalAmount = data.Sum(x => x.TotalAmount);
                document.Add(new Paragraph($"Total Amount: R{totalAmount:F2}", headerFont));

                document.Close();
                return memoryStream.ToArray();
            }
        }

        private byte[] GeneratePdfInvoice(Claim claim)
        {
            using (var memoryStream = new MemoryStream())
            {
                var document = new Document(PageSize.A4, 50, 50, 25, 25);
                var writer = PdfWriter.GetInstance(document, memoryStream);

                document.Open();

                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12);
                var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);

                // Invoice Header
                document.Add(new Paragraph("INVOICE", titleFont));
                document.Add(new Paragraph($"Invoice #: CMCS-{claim.Id:00000}", normalFont));
                document.Add(new Paragraph($"Date: {System.DateTime.Now:yyyy-MM-dd}", normalFont));
                document.Add(Chunk.NEWLINE);

                // Lecturer Information
                document.Add(new Paragraph("BILL TO:", headerFont));
                document.Add(new Paragraph(claim.User.FullName ?? claim.User.Email, normalFont));
                document.Add(Chunk.NEWLINE);

                // Claim Details Table
                var table = new PdfPTable(2);
                table.WidthPercentage = 100;
                table.SetWidths(new float[] { 1f, 3f });

                table.AddCell(new PdfPCell(new Phrase("Description", headerFont)));
                table.AddCell(new PdfPCell(new Phrase("Amount", headerFont)));

                table.AddCell(new PdfPCell(new Phrase("Hours Worked", normalFont)));
                table.AddCell(new PdfPCell(new Phrase($"{claim.HoursWorked} hours @ R{claim.HourlyRate:F2}/hour", normalFont)));

                table.AddCell(new PdfPCell(new Phrase("Total Amount", headerFont)));
                table.AddCell(new PdfPCell(new Phrase("R" + claim.TotalAmount.ToString("F2"), headerFont)));

                document.Add(table);

                // Notes
                if (!string.IsNullOrEmpty(claim.Notes))
                {
                    document.Add(Chunk.NEWLINE);
                    document.Add(new Paragraph("Notes:", headerFont));
                    document.Add(new Paragraph(claim.Notes, normalFont));
                }

                // Approval Information
                document.Add(Chunk.NEWLINE);
                document.Add(new Paragraph("Approval Details:", headerFont));
                document.Add(new Paragraph($"Status: {claim.Status}", normalFont));
                document.Add(new Paragraph($"Submitted: {claim.SubmittedAt:yyyy-MM-dd}", normalFont));

                if (claim.ApprovedAt.HasValue)
                    document.Add(new Paragraph($"Approved: {claim.ApprovedAt.Value:yyyy-MM-dd}", normalFont));

                document.Close();
                return memoryStream.ToArray();
            }
        }
    }
}