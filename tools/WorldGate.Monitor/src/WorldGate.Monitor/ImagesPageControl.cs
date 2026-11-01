using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using WorldGate.Monitor.Core.ManagedImages;

namespace WorldGate.Monitor;

internal sealed class ImagesPageControl : UserControl
{
    private readonly ConfigurationStore configurationStore = new();
    private readonly ManagedImageStatusService statusService;
    private readonly ListView runnerList = CreateListView();
    private readonly ListView otherList = CreateListView();
    private readonly Label pathLabel = new() { AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(720, 0) };
    private readonly Button refreshButton = new() { Text = "Refresh status", AutoSize = true };
    private readonly Button addImageButton = new() { Text = "Add image…", AutoSize = true };

    private MonitorConfiguration baseline = new();
    private string configurationPath = string.Empty;
    private IReadOnlyDictionary<string, ManagedImageStatusSnapshot> statusById = new Dictionary<string, ManagedImageStatusSnapshot>();
    private int modalInteractionDepth;
    private bool pendingListRebuild;
    private bool pendingStatusRefresh;

    public event EventHandler? ManagedImagesChanged;

    public bool PausesBackgroundRefresh => modalInteractionDepth > 0;

    public ImagesPageControl(string? wslDistribution)
    {
        statusService = new ManagedImageStatusService(wslDistribution);
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(8, 4, 8, 8),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        pathLabel.Margin = new Padding(4, 4, 4, 4);
        root.Controls.Add(pathLabel, 0, 0);

        root.Controls.Add(BuildSection("GitHub Actions runner images", runnerList), 0, 1);
        root.Controls.Add(BuildSection("Other Docker / Kubernetes images", otherList), 0, 2);

        var bar = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        bar.Controls.Add(addImageButton);
        bar.Controls.Add(refreshButton);
        root.Controls.Add(bar, 0, 3);

        Controls.Add(root);

        runnerList.MouseDoubleClick += (_, e) => OnEdit(runnerList, e);
        otherList.MouseDoubleClick += (_, e) => OnEdit(otherList, e);
        addImageButton.Click += (_, _) => AddEntry();
        refreshButton.Click += async (_, _) => await RefreshStatusAsync(force: true);

        ReloadFromDisk(silent: true);
    }

    public IReadOnlyList<ManagedImageEntry> GetManagedImages() => baseline.ManagedImages;

    public async Task RefreshStatusAsync(bool force = false)
    {
        if (!force && PausesBackgroundRefresh)
        {
            pendingStatusRefresh = true;
            return;
        }

        IReadOnlyList<ManagedImageStatusSnapshot> snapshots =
            await statusService.ProbeAllAsync(baseline.ManagedImages).ConfigureAwait(true);
        statusById = snapshots.ToDictionary(snapshot => snapshot.EntryId, snapshot => snapshot);

        if (PausesBackgroundRefresh)
        {
            pendingStatusRefresh = true;
            ApplyStatusToVisibleItems(runnerList);
            ApplyStatusToVisibleItems(otherList);
            return;
        }

        RebuildList(runnerList, ManagedImageCategory.GitHubRunner);
        RebuildList(otherList, ManagedImageCategory.OtherWorkload);
    }

    public void ReloadOnShow()
    {
        if (PausesBackgroundRefresh)
        {
            pendingListRebuild = true;
            pendingStatusRefresh = true;
            return;
        }

        ReloadFromDisk(silent: true);
        _ = RefreshStatusAsync();
    }

