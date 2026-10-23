namespace TOEICMaster.Tests.TestDoubles;

public sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(int seconds) => Now = Now.AddSeconds(seconds);
}
