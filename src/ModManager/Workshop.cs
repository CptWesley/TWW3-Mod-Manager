using Steamworks.Ugc;

namespace ModManager;

public delegate void WorkshopItemDownloadProgressEventHandler(ulong id, float progress);

public sealed class Workshop
{
    public event WorkshopItemDownloadProgressEventHandler? DownloadProgress;

    public void Subscribe(PlaylistMod mod)
        => Subscribe(mod.Id);

    public void Subscribe(ulong mod)
    {
        var maybeItem = Item.GetAsync(mod).GetAwaiter().GetResult();
        
        if (maybeItem is not { } item)
        {
            return; // TODO handle error
        }

        _ = item.Subscribe().GetAwaiter().GetResult();
        _ = item.DownloadAsync(progress: progress =>
        {
            DownloadProgress?.Invoke(mod, progress);
        });
    }

    public WorkshopInfo? GetInfo(PlaylistMod mod)
        => GetInfo(mod.Id);

    public WorkshopInfo? GetInfo(ulong mod)
    {
        var maybeItem = Item.GetAsync(mod).GetAwaiter().GetResult();

        if (maybeItem is not { } item)
        {
            return null;
        }

        return new()
        {
            Id = mod,
            Name = item.Title,
            Description = item.Description,
            Image = item.PreviewImageUrl,
            Created = item.Created,
            Updated = item.Updated,
            Owner = item.Owner.Name,
            IsSubscribed = item.IsSubscribed,
            IsDownloading = item.IsDownloading || item.IsDownloadPending,
            DownloadProgress = item.DownloadAmount,
        };
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
