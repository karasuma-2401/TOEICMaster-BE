namespace TOEICMaster.Application.Common.Interfaces;

public interface IAdminBootstrapSeeder
{
    Task<AdminBootstrapSeedResult> SeedAsync(CancellationToken cancellationToken);
}

public sealed record AdminBootstrapSeedResult(AdminBootstrapSeedAction Action);

public enum AdminBootstrapSeedAction
{
    Created,
    PromotedExisting,
    AlreadyAdmin
}
