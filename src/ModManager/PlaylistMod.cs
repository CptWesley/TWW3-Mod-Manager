namespace ModManager;

public sealed record PlaylistMod
{
    public required ulong Id { get; init; }

    public required bool Enabled { get; init; }
}
