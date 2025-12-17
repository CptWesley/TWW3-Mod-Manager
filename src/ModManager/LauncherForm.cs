using System.Drawing;
using System.Threading;
using System.Xml.Linq;

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
    private readonly Label unusedListLabel = new();
    private readonly Label usedListLabel = new();

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

        backgroundWorker = new(StartBackgroundWork);
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
        var label = unusedListLabel;

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

        unusedList.ColumnWidthChanging += (s, e) =>
        {
            e.NewWidth = unusedList.Columns[e.ColumnIndex].Width;
            e.Cancel = true;
        };
    }

    private bool isBuildingUsedList = false;
    private bool isTogglingStatusColumn = false;

    private void ToggleStatusColumn(bool enabled)
    {
        var column = usedList.Columns[1];
        var newWidth = enabled ? 90 : 0;

        if (column.Width == newWidth)
        {
            return;
        }

        isTogglingStatusColumn = true;
        column.Width = newWidth;
        isTogglingStatusColumn = false;
    }

    private void SetupUsedList()
    {
        var label = usedListLabel;

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
        usedList.Columns.Add(new ColumnHeader() { Text = "Status", Width = 0 });
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

        var isBlockingChecking = false;

        usedList.MouseDown += (s, e) =>
        {
            if (e.Clicks > 1)
            {

            }
        };

        usedList.MouseUp += (s, e) =>
        {

        };

        usedList.ItemCheck += (s, e) =>
        {
            if (isBuildingUsedList || isBlockingChecking)
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

        usedList.ColumnWidthChanging += (s, e) =>
        {
            if (!isTogglingStatusColumn)
            {
                return;
            }

            e.NewWidth = usedList.Columns[e.ColumnIndex].Width;
            e.Cancel = true;
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

    private bool isBuildingSelectorList = false;

    private void SetupPlaylistSelector()
    {
        var label = new Label();
        label.Text = "Playlist:";

        var createButton = new Button();
        createButton.Text = "New";

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

        createButton.Click += (s, e) =>
        {
            using var nameDialog = new TextInputDialog
            {
                Text = "Confirm New Playlist",
                Label = "Please enter the name of the new playlist.",
                Input = "",
            };

            if (nameDialog.ShowDialog(this) is not DialogResult.OK)
            {
                return;
            }

            var name = nameDialog.Input;

            if (playlists.Get().Any(p => p.Name == name))
            {
                using var overwriteDialog = new TextInputDialog
                {
                    Text = "Confirm Overwrite Playlist",
                    Label = $"There is already a playlist with the name '{name}'. Are you sure you want to overwrite it? If you continue the old playlist will be deleted.",
                    InputVisible = false,
                };

                if (overwriteDialog.ShowDialog(this) is not DialogResult.OK)
                {
                    return;
                }
            }

            var copy = playlist with
            {
                Name = name,
            };

            UpdatePlaylist(copy);
        };

        deleteButton.Click += (s, e) =>
        {
            using var dialog = new TextInputDialog
            {
                Text = "Confirm Playlist Deletion",
                Label = $"Are you sure you want to delete the '{playlist.Name}' playlist? This action can not be reverted.",
                InputVisible = false,
            };

            if (dialog.ShowDialog(this) is not DialogResult.OK)
            {
                return;
            }

            playlists.Delete(playlist.Name);
            SetPlaylist(string.Empty);
        };

        playlistSelector.SelectedValueChanged += (s, e) =>
        {
            var index = playlistSelector.SelectedIndex;
            var value = (string)playlistSelector.Items[index];

            deleteButton.Enabled = !string.IsNullOrEmpty(value);

            if (!isBuildingSelectorList)
            {
                SetPlaylist(value);
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

        importButton.Click += (s, e) =>
        {
            using var importDialog = new TextInputDialog
            {
                Text = "Import Playlist",
                Label = $"Please enter the share code of the playlist you would like to import.",
            };

            if (importDialog.ShowDialog(this) is not DialogResult.OK)
            {
                return;
            }

            Playlist imported;
            try
            {
                imported = Playlist.Deserialize(importDialog.Input);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                MessageBox.Show($"An error has occurred while trying to import the playlist.\n\n{ex}");
                return;
            }

            using var nameDialog = new TextInputDialog
            {
                Text = "Import Playlist",
                Label = $"Please enter the name of the imported playlist.",
                Input = imported.Name,
            };

            if (nameDialog.ShowDialog(this) is not DialogResult.OK)
            {
                return;
            }

            var name = nameDialog.Input;

            if (playlists.Get().Any(p => p.Name == name))
            {
                using var overwriteDialog = new TextInputDialog
                {
                    Text = "Confirm Overwrite Playlist",
                    Label = $"There is already a playlist with the name '{name}'. Are you sure you want to overwrite it? If you continue the old playlist will be deleted.",
                    InputVisible = false,
                };

                if (overwriteDialog.ShowDialog(this) is not DialogResult.OK)
                {
                    return;
                }
            }

            var renamed = imported with
            {
                Name = name,
            };

            UpdatePlaylist(renamed);
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

    private void SetPlaylist(string name)
    {
        var retrieved = playlists.Get(name);
        SetPlaylist(retrieved ?? throw new NullReferenceException());
    }

    private void SetPlaylist(Playlist playlist)
    {
        this.playlist = playlist;
        shareCodeBox.Text = playlist.Serialize();
        UpdatePlaylistSelector();
        _ = UpdateModListAsync();
    }

    private void UpdatePlaylistSelector()
    {
        isBuildingSelectorList = true;
        playlistSelector.SuspendLayout();

        var all = playlists.Get().OrderBy(p => p.Name).ToArray();

        bool ShouldRebuild()
        {
            if (all.Length != playlistSelector.Items.Count)
            {
                return true;
            }

            for (var i = 0; i < all.Length; i++)
            {
                var expected = all[i].Name;
                var found = (string)playlistSelector.Items[i];

                if (expected != found)
                {
                    return true;
                }
            }

            return false;
        }

        void Rebuild()
        {
            playlistSelector.Items.Clear();

            foreach (var p in all)
            {
                playlistSelector.Items.Add(p.Name);

                if (p.Name == playlist.Name)
                {
                    playlistSelector.SelectedIndex = playlistSelector.Items.Count - 1;
                }
            }
        }

        void Select()
        {
            var index = -1;
            for (var i = 0; i < all.Length; i++)
            {
                var mod = all[i];
                if (mod.Name == playlist.Name)
                {
                    index = i;
                    break;
                }
            }

            if (index >= 0 && index != playlistSelector.SelectedIndex)
            {
                playlistSelector.SelectedIndex = index;
            }
        }

        if (ShouldRebuild())
        {
            Rebuild();
        }
        else
        {
            Select();
        }

        isBuildingSelectorList = false;
        playlistSelector.ResumeLayout(true);
    }

    private async Task UpdateModListAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("Updating subscribed workshop items...");
        var subscribedMods = await workshop.GetSubscribedItems(cancellationToken).ConfigureAwait(false);
        var unsubscribedModIds = playlist.Mods
            .Where(pm => !subscribedMods.Any(sm => sm.Id == pm.Id))
            .Select(pm => pm.Id)
            .ToArray();
        var unsubscribedMods = await workshop.GetItems(unsubscribedModIds, cancellationToken).ConfigureAwait(false);
        var mods = subscribedMods.AddRange(unsubscribedMods);
        Delegate(() => UpdateModList(mods));
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

            if (unusedList.Items.Count > 0)
            {
                lastTopIndex = Math.Min(lastTopIndex, unusedList.Items.Count - 1);
                var item = unusedList.Items[lastTopIndex];
                unusedList.TopItem = item;
            }

            unusedListLabel.Text = $"Available mods ({unusedList.Items.Count})";

            unusedList.ResumeLayout(true);
        }

        void UpdateUsedList()
        {
            usedList.SuspendLayout();
            isBuildingUsedList = true;

            var showStatus = false;
            var ready = true;

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
                var info = mods.FirstOrDefault(m => m.Id == id)
                    ?? new()
                    {
                        Id = id,
                        Name = id.ToString(),
                        Created = default,
                        Description = string.Empty,
                        DownloadProgress = 0,
                        Image = string.Empty,
                        IsDownloading = false,
                        IsSubscribed = false,
                        Owner = string.Empty,
                        Updated = default,
                    };

                var status = GetStatus(info);

                if (status.Length > 0)
                {
                    showStatus = true;
                }

                if (entry.Value.Mod.Enabled && status.Length > 0)
                {
                    ready = false;
                }

                if (!idToListView.TryGetValue(id, out var item))
                {
                    item = new(info, string.Empty, status, info.Name);
                    usedList.Items.Add(item);
                }
                else
                {
                    item.Annotation = info;
                    item.SubItems[1].Text = status;
                    item.SubItems[2].Text = info.Name;
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

            startButton.Enabled = ready;
            ToggleStatusColumn(showStatus);

            usedListLabel.Text = $"Mods in current playlist ({usedList.Items.Count})";

            isBuildingUsedList = false;
            usedList.ResumeLayout(true);
        }

        UpdateUnusedList();
        UpdateUsedList();
    }

    private static string GetStatus(WorkshopInfo info)
    {
        if (!info.IsSubscribed)
        {
            return "Not Subscribed";
        }

        if (info.DownloadProgress >= 1)
        {
            return "";
        }

        return $"{(int)(info.DownloadProgress * 100)}%";
    }

    private void SetName()
    {
        var name = "CptWesley's Total War Warhammer III Mod Manager";
        this.Text = name;
        this.Name = name;
    }

    private async Task DoBackgroundWork(CancellationToken cancellationToken)
    {

        await UpdateModListAsync(cancellationToken).ConfigureAwait(false);
        await Task.Delay(5_000, cancellationToken).ConfigureAwait(false);
    }

    private void StartBackgroundWork()
    {
        try
        {
            var cancellationToken = cancellationTokenSource.Token;

            while (!cancellationToken.IsCancellationRequested)
            {
                DoBackgroundWork(cancellationToken).GetAwaiter().GetResult();
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
            var result = Invoke(() =>
            {
                try
                {
                    act();
                }
                catch (Exception ex)
                {
                    return ex;
                }

                return null;
            });

            if (result is Exception ex)
            {
                throw ex;
            }
        }
        else
        {
            act();
        }
    }
}
