using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace ModManager;

public sealed class LauncherForm : Form
{
    private const int DefaultMargin = 15;

    private readonly object updateModListLock = new();

    private readonly GameLauncher launcher;
    private readonly UsedMods usedMods;
    private readonly Playlists playlists;
    private readonly Workshop workshop;

    private readonly Button startButton = new();
    private readonly AnnotatedListView<WorkshopInfo> unusedList = new();
    private readonly AnnotatedListView<WorkshopInfo> usedList = new();

    private readonly Thread backgroundWorker;

    private readonly CancellationTokenSource cancellationTokenSource = new();

    private Playlist playlist;

    public LauncherForm(
        GameLauncher launcher,
        UsedMods usedMods,
        Playlists playlists,
        Workshop workshop)
    {
        this.launcher = launcher;
        this.usedMods = usedMods;
        this.playlists = playlists;
        this.workshop = workshop;

        playlist = this.playlists.Get(string.Empty)!;

        InitializeComponent();

        backgroundWorker = new(DoBackgroundWork);
        //backgroundWorker.Start();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        this.FormClosing += (s, e) => this.cancellationTokenSource.Cancel();
        this.FormClosed += (s, e) => this.cancellationTokenSource.Cancel();
        SetName();
        SetupStartButton();
        SetupUnusedList();
        SetupUsedList();

        this.MinimumSize = new(600, 350);
        this.Size = new(1200, 700);

        this.HandleCreated += (s, e) =>
        {
            UpdateModList();
        };

        this.ResumeLayout(true);
    }

    private void SetupStartButton()
    {
        this.Controls.Add(this.startButton);
        startButton.Enabled = true;
        startButton.Text = "Launch Game";
        startButton.Width = 100;
        startButton.Height = 30;

        this.Resize += (s, e) =>
        {
            var width = this.ClientSize.Width;
            var height = this.ClientSize.Height;

            startButton.Left = width - startButton.Width - DefaultMargin;
            startButton.Top = height - startButton.Height - DefaultMargin;
        };

        launcher.GameLaunched += (s, e) => Invoke(() => startButton.Enabled = false);
        launcher.GameClosed += (s, e) => Invoke(() => startButton.Enabled = true);
        startButton.Click += (s, e) =>
        {
            usedMods.Set(playlist);
            launcher.LaunchGame();
        };
    }

    private void SetupUnusedList()
    {
        this.Controls.Add(unusedList);
        this.Resize += (s, e) =>
        {
            var width = this.ClientSize.Width;
            var height = this.ClientSize.Height;

            unusedList.Left = DefaultMargin;
            unusedList.Top = DefaultMargin;
            unusedList.Width = (width - (4 * DefaultMargin)) / 3;
            unusedList.Height = height - (10 * DefaultMargin);
        };

        unusedList.View = View.Details;
        unusedList.AutoArrange = false;
        unusedList.FullRowSelect = true;
        unusedList.Columns.Add(new ColumnHeader() { Text = "Name", Width = -2 });
    }

    private bool isBuildingUsedList = false;