    private Control BuildSection(string title, ListView list)
    {
        var group = new GroupBox { Text = title, Dock = DockStyle.Fill, Padding = new Padding(8, 4, 8, 8) };
        list.Dock = DockStyle.Fill;
        group.Controls.Add(list);

        var context = new ContextMenuStrip();
        var editItem = new ToolStripMenuItem("Edit…", null, (_, _) => EditSelected(list));
        var removeItem = new ToolStripMenuItem("Remove", null, (_, _) => RemoveSelected(list));
        var installItem = new ToolStripMenuItem("Install / build…", null, async (_, _) => await InstallSelectedAsync(list));
        var toggleMonitorItem = new ToolStripMenuItem("Toggle monitor", null, (_, _) => ToggleMonitor(list));
        var openPathItem = new ToolStripMenuItem("Open build path", null, (_, _) => OpenBuildPath(list));
        context.Items.AddRange([editItem, removeItem, installItem, toggleMonitorItem, openPathItem]);
        list.ContextMenuStrip = context;

        return group;
    }

    private static ListView CreateListView()
    {
        return new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
        };
    }

    private void ReloadFromDisk(bool silent)
    {
        if (PausesBackgroundRefresh)
        {
            pendingListRebuild = true;
            return;
        }

        ConfigurationLoadResult loaded = configurationStore.LoadOrCreate();
        configurationPath = loaded.ConfigurationPath;
        baseline = loaded.Configuration with { ManagedImages = loaded.Configuration.ManagedImages.ToArray() };
        pathLabel.Text = string.IsNullOrWhiteSpace(loaded.Error)
            ? $"Stored in {loaded.ConfigurationPath}"
            : $"{loaded.ConfigurationPath} — {loaded.Error}";

        EnsureColumns(runnerList);
        EnsureColumns(otherList);
        RebuildList(runnerList, ManagedImageCategory.GitHubRunner);
        RebuildList(otherList, ManagedImageCategory.OtherWorkload);
    }

    private static void EnsureColumns(ListView list)
    {
        if (list.Columns.Count > 0)
        {
            return;
        }

        list.Columns.Add("Name", 140);
        list.Columns.Add("Location", 280);
        list.Columns.Add("Monitor", 64);
        list.Columns.Add("Status", 140);
        list.Columns.Add("Actions", 80);
    }

    private void RebuildList(ListView list, ManagedImageCategory category)
    {
        if (PausesBackgroundRefresh)
        {
            pendingListRebuild = true;
            return;
        }

        string? selectedId = GetSelectedEntryId(list);
        list.BeginUpdate();
        try
        {
            list.Items.Clear();
            foreach (ManagedImageEntry entry in baseline.ManagedImages.Where(entry => entry.Category == category))
            {
                list.Items.Add(CreateListItem(entry));
            }

            RestoreSelection(list, selectedId);
        }
        finally
        {
            list.EndUpdate();
        }
    }

    private void ApplyStatusToVisibleItems(ListView list)
    {
        list.BeginUpdate();
        try
        {
            foreach (ListViewItem item in list.Items)
            {
                if (item.Tag is not ManagedImageEntry entry)
                {
                    continue;
                }

                ManagedImageEntry? current = ManagedImageEntryCollection.FindById(baseline.ManagedImages, entry.Id) ?? entry;
                item.Tag = current;
                item.SubItems[3].Text = FormatStatus(current.Id);
            }
        }
        finally
        {
            list.EndUpdate();
        }
    }

    private ListViewItem CreateListItem(ManagedImageEntry entry)
    {
        var item = new ListViewItem(entry.DisplayName);
        item.SubItems.Add(FormatLocation(entry));
        item.SubItems.Add(entry.MonitorStatus ? "On" : "Off");
        item.SubItems.Add(FormatStatus(entry.Id));
        item.SubItems.Add("Right-click");
        item.Tag = entry;
        return item;
    }

    private string FormatStatus(string entryId)
    {
        statusById.TryGetValue(entryId, out ManagedImageStatusSnapshot? snapshot);
        return snapshot?.Summary ?? "—";
    }

    private static string? GetSelectedEntryId(ListView list)
    {
        if (list.SelectedItems.Count == 0 || list.SelectedItems[0].Tag is not ManagedImageEntry entry)
        {
            return null;
        }

        return entry.Id;
    }

    private static void RestoreSelection(ListView list, string? entryId)
    {
        if (string.IsNullOrWhiteSpace(entryId))
        {
            return;
        }

        foreach (ListViewItem item in list.Items)
        {
            if (item.Tag is ManagedImageEntry entry && entry.Id == entryId)
            {
                item.Selected = true;
                item.Focused = true;
                item.EnsureVisible();
                return;
            }
        }
    }

    private static string FormatLocation(ManagedImageEntry entry)
    {
        var parts = new List<string> { entry.ImageReference };
        if (!string.IsNullOrWhiteSpace(entry.BuildContextPath))
        {
            parts.Add($"build: {entry.BuildContextPath.Trim()}");
        }

        if (entry.ProbeBackend == ManagedImageProbeBackend.Kubernetes
            && !string.IsNullOrWhiteSpace(entry.KubernetesNamespace))
        {
            parts.Add($"ns: {entry.KubernetesNamespace.Trim()}");
        }

        return string.Join(" · ", parts);
    }

    private void AddEntry()
    {
        using (BeginModalInteraction())
        {
            using var addDialog = new AddManagedImageDialog();
            if (addDialog.ShowDialog(FindForm()) != DialogResult.OK)
            {
                return;
            }

            ManagedImageEntry? entry = addDialog.BuildEntry();
            if (entry is null)
            {
                return;
            }

            SaveEntries(baseline.ManagedImages.Concat([entry]).ToArray());
        }
    }

    private void OnEdit(ListView list, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        ListViewItem? item = list.GetItemAt(e.X, e.Y);
        if (item?.Tag is not ManagedImageEntry entry)
        {
            return;
        }

        EditSelected(list, entry.Id);
    }

    private void EditSelected(ListView list, string? entryId = null)
    {
        ManagedImageEntry? existing = ResolveEntryForEdit(list, entryId);
        if (existing is null)
        {
            return;
        }

        string editEntryId = existing.Id;
        using (BeginModalInteraction())
        {
            using var dialog = new AddManagedImageDialog(existing);
            if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
            {
                return;
            }

            ManagedImageEntry? updated = dialog.BuildEntry(editEntryId);
            if (updated is null)
            {
                return;
            }

            ManagedImageEntry? latest = ManagedImageEntryCollection.FindById(baseline.ManagedImages, editEntryId);
            if (latest is null)
            {
                MessageBox.Show(FindForm(), "This image entry is no longer in the list. Refresh and try again.", "Edit image", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            IReadOnlyList<ManagedImageEntry> next = ManagedImageEntryCollection.ReplaceById(baseline.ManagedImages, updated);
            SaveEntries(next);
        }
    }

    private ManagedImageEntry? ResolveEntryForEdit(ListView list, string? entryId)
    {
        if (!string.IsNullOrWhiteSpace(entryId))
        {
            return ManagedImageEntryCollection.FindById(baseline.ManagedImages, entryId);
        }

        if (list.SelectedItems.Count == 0 || list.SelectedItems[0].Tag is not ManagedImageEntry selected)
        {
            return null;
        }

        return ManagedImageEntryCollection.FindById(baseline.ManagedImages, selected.Id) ?? selected;
    }

    private void RemoveSelected(ListView list)
    {
        ManagedImageEntry? existing = ResolveEntryForEdit(list, entryId: null);
        if (existing is null)
        {
            return;
        }

        using (BeginModalInteraction())
        {
            DialogResult confirm = MessageBox.Show(
                FindForm(),
                $"Remove “{existing.DisplayName}” from the managed list?",
                "Remove image",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            SaveEntries(baseline.ManagedImages.Where(entry => entry.Id != existing.Id).ToArray());
        }
    }

    private async Task InstallSelectedAsync(ListView list)
    {
        ManagedImageEntry? existing = ResolveEntryForEdit(list, entryId: null);
        if (existing is null)
        {
            return;
        }

        using (BeginModalInteraction())
        {
            string plan = ManagedImageInstallHelper.DescribeInstallPlan(existing);
            DialogResult choice = MessageBox.Show(
                FindForm(),
                $"{plan}\n\nRun this in WSL now? (Requires WSL running.)",
                "Install image",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1);
            if (choice == DialogResult.Cancel)
            {
                return;
            }

            if (choice == DialogResult.No)
            {
                try
                {
                    Clipboard.SetText(plan);
                    MessageBox.Show(FindForm(), "Install command copied to the clipboard.", "Install image", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception exception)
                {
                    MessageBox.Show(FindForm(), exception.Message, "Clipboard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                return;
            }

            IReadOnlyList<string> command = !string.IsNullOrWhiteSpace(existing.BuildContextPath)
                ? ManagedImageInstallHelper.BuildWslBuildCommand(existing)
                : ManagedImageInstallHelper.BuildWslPullCommand(existing);

            var runner = new WslCommandRunner(baseline.WslDistribution, TimeSpan.FromMinutes(30));
            WslCommandResult result = await runner.RunAsync(command, CancellationToken.None).ConfigureAwait(true);
            if (!result.Success)
            {
                string detail = !string.IsNullOrWhiteSpace(result.StandardError)
                    ? result.StandardError.Trim()
                    : result.Error ?? "Install failed";
                MessageBox.Show(FindForm(), detail, "Install failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show(FindForm(), "Install command finished successfully.", "Install image", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await RefreshStatusAsync(force: true);
        }
    }

    private void ToggleMonitor(ListView list)
    {
        ManagedImageEntry? existing = ResolveEntryForEdit(list, entryId: null);
        if (existing is null)
        {
            return;
        }

        ManagedImageEntry updated = existing with { MonitorStatus = !existing.MonitorStatus };
        SaveEntries(ManagedImageEntryCollection.ReplaceById(baseline.ManagedImages, updated));
    }

    private void OpenBuildPath(ListView list)
    {
        ManagedImageEntry? existing = ResolveEntryForEdit(list, entryId: null);
        if (existing is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(existing.BuildContextPath))
        {
            MessageBox.Show(FindForm(), "No build context path is set for this entry.", "Open path", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", existing.BuildContextPath.Trim()) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            MessageBox.Show(FindForm(), exception.Message, "Open path", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveEntries(IReadOnlyList<ManagedImageEntry> entries)
    {
        MonitorConfiguration next = baseline with { ManagedImages = entries.ToArray() };
        ConfigurationSaveResult save = configurationStore.Save(next, configurationPath);
        if (!save.Succeeded)
        {
            MessageBox.Show(FindForm(), save.Error ?? "Save failed.", "Images", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        baseline = next;
        RebuildList(runnerList, ManagedImageCategory.GitHubRunner);
        RebuildList(otherList, ManagedImageCategory.OtherWorkload);
        ManagedImagesChanged?.Invoke(this, EventArgs.Empty);
        _ = RefreshStatusAsync();
    }

    private ModalInteractionScope BeginModalInteraction() => new(this);

    private void EndModalInteraction()
    {
        modalInteractionDepth = Math.Max(0, modalInteractionDepth - 1);
        if (modalInteractionDepth > 0)
        {
            return;
        }

        if (pendingListRebuild)
        {
            pendingListRebuild = false;
            ReloadFromDisk(silent: true);
        }

        if (pendingStatusRefresh)
        {
            pendingStatusRefresh = false;
            _ = RefreshStatusAsync(force: true);
        }
    }

    private sealed class ModalInteractionScope : IDisposable
    {
        private readonly ImagesPageControl owner;
        private bool disposed;

        public ModalInteractionScope(ImagesPageControl owner)
        {
            this.owner = owner;
            owner.modalInteractionDepth++;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            owner.EndModalInteraction();
        }
    }
}
