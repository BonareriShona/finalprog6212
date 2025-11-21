namespace CMCSWeb.Services.Interfaces
{
    public interface IPolicyService
    {
        Task<ClaimPolicies> GetCurrentPoliciesAsync();
        Task<double> GetMaxHoursPerClaimAsync();
        Task<double> GetMaxAmountPerClaimAsync();
        Task<double[]> GetAllowedHourlyRatesAsync();
        Task<bool> IsWithinBudgetAsync(string userId, double claimAmount);
    }

    public class ClaimPolicies
    {
        public double MaxHoursPerClaim { get; set; } = 180;
        public double MaxAmountPerClaim { get; set; } = 10000;
        public double[] AllowedHourlyRates { get; set; } = { 150, 200, 250, 300 };
        public double MonthlyBudgetPerUser { get; set; } = 50000;
        public bool AutoApproveSmallClaims { get; set; } = true;
        public double AutoApproveThreshold { get; set; } = 5000;
    }
}