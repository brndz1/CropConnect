namespace Notifications.Service.Tests.Infrastructure;

// A clock the test moves by hand, to reach the next email retry without waiting.
public sealed class ManualClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan by) => Now += by;
}