    private void SetupUsedList()
    {
        this.Controls.Add(usedList);
        this.Resize += (s, e) =>
        {
            var width = this.ClientSize.Width;
            var height = this.ClientSize.Height;

            usedList.Width = (width - (4 * DefaultMargin)) / 3;
            usedList.Height = height - (10 * DefaultMargin);

            usedList.Left = DefaultMargin + usedList.Width + DefaultMargin;
            usedList.Top = DefaultMargin;
        };

        usedList.CheckBoxes = true;
        usedList.View = View.Details;
        usedList.AutoArrange = false;
        usedList.FullRowSelect = true;
        usedList.Columns.Add(new ColumnHeader() { Text = "Enabled", Width = 60 });
        usedList.Columns.Add(new ColumnHeader() { Text = "Name", Width = -2 });

        AnnotatedListViewItem<WorkshopInfo>? movingItem = null;

        usedList.AllowDrop = true;
        usedList.MouseDown += (s, e) =>
        {
            movingItem = usedList.GetItemAt(e.X, e.Y);

            if (movingItem is null)
            {
                return;
            }

            usedList.DoDragDrop(movingItem, DragDropEffects.Move);
        };
        usedList.DragOver += (s, e) =>
        {
            e.Effect = DragDropEffects.Move;
        };
        usedList.DragDrop += (s, e) =>
        {
            lock (updateModListLock)
            {
                if (movingItem is null)
                {
                    return;
                }

                var modId = movingItem.Annotation.Id;

                var localPoint = usedList.PointToClient(new Point(e.X, e.Y));
                var target = usedList.GetItemAt(localPoint.X, localPoint.Y);
                var newIndex = target?.Index ?? usedList.Items.Count - 1;

                AddMod(modId, newIndex);
            }
        };

        var prev = default(DateTime);
        var prevSender = default(object);
        var prevEvent = default(ItemCheckEventArgs);

        usedList.ItemCheck += (s, e) =>
        {
            lock (updateModListLock)
            {
                if (isBuildingUsedList)
                {
                    return;
                }

                var now = DateTime.UtcNow;
                if (now - prev < TimeSpan.FromMilliseconds(100))
                {
                    e.NewValue = e.CurrentValue;
                    return;
                }

                prev = now;
                prevSender = s;
                prevEvent = e;

                var item = usedList.Items[e.Index];
                var modId = item.Annotation.Id;
                CheckMod(modId, e.NewValue is CheckState.Checked);
                e.NewValue = e.CurrentValue;
            }
        };
    }

    private void AddMod(ulong id, int index)
    {
        var modified = playlist;

        var maybeOldIndex = playlist.Mods
            .Select<PlaylistMod, (ulong Id, int Index)?>((x, i) => (x.Id, i))
            .FirstOrDefault(m => m.HasValue && m.Value.Id == id)
            ?.Index;

        if (maybeOldIndex is not { } oldIndex)
        {
            modified = modified with
            {
                Mods = modified.Mods.Insert(index, new PlaylistMod { Id = id, Enabled = true }),
            };
        }
        else if (oldIndex == index)
        {
            return;
        }
        else
        {
            var mod = modified.Mods[oldIndex];
            var insertOffset = index > oldIndex ? 0 : 0;

            modified = modified with
            {
                Mods = modified.Mods
                    .RemoveAt(oldIndex)
                    .Insert(index + insertOffset, mod),
            };
        }

        UpdatePlaylist(modified);
    }

    private void CheckMod(ulong id, bool enabled)
    {
        Console.WriteLine($"CheckMod({id}, {enabled})");

        var maybeOldIndex = playlist.Mods
            .Select<PlaylistMod, (ulong Id, int Index)?>((x, i) => (x.Id, i))
            .FirstOrDefault(m => m.HasValue && m.Value.Id == id)
            ?.Index;

        if (maybeOldIndex is not { } oldIndex)
        {
            return;
        }

        var item = playlist.Mods[oldIndex];

        if (item.Enabled == enabled)
        {
            return;
        }

        var modified = playlist with
        {
            Mods = playlist.Mods
                .RemoveAt(oldIndex)
                .Insert(oldIndex, item with { Enabled = enabled }),
        };

        UpdatePlaylist(modified);
    }

    private void UpdatePlaylist(Playlist modified)
    {
        playlist = modified;
        playlists.Save(playlist);
        UpdateModList();
    }

    private void UpdateModList(CancellationToken cancellationToken = default)
    {
        lock (updateModListLock)
        {
            Console.WriteLine("Updating subscribed workshop items...");
            var subscribedMods = workshop.GetSubscribedItems(cancellationToken);
            var unsubscribedMods = playlist.Mods
                .Where(pm => !subscribedMods.Any(sm => sm.Id == pm.Id))
                .Select(pm =>
                {
                    if (workshop.GetInfo(pm) is { } info)
                    {
                        return info;
                    }

                    return new()
                    {
                        Created = default,
                        Description = "",
                        DownloadProgress = 0,
                        Id = pm.Id,
                        Image = "",
                        IsDownloading = false,
                        IsSubscribed = false,
                        Name = $"Unknown mod [{pm.Id}]",
                        Owner = "",
                        Updated = default,
                    };
                })
                .ToImmutableArray();
            var mods = subscribedMods.AddRange(unsubscribedMods);
            //Invoke(() => UpdateModList(subscribedMods));
            UpdateModList(subscribedMods);
            Console.WriteLine("Updated subscribed workshop items.");
        }
    }

