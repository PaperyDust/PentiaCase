using PentiaCase.Web.Models.Caching;
using PentiaCase.Web.Services.Api;

namespace PentiaCase.Web.Services.Caching;

public sealed class WingineersDataCache(Func<WingineersApiClient> createClient, TimeProvider timeProvider) : IDisposable
{
    private readonly SemaphoreSlim loadLock = new(1, 1);
    private WingineersDataSnapshot? snapshot;

    public WingineersDataSnapshot? Current => Volatile.Read(ref snapshot);

    public Task<WingineersDataSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        return LoadAsync(false, cancellationToken);
    }

    public Task<WingineersDataSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        return LoadAsync(true, cancellationToken);
    }

    private async Task<WingineersDataSnapshot> LoadAsync(bool refresh, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!refresh && Current is { } cached)
        {
            return cached;
        }

        await loadLock.WaitAsync(cancellationToken);
        try
        {
            if (!refresh && Current is { } loaded)
            {
                return loaded;
            }

            var client = createClient();
            var salespeopleTask = client.GetSalesPersonsAsync(cancellationToken);
            var ordersTask = client.GetOrderLinesAsync(cancellationToken);
            await Task.WhenAll(salespeopleTask, ordersTask);
            cancellationToken.ThrowIfCancellationRequested();

            var replacement = new WingineersDataSnapshot(
                Array.AsReadOnly((await salespeopleTask).ToArray()),
                Array.AsReadOnly((await ordersTask).ToArray()),
                timeProvider.GetUtcNow());
            Volatile.Write(ref snapshot, replacement);
            return replacement;
        }
        finally
        {
            loadLock.Release();
        }
    }

    public void Dispose()
    {
        loadLock.Dispose();
    }
}
