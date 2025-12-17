using System.Drawing;

namespace ModManager;

public sealed class LauncherForm : Form
{
    private const int DefaultMargin = 15;
    private const int LabelVerticalOffset = 4;
    private const int AddRemoveButtonSize = 30;

    private readonly GameLauncher launcher;
    private readonly UsedMods usedMods;
    private readonly Playlists playlists;
    private readonly Workshop workshop;

    private readonly Button startButton = new();

    private readonly Button addToPlaylistButton = new();
    private readonly Button removeFromPlaylistButton = new();

    private readonly AnnotatedListView<WorkshopInfo> unusedList = new();
    private readonly AnnotatedListView<WorkshopInfo> usedList = new();

    private readonly TextBox shareCodeBox = new();
    private readonly ComboBox playlistSelector = new();

    private readonly Thread backgroundWorker;

    private readonly CancellationTokenSource cancellationTokenSource = new();

    private Playlist playlist = null!;

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

        InitializeComponent();

        backgroundWorker = new(DoBackgroundWork);
        backgroundWorker.Start();
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
        SetupAddModButton();
        SetupRemoveModbutton();
        SetupPlaylistSelector();
        SetupShareCode();

        this.MinimumSize = new(600, 350);
        this.Size = new(1200, 700);

        this.HandleCreated += (s, e) =>
        {
            SetPlaylist(playlists.Get(string.Empty)!);
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

        launcher.GameLaunched += (s, e) => Delegate(() => startButton.Enabled = false);
        launcher.GameClosed += (s, e) => Delegate(() => startButton.Enabled = true);
        startButton.Click += (s, e) =>
        {
            usedMods.Set(playlist);
            launcher.LaunchGame();
        };
    }

    private void SetupUnusedList()
    {
        var label = new Label();
        label.Text = "Available mods";

        this.Controls.Add(label);
        this.Controls.Add(unusedList);

        this.Resize += (s, e) =>
        {
            var width = this.ClientSize.Width;
            var height = this.ClientSize.Height;

            label.Left = DefaultMargin;
            label.Top = DefaultMargin;

            unusedList.Left = label.Left;
            unusedList.Top = label.Bottom;
            unusedList.Width = (width - (4 * DefaultMargin)) / 3;
            unusedList.Height = height - (10 * DefaultMargin);

            label.Width = unusedList.Width;
        };

        unusedList.View = View.Details;
        unusedList.AutoArrange = false;
        unusedList.FullRowSelect = true;
        unusedList.Columns.Add(new ColumnHeader() { Text = "Name", Width = -2 });
    }

    private bool isBuildingUsedList = false;

    private void SetupUsedList()
    {
        var label = new Label();
        label.Text = "Mods in current playlist";

        this.Controls.Add(label);
        this.Controls.Add(usedList);
        this.Resize += (s, e) =>
        {
            var width = this.ClientSize.Width;
            var height = this.ClientSize.Height;

            usedList.Width = (width - (4 * DefaultMargin)) / 3;
            usedList.Height = height - (10 * DefaultMargin);

            usedList.Left = DefaultMargin + AddRemoveButtonSize + DefaultMargin + usedList.Width + DefaultMargin;
            usedList.Top = DefaultMargin;

            label.Width = usedList.Width;

            label.Left = DefaultMargin + AddRemoveButtonSize + DefaultMargin + usedList.Width + DefaultMargin;
            label.Top = DefaultMargin;

            usedList.Left = label.Left;
            usedList.Top = label.Bottom;
        };

        usedList.CheckBoxes = true;
        usedList.View = View.Details;
        usedList.AutoArrange = false;
        usedList.FullRowSelect = true;
        usedList.Columns.Add(new ColumnHeader() { Text = "Enabled", Width = 60 });
        usedList.Columns.Add(new ColumnHeader() { Text = "Name", Width = -2 });

        usedList.AllowDrop = true;
        usedList.ItemDrag += (s, e) =>
        {
            usedList.DoDragDrop(e.Item, DragDropEffects.Move);
        };
        usedList.DragEnter += (s, e) =>
        {
            e.Effect = e.AllowedEffect;
        };
        usedList.DragOver += (s, e) =>
        {
            var targetPoint = usedList.PointToClient(new Point(e.X, e.Y));
            var targetIndex = usedList.InsertionMark.NearestIndex(targetPoint);

            if (targetIndex >= 0)
            {
                var itemBounds = usedList.GetItemRect(targetIndex);
                if (targetPoint.Y > itemBounds.Top + (itemBounds.Height / 2))
                {
                    usedList.InsertionMark.AppearsAfterItem = true;
                }
                else
                {
                    usedList.InsertionMark.AppearsAfterItem = false;
                }
            }

            usedList.InsertionMark.Index = targetIndex;
        };
        usedList.DragLeave += (s, e) =>
        {
            usedList.InsertionMark.Index = -1;
        };
        usedList.DragDrop += (s, e) =>
        {
            var targetIndex = usedList.InsertionMark.Index;

            // If the insertion mark is not visible, exit the method.
            if (targetIndex == -1)
            {
                return;
            }

            // If the insertion mark is to the right of the item with
            // the corresponding index, increment the target index.
            if (usedList.InsertionMark.AppearsAfterItem)
            {
                targetIndex++;
            }

            usedList.InsertionMark.Index = -1;

            var draggedItem =
                (AnnotatedListViewItem<WorkshopInfo>)e.Data.GetData(typeof(AnnotatedListViewItem<WorkshopInfo>));

            var modId = draggedItem.Annotation.Id;

            AddMod(modId, targetIndex);
        };

        var previousCheckChange = default(DateTime);

        usedList.ItemCheck += (s, e) =>
        {
            if (isBuildingUsedList)
            {
                return;
            }

            var now = DateTime.UtcNow;
            if (now - previousCheckChange < TimeSpan.FromMilliseconds(100))
            {
                e.NewValue = e.CurrentValue;
                return;
            }

            previousCheckChange = now;

            var item = usedList.Items[e.Index];
            var modId = item.Annotation.Id;
            CheckMod(modId, e.NewValue is CheckState.Checked);
            e.NewValue = e.CurrentValue;
        };


    }

