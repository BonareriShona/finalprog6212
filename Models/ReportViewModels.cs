using System;

namespace CMCSWeb.Models
{
    public class ClaimReportItem
    {
        public int ClaimId { get; set; }
        public string LecturerName { get; set; }
        public double HoursWorked { get; set; }
        public double HourlyRate { get; set; }
        public double TotalAmount { get; set; }
        public DateTime SubmittedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string Status { get; set; }
        public string ApprovedBy { get; set; }
    }

    public class ReportParameters
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Status { get; set; }
        public string ReportType { get; set; } // "Claims", "Approved", "Rejected"
    }
}