    private void UpdateModList(ImmutableArray<WorkshopInfo> mods)
    {
        void UpdateUnusedList()
        {
            var lastTopIndex = unusedList.TopItem?.Index ?? 0;

            var toAdd = new Dictionary<ulong, WorkshopInfo>();
            var toDelete = new Dictionary<ulong, int>();

            foreach (var item in mods)
            {
                if (!playlist.Mods.Any(m => m.Id == item.Id))
                {
                    toAdd.Add(item.Id, item);
                }
            }

            for (var i = 0; i < unusedList.Items.Count; i++)
            {
                var item = unusedList.Items[i];
                var id = item.Annotation.Id;
                toDelete.Add(id, i);
                toAdd.Remove(id);
            }

            foreach (var item in mods)
            {
                if (!playlist.Mods.Any(m => m.Id == item.Id))
                {
                    var persist = toDelete.Remove(item.Id);

                    if (persist)
                    {
                        var oldItem = unusedList.Items
                            .AsEnumerable()
                            .First(row => row.Annotation.Id == item.Id);
                        oldItem.SubItems[0].Text = item.Name;
                    }
                }
            }

            foreach (var pair in toDelete.OrderBy(x => x.Value).Select((x, i) => (x, i)))
            {
                var id = pair.x.Key;
                var index = pair.x.Value - pair.i;
                unusedList.Items.RemoveAt(index);
            }

            foreach (var pair in toAdd)
            {
                var mod = pair.Value;
                var index = unusedList.Items
                    .AsEnumerable()
                    .Select(static item => item.SubItems[0].Text)
                    .TakeWhile(listItem => listItem.CompareTo(mod.Name) < 0)
                    .Count();
                unusedList.Items.Insert(index, new(mod, mod.Name));
            }

            if (unusedList.Items.Count >= 0)
            {
                lastTopIndex = Math.Min(lastTopIndex, unusedList.Items.Count - 1);
                var item = unusedList.Items[lastTopIndex];
                unusedList.TopItem = item;
            }
        }

        void UpdateUsedList()
        {
            Console.WriteLine("START");
            isBuildingUsedList = true;

            var lastTopIndex = usedList.TopItem?.Index ?? 0;

            usedList.Items.Clear();

            foreach (var mod in playlist.Mods)
            {
                if (playlist.Mods.Where(x => !x.Enabled).Count() >= 2)
                {

                }

                var info = mods.First(m => m.Id == mod.Id);
                var name = info.Name;
                usedList.Items.Add(new(info, string.Empty, name)
                {
                    Checked = mod.Enabled,
                });
            }

            if (usedList.Items.Count > 0)
            {
                lastTopIndex = Math.Max(0, Math.Min(lastTopIndex, usedList.Items.Count - 1));
                var item = usedList.Items[lastTopIndex];
                usedList.TopItem = item;
            }

            isBuildingUsedList = false;
            Console.WriteLine("END");
        }

        lock (updateModListLock)
        {
            UpdateUnusedList();
            UpdateUsedList();
        }
    }

    private void SetName()
    {
        var name = "CptWesley's Total War Warhammer III Mod Manager";
        this.Text = name;
        this.Name = name;
    }

    private void DoBackgroundWork()
    {
        try
        {
            var cancellationToken = cancellationTokenSource.Token;

            while (!cancellationToken.IsCancellationRequested)
            {
                if (!this.Created)
                {
                    continue;
                }

                UpdateModList(cancellationToken);
                Task.Delay(5_000, cancellationToken).GetAwaiter().GetResult();
            }
        }
        catch (TaskCanceledException)
        {
            // Do nothing. Expected.
        }
    }

    protected override void Dispose(bool disposing)
    {
        this.cancellationTokenSource.Cancel();
        base.Dispose(disposing);
    }
}
