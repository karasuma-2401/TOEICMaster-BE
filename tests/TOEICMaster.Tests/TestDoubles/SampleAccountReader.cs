using TOEICMaster.Application.Accounts;
using TOEICMaster.Application.Accounts.Queries.GetAccounts;

namespace TOEICMaster.Tests.TestDoubles;

public sealed class SampleAccountReader : IAccountReader
{
    public Task<AccountPageDto> GetPageAsync(int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var accounts = Enumerable.Range(1, 23).Select(index => new AccountSummaryDto(
            Guid.Parse($"00000000-0000-0000-0000-{index:D12}"), $"user{index}@example.com",
            $"User {index}", true, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero))).ToArray();
        return Task.FromResult(new AccountPageDto(
            accounts.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray(), pageNumber, pageSize, accounts.Length));
    }
}
