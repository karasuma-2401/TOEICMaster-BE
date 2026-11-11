using FluentValidation;

namespace TOEICMaster.Application.Accounts.Queries.GetAdminUsers;

public sealed class GetAdminUsersQueryValidator : AbstractValidator<GetAdminUsersQuery>
{
    private static readonly string[] Roles = [AdminUserValues.RoleUser, AdminUserValues.RoleAdmin];
    private static readonly string[] Statuses =
        [AdminUserValues.StatusActive, AdminUserValues.StatusPending, AdminUserValues.StatusLocked];
    private static readonly string[] Sorts =
    [
        "fullName:asc", "fullName:desc", "createdAt:asc", "createdAt:desc", "lastLoginAt:asc", "lastLoginAt:desc"
    ];

    public GetAdminUsersQueryValidator()
    {
        RuleFor(query => query.Page).InclusiveBetween(1, 10_000);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Q).MaximumLength(100);
        RuleFor(query => query.Role).Must(role => Roles.Contains(role)).When(query => query.Role is not null);
        RuleFor(query => query.Status).Must(status => Statuses.Contains(status)).When(query => query.Status is not null);
        RuleFor(query => query.Sort).Must(sort => Sorts.Contains(sort));
    }
}
