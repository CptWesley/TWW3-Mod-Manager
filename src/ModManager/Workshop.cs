using Steamworks.Data;
using Steamworks.Ugc;

namespace ModManager;

public delegate void WorkshopItemDownloadProgressEventHandler(ulong id, float progress);

public sealed class Workshop
{
    public event WorkshopItemDownloadProgressEventHandler? DownloadProgress;

    public async Task Subscribe(PlaylistMod mod, CancellationToken cancellationToken = default)
        => await Subscribe(mod.Id, cancellationToken).ConfigureAwait(false);

    public async Task Subscribe(ulong mod, CancellationToken cancellationToken = default)
    {
        _ = await GetItemInternal<object?>(
            id: mod,
            map: async maybeItem =>
            {
                if (maybeItem is not { } item)
                {
                    return null; // TODO handle error
                }

                await item.Subscribe().ConfigureAwait(false);
                _ = item.DownloadAsync(progress: progress =>
                {
                    DownloadProgress?.Invoke(mod, progress);
                });

                return null;
            },
            cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<WorkshopInfo?> GetInfo(PlaylistMod mod, CancellationToken cancellationToken = default)
        => await GetInfo(mod.Id, cancellationToken).ConfigureAwait(false);

    public async Task<WorkshopInfo?> GetInfo(ulong mod, CancellationToken cancellationToken = default)
        => (await GetItems([mod], cancellationToken).ConfigureAwait(false)).FirstOrDefault();

    private static WorkshopInfo Convert(Item item)
        => new ()
        {
            Id = item.Id.Value,
            Name = item.Title,
            Description = item.Description,
            Image = item.PreviewImageUrl,
            Created = item.Created,
            Updated = item.Updated,
            Owner = item.Owner.Name,
            IsSubscribed = item.IsSubscribed,
            IsDownloading = item.IsDownloading || item.IsDownloadPending,
            DownloadProgress = (!item.IsDownloading && !item.IsInstalled) ? 0 : item.DownloadAmount,
        };

    public async Task<ImmutableArray<WorkshopInfo>> GetSubscribedItems(CancellationToken cancellationToken = default)
    {
        var query = Query
            .Items
            .WhereUserSubscribed();
        var result = (await DoQuery(
                query: query,
                name: "GetSubscribedItems",
                map: items => Task.FromResult(items.Select(Convert).ToImmutableArray()),
                cancellationToken: cancellationToken)
                .ConfigureAwait(false))
            .OrderBy(static item => item.Name)
            .ThenBy(static item => item.Id)
            .ToImmutableArray();
        return result;
    }

    public async Task<ImmutableArray<WorkshopInfo>> GetItems(IEnumerable<ulong> ids, CancellationToken cancellationToken = default)
    {
        var result = (await GetItemsInternal(
                ids: ids,
                map: items => Task.FromResult(items.Select(Convert).ToImmutableArray()),
                cancellationToken: cancellationToken)
                .ConfigureAwait(false))
            .OrderBy(static item => item.Name)
            .ThenBy(static item => item.Id)
            .ToImmutableArray();
        return result;
    }

    private async Task<T> GetItemInternal<T>(
        ulong id,
        Func<Item?, Task<T>> map,
        CancellationToken cancellationToken)
    {
        var result = await GetItemsInternal(
            ids: [id],
            map: items =>
            {
                var item = items.Length <= 0 ? default(Item?) : items[0];
                var mapResult = map(item);
                return mapResult;
            },
            cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result;
    }

    private async Task<T> GetItemsInternal<T>(
        IEnumerable<ulong> ids,
        Func<ImmutableArray<Item>, Task<T>> map,
        CancellationToken cancellationToken)
    {
        var queryIds = ids
            .Select(id => (PublishedFileId)id)
            .ToArray();

        if (queryIds.Length == 0)
        {
            var emptyResult = await map([]);
            return emptyResult;
        }
        else if (queryIds.Length == 1)
        {
            var sw = Stopwatch.StartNew();
            var singleItem = await Item.GetAsync(queryIds[0]);

            Console.WriteLine($"Get item took {sw.ElapsedMilliseconds} ms.");

            if (!singleItem.HasValue)
            {
                var emptyResult = await map([]);
                return emptyResult;
            }
            else
            {
                var singleResult = await map([singleItem.Value]);
                return singleResult;
            }

        }

        var query = Query
            .Items
            .WithFileId(queryIds);

        var result = await DoQuery(query, "GetItemsInternal", map, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async Task<T> DoQuery<T>(
        Query query,
        string name,
        Func<ImmutableArray<Item>, Task<T>> map,
        CancellationToken cancellationToken)
    {
        var disposables = new List<IDisposable>();
        var sw = Stopwatch.StartNew();

        try
        {
            var items = new List<Item>();
            var seen = new HashSet<ulong>();

            var page = 1;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (await GetPage(query, page) is not { } pageResult)
                {
                    break;
                }

                disposables.Add(pageResult);

                var pageItems = pageResult
                    .Entries;

                foreach (var item in pageItems)
                {
                    if (seen.Add(item.Id))
                    {
                        items.Add(item);
                    }
                }

                if (pageResult.TotalCount <= seen.Count)
                {
                    break;
                }    

                page++;
            }

            var result = await map(items.ToImmutableArray());
            return result;
        }
        finally
        {
            foreach (var disposable in disposables)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                }
            }

            sw.Stop();
            Console.WriteLine($"Query '{name}' took {sw.ElapsedMilliseconds} ms.");
        }
    }

    private static async Task<ResultPage?> GetPage(Query query, int page)
    {
        var maybePageResult = await query
            .GetPageAsync(page)
            .ConfigureAwait(false);

        if (maybePageResult is not { } pageResult)
        {
            return null;
        }

        if (pageResult.ResultCount <= 0)
        {
            pageResult.Dispose();
            return null;
        }

        return pageResult;
    }
}

public sealed record WorkshopInfo
{
    public required ulong Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string Owner { get; init; }

    public required string Image { get; init; }

    public required DateTime Created { get; init; }

    public required DateTime Updated { get; init; }

    public required bool IsSubscribed { get; init; }

    public required bool IsDownloading { get; init; }

    public required float DownloadProgress { get; init; }
}
