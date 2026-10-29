namespace CropConnect.EndToEndTests.Infrastructure;

// Events, emails and outbox publishing are asynchronous, so their effects are polled for.
public static class Eventually
{
    public static async Task<T> GetAsync<T>(Func<Task<T>> probe, Func<T, bool> until, TimeSpan? timeout = null, string? because = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        var last = default(T)!;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                last = await probe();
                if (until(last))
                {
                    return last;
                }
            }
            catch (HttpRequestException)
            {
                // A service may be (re)starting.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        Assert.Fail($"Timed out waiting: {because ?? "condition not met"}. Last value: {last}");
        return last;
    }
}