    private void SetupAddModButton()
    {
        this.Controls.Add(addToPlaylistButton);
        addToPlaylistButton.Text = ">>";
        addToPlaylistButton.Width = AddRemoveButtonSize;
        addToPlaylistButton.Height = AddRemoveButtonSize;
        addToPlaylistButton.Enabled = false;
        unusedList.ItemSelectionChanged += (s, e) =>
        {
            addToPlaylistButton.Enabled = unusedList.SelectedIndices.Count > 0;
        };

        addToPlaylistButton.Click += (s, e) =>
        {
            var ids = unusedList.SelectedItems.Select(x => x.Annotation.Id);
            AddMods(ids);
        };

        unusedList.Resize += (s, e) =>
        {
            addToPlaylistButton.Left = unusedList.Right + DefaultMargin;
            addToPlaylistButton.Top = unusedList.Top + (unusedList.Height / 2) - (DefaultMargin / 2) - AddRemoveButtonSize;
        };
    }

    private void SetupRemoveModbutton()
    {
        this.Controls.Add(removeFromPlaylistButton);
        removeFromPlaylistButton.Text = "<<";
        removeFromPlaylistButton.Width = AddRemoveButtonSize;
        removeFromPlaylistButton.Height = AddRemoveButtonSize;
        removeFromPlaylistButton.Enabled = false;
        usedList.ItemSelectionChanged += (s, e) =>
        {
            removeFromPlaylistButton.Enabled = usedList.SelectedIndices.Count > 0;
        };

        removeFromPlaylistButton.Click += (s, e) =>
        {
            var ids = usedList.SelectedItems.Select(x => x.Annotation.Id);
            RemoveMods(ids);
        };

        unusedList.Resize += (s, e) =>
        {
            removeFromPlaylistButton.Left = unusedList.Right + DefaultMargin;
            removeFromPlaylistButton.Top = unusedList.Top + (unusedList.Height / 2) + (DefaultMargin / 2);
        };
    }

    private void SetupPlaylistSelector()
    {
        var label = new Label();
        label.Text = "Playlist:";

        var createButton = new Button();
        createButton.Text = "Create";

        var deleteButton = new Button();
        deleteButton.Text = "Delete";

        this.Controls.Add(createButton);
        this.Controls.Add(deleteButton);
        this.Controls.Add(label);
        this.Controls.Add(playlistSelector);

        this.Resize += (s, e) =>
        {
            label.Width = 70;
            label.Left = usedList.Left;
            label.Top = usedList.Bottom + DefaultMargin + LabelVerticalOffset;

            playlistSelector.Left = label.Right;
            playlistSelector.Top = usedList.Bottom + DefaultMargin;

            deleteButton.Width = 50;
            deleteButton.Height = playlistSelector.Height;

            deleteButton.Left = usedList.Right - deleteButton.Width;
            deleteButton.Top = playlistSelector.Top;

            createButton.Width = 50;
            createButton.Height = playlistSelector.Height;

            createButton.Left = deleteButton.Left - (DefaultMargin / 2) - createButton.Width;
            createButton.Top = playlistSelector.Top;

            playlistSelector.Width = usedList.Width - label.Width - createButton.Width - deleteButton.Width - DefaultMargin;
        };

        playlistSelector.DropDown += (s, e) =>
        {
            playlistSelector.Items.Clear();
            
            foreach (var playlist in playlists.Get())
            {
                playlistSelector.Items.Add(playlist.Name);
            }
        };
    }

