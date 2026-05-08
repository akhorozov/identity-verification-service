namespace AddressValidation.Api.Features.Validation.ValidateBatch;

using FluentValidation;
using AddressValidation.Api.Features.Validation.ValidateSingle;

/// <summary>
/// FluentValidation validator for <see cref="ValidateBatchRequest"/>.
/// SRS Ref: FR-002, Section 9.3.2 — Batch input validation rules:
///   - Addresses array: required, 1–100 items
///   - Each item: reuses single-address validation rules via <see cref="ValidateSingleRequestValidator"/>
/// </summary>
public sealed class ValidateBatchRequestValidator : AbstractValidator<ValidateBatchRequest>
{
    public ValidateBatchRequestValidator()
    {
        RuleFor(x => x.Addresses)
            .NotNull().WithMessage("Addresses array is required.")
            .Must(a => a.Length >= 1).WithMessage("At least one address must be provided.")
            .Must(a => a.Length <= 100).WithMessage("A maximum of 100 addresses may be submitted per request.");

        RuleForEach(x => x.Addresses)
            .ChildRules(item =>
            {
                item.RuleFor(x => x.Street)
                    .NotEmpty().WithMessage("Street address is required.")
                    .MaximumLength(100).WithMessage("Street must not exceed 100 characters.");

                item.RuleFor(x => x.Street2)
                    .MaximumLength(100).WithMessage("Street2 must not exceed 100 characters.")
                    .When(x => x.Street2 is not null);

                item.RuleFor(x => x.City)
                    .MaximumLength(50).WithMessage("City must not exceed 50 characters.")
                    .When(x => x.City is not null);

                item.RuleFor(x => x.State)
                    .Length(2).WithMessage("State must be exactly 2 characters.")
                    .Must(s => ValidStates.Contains(s!.ToUpperInvariant()))
                    .WithMessage("State must be a valid US state or territory abbreviation.")
                    .When(x => x.State is not null);

                item.RuleFor(x => x.ZipCode)
                    .Matches(@"^\d{5}(-\d{4})?$").WithMessage("ZipCode must be in format 12345 or 12345-6789.")
                    .When(x => x.ZipCode is not null);

                item.RuleFor(x => x.Plus4)
                    .Matches(@"^\d{4}$").WithMessage("Plus4 must be exactly 4 digits.")
                    .When(x => x.Plus4 is not null);

                item.RuleFor(x => x)
                    .Must(x =>
                        (!string.IsNullOrWhiteSpace(x.City) && !string.IsNullOrWhiteSpace(x.State)) ||
                        !string.IsNullOrWhiteSpace(x.ZipCode))
                    .WithName("Location")
                    .WithMessage("Either City and State, or ZipCode must be provided.");
            });
    }

    private static readonly HashSet<string> ValidStates =
    [
        "AL","AK","AZ","AR","CA","CO","CT","DE","FL","GA",
        "HI","ID","IL","IN","IA","KS","KY","LA","ME","MD",
        "MA","MI","MN","MS","MO","MT","NE","NV","NH","NJ",
        "NM","NY","NC","ND","OH","OK","OR","PA","RI","SC",
        "SD","TN","TX","UT","VT","VA","WA","WV","WI","WY",
        "DC","PR","VI","GU","MP","AS","AA","AE","AP"
    ];
}
