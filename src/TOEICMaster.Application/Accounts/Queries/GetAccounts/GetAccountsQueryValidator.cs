using FluentValidation;

namespace TOEICMaster.Application.Accounts.Queries.GetAccounts;

public sealed class GetAccountsQueryValidator : AbstractValidator<GetAccountsQuery>
{
    public GetAccountsQueryValidator()
    {
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.PageNumber)
            .Must((query, pageNumber) => ((long)pageNumber - 1) * query.PageSize <= int.MaxValue)
            .When(query => query.PageNumber > 0 && query.PageSize is >= 1 and <= 100)
            .WithMessage("The requested page exceeds the supported pagination offset.");
    }
}