    private void SetupShareCode()
    {
        var label = new Label();
        label.Text = "Share code:";

        var copyButton = new Button();
        copyButton.Text = "Copy";

        var importButton = new Button();
        importButton.Text = "Import";

        this.Controls.Add(copyButton);
        this.Controls.Add(importButton);
        this.Controls.Add(label);
        this.Controls.Add(shareCodeBox);

        shareCodeBox.WordWrap = false;
        shareCodeBox.ReadOnly = true;
        shareCodeBox.BackColor = SystemColors.Window;
        this.Resize += (s, e) =>
        {
            label.Left = usedList.Left;
            label.Top = playlistSelector.Bottom + DefaultMargin + LabelVerticalOffset;
            label.Width = 70;

            shareCodeBox.Left = playlistSelector.Left;
            shareCodeBox.Top = playlistSelector.Bottom + DefaultMargin;

            importButton.Width = 50;
            importButton.Height = shareCodeBox.Height;

            importButton.Left = usedList.Right - importButton.Width;
            importButton.Top = shareCodeBox.Top;

            copyButton.Width = 50;
            copyButton.Height = shareCodeBox.Height;

            copyButton.Left = importButton.Left - (DefaultMargin / 2) - copyButton.Width;
            copyButton.Top = shareCodeBox.Top;

            shareCodeBox.Width = usedList.Width - label.Width - copyButton.Width - importButton.Width - DefaultMargin;
        };

        copyButton.Click += (s, e) =>
        {
            Clipboard.SetText(shareCodeBox.Text);
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

            if (index > oldIndex)
            {
                index--;
            }

            modified = modified with
            {
                Mods = modified.Mods
                    .RemoveAt(oldIndex)
                    .Insert(index, mod),
            };
        }

        UpdatePlaylist(modified);
    }

    private void AddMods(IEnumerable<ulong> ids)
    {
        var modified = playlist;
        var oldCount = modified.Mods.Length;

        var idSet = new HashSet<ulong>(modified.Mods.Select(m => m.Id));

        modified = modified with
        {
            Mods = modified.Mods
                .AddRange(ids.Where(id => !idSet.Contains(id)).Select(id => new PlaylistMod { Id = id, Enabled = true }))
                .ToImmutableArray(),
        };

        var newCount = modified.Mods.Length;

        if (newCount != oldCount)
        {
            UpdatePlaylist(modified);
        }
    }

    private void RemoveMods(IEnumerable<ulong> ids)
    {
        var modified = playlist;
        var oldCount = modified.Mods.Length;

        var idSet = new HashSet<ulong>(ids);

        modified = modified with
        {
            Mods = modified.Mods
                .Where(mod => !idSet.Contains(mod.Id))
                .ToImmutableArray(),
        };

        var newCount = modified.Mods.Length;

        if (newCount != oldCount)
        {
            UpdatePlaylist(modified);
        }
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
        playlists.Save(modified);
        SetPlaylist(modified);
    }

    private void SetPlaylist(Playlist playlist)
    {
        this.playlist = playlist;
        shareCodeBox.Text = playlist.Serialize();
        playlistSelector.SelectedItem = playlist.Name;
        UpdateModList();
    }

    private void UpdateModList(CancellationToken cancellationToken = default)
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
        Delegate(() => UpdateModList(subscribedMods));
        Console.WriteLine("Updated subscribed workshop items.");
    }

    private void UpdateModList(ImmutableArray<WorkshopInfo> mods)
    {
        void UpdateUnusedList()
        {
            unusedList.SuspendLayout();

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

            unusedList.ResumeLayout(true);
        }

        void UpdateUsedList()
        {
            usedList.SuspendLayout();
            isBuildingUsedList = true;

            var lastTopIndex = usedList.TopItem?.Index ?? 0;

            var idToPlaylist = new Dictionary<ulong, (PlaylistMod Mod, int Index)>();
            var idToListView = new Dictionary<ulong, AnnotatedListViewItem<WorkshopInfo>>();

            for (var i = 0; i < playlist.Mods.Length; i++)
            {
                var mod = playlist.Mods[i];
                idToPlaylist[mod.Id] = (mod, i);
            }

            for (var i = 0; i < usedList.Items.Count; i++)
            {
                var item = usedList.Items[i];
                idToListView[item.Annotation.Id] = item;
            }

            foreach (var entry in idToListView)
            {
                var id = entry.Key;
                if (!idToPlaylist.ContainsKey(id))
                {
                    usedList.Items.Remove(entry.Value);
                }
            }

            var intededIndex = new Dictionary<AnnotatedListViewItem<WorkshopInfo>, int>();

            foreach (var entry in idToPlaylist)
            {
                var id = entry.Key;
                var info = mods.First(m => m.Id == id);

                if (!idToListView.TryGetValue(id, out var item))
                {
                    item = new(info, string.Empty, info.Name);
                    usedList.Items.Add(item);
                }
                else
                {
                    item.Annotation = info;
                    item.SubItems[1].Text = info.Name;
                }

                item.Checked = entry.Value.Mod.Enabled;
                intededIndex[item] = entry.Value.Index;
            }

            usedList.Items.SortBy(item => intededIndex[item]);

            if (usedList.Items.Count > 0)
            {
                lastTopIndex = Math.Max(0, Math.Min(lastTopIndex, usedList.Items.Count - 1));
                var item = usedList.Items[lastTopIndex];
                usedList.TopItem = item;
            }

            isBuildingUsedList = false;
            usedList.ResumeLayout(true);
        }

        UpdateUnusedList();
        UpdateUsedList();
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

    private void Delegate(Action act)
    {
        if (this.InvokeRequired)
        {
            Invoke(act);
        }
        else
        {
            act();
        }
    }
}
