using CMCSWeb.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace CMCSWeb.Services
{
    public class PolicyService : IPolicyService
    {
        private readonly ClaimPolicies _policies;

        public PolicyService(IOptions<ClaimPolicies> policies)
        {
            _policies = policies.Value;
        }

        public Task<ClaimPolicies> GetCurrentPoliciesAsync()
        {
            return Task.FromResult(_policies);
        }

        public Task<double> GetMaxHoursPerClaimAsync()
        {
            return Task.FromResult(_policies.MaxHoursPerClaim);
        }

        public Task<double> GetMaxAmountPerClaimAsync()
        {
            return Task.FromResult(_policies.MaxAmountPerClaim);
        }

        public Task<double[]> GetAllowedHourlyRatesAsync()
        {
            return Task.FromResult(_policies.AllowedHourlyRates);
        }

        public async Task<bool> IsWithinBudgetAsync(string userId, double claimAmount)
        {
            var monthlyBudget = _policies.MonthlyBudgetPerUser;

            // In a real implementation, you would sum all claims for this user this month
            // using your ApplicationDbContext
            double monthlyClaimsTotal = 0; // This would come from database

            return (monthlyClaimsTotal + claimAmount) <= monthlyBudget;
        }
    }
}