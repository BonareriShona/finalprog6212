using CMCSWeb.Models;

namespace CMCSWeb.Services.Interfaces
{
    public interface IClaimValidationService
    {
        Task<ValidationResult> ValidateClaimAsync(Claim claim);
        Task<bool> CheckHoursWorkedAsync(Claim claim);
        Task<bool> CheckHourlyRateAsync(Claim claim);
        Task<bool> CheckTotalAmountAsync(Claim claim);
        Task<bool> IsClaimWithinPolicyAsync(Claim claim);
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public bool CanAutoApprove { get; set; }
    }
}