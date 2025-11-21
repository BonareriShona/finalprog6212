using CMCSWeb.Models;
using CMCSWeb.Services.Interfaces;

namespace CMCSWeb.Services
{
    public class ClaimValidationService : IClaimValidationService
    {
        private readonly IPolicyService _policyService;

        public ClaimValidationService(IPolicyService policyService)
        {
            _policyService = policyService;
        }

        public async Task<ValidationResult> ValidateClaimAsync(Claim claim)
        {
            var result = new ValidationResult();

            // Check hours worked
            if (!await CheckHoursWorkedAsync(claim))
            {
                var maxHours = await _policyService.GetMaxHoursPerClaimAsync();
                result.Errors.Add($"Hours worked ({claim.HoursWorked}) exceeds maximum allowed ({maxHours} hours).");
            }

            // Check hourly rate
            if (!await CheckHourlyRateAsync(claim))
            {
                var allowedRates = await _policyService.GetAllowedHourlyRatesAsync();
                result.Errors.Add($"Hourly rate (R{claim.HourlyRate}) is not allowed. Allowed rates: R{string.Join(", R", allowedRates)}.");
            }

            // Check total amount
            if (!await CheckTotalAmountAsync(claim))
            {
                var maxAmount = await _policyService.GetMaxAmountPerClaimAsync();
                result.Errors.Add($"Total amount (R{claim.TotalAmount}) exceeds maximum per claim (R{maxAmount}).");
            }

            // Check budget - using UserId instead of LecturerId
            if (!await _policyService.IsWithinBudgetAsync(claim.UserId, claim.TotalAmount))
            {
                result.Errors.Add("Claim amount exceeds monthly budget.");
            }

            // Check if claim can be auto-approved
            var policies = await _policyService.GetCurrentPoliciesAsync();
            result.CanAutoApprove = policies.AutoApproveSmallClaims &&
                                   claim.TotalAmount <= policies.AutoApproveThreshold;

            result.IsValid = !result.Errors.Any();
            return result;
        }

        public async Task<bool> CheckHoursWorkedAsync(Claim claim)
        {
            var maxHours = await _policyService.GetMaxHoursPerClaimAsync();
            return claim.HoursWorked <= maxHours && claim.HoursWorked > 0;
        }

        public async Task<bool> CheckHourlyRateAsync(Claim claim)
        {
            var allowedRates = await _policyService.GetAllowedHourlyRatesAsync();
            return allowedRates.Contains(claim.HourlyRate);
        }

        public async Task<bool> CheckTotalAmountAsync(Claim claim)
        {
            var maxAmount = await _policyService.GetMaxAmountPerClaimAsync();
            return claim.TotalAmount <= maxAmount;
        }

        public async Task<bool> IsClaimWithinPolicyAsync(Claim claim)
        {
            var validation = await ValidateClaimAsync(claim);
            return validation.IsValid;
        }
    }
}