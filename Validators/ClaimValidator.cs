using FluentValidation;
using CMCSWeb.Models;

namespace CMCSWeb.Validators
{
    public class ClaimValidator : AbstractValidator<Claim>
    {
        public ClaimValidator()
        {
            RuleFor(x => x.HoursWorked)
                .GreaterThan(0).WithMessage("Hours worked must be greater than 0")
                .LessThanOrEqualTo(180).WithMessage("Hours worked cannot exceed 180 hours");

            RuleFor(x => x.HourlyRate)
                .GreaterThan(0).WithMessage("Hourly rate must be greater than 0")
                .Must(BeAllowedRate).WithMessage("Hourly rate is not in the allowed rates");

            RuleFor(x => x.Notes)
                .MaximumLength(500).WithMessage("Notes cannot exceed 500 characters");

            RuleFor(x => x.TotalAmount)
                .GreaterThan(0).WithMessage("Total amount must be greater than 0");
        }

        private bool BeAllowedRate(double rate)
        {
            var allowedRates = new[] { 150.0, 200.0, 250.0, 300.0 };
            return allowedRates.Contains(rate);
        }
    }
}