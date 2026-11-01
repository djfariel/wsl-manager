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

    public event EventHandler? ManagedImagesChanged;

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
        refreshButton.Click += async (_, _) => await RefreshStatusAsync();

        ReloadFromDisk(silent: true);
    }

    public IReadOnlyList<ManagedImageEntry> GetManagedImages() => baseline.ManagedImages;

    public async Task RefreshStatusAsync()
    {
        IReadOnlyList<ManagedImageStatusSnapshot> snapshots =
            await statusService.ProbeAllAsync(baseline.ManagedImages).ConfigureAwait(true);
        statusById = snapshots.ToDictionary(snapshot => snapshot.EntryId, snapshot => snapshot);
        PopulateList(runnerList, ManagedImageCategory.GitHubRunner);
        PopulateList(otherList, ManagedImageCategory.OtherWorkload);
    }

    public void ReloadOnShow()
    {
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
        ConfigurationLoadResult loaded = configurationStore.LoadOrCreate();
        configurationPath = loaded.ConfigurationPath;
        baseline = loaded.Configuration with { ManagedImages = loaded.Configuration.ManagedImages.ToArray() };
        pathLabel.Text = string.IsNullOrWhiteSpace(loaded.Error)
            ? $"Stored in {loaded.ConfigurationPath}"
            : $"{loaded.ConfigurationPath} — {loaded.Error}";

        EnsureColumns(runnerList);
        EnsureColumns(otherList);
        PopulateList(runnerList, ManagedImageCategory.GitHubRunner);
        PopulateList(otherList, ManagedImageCategory.OtherWorkload);
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

    private void PopulateList(ListView list, ManagedImageCategory category)
    {
        list.Items.Clear();
        foreach (ManagedImageEntry entry in baseline.ManagedImages.Where(entry => entry.Category == category))
        {
            string location = FormatLocation(entry);
            statusById.TryGetValue(entry.Id, out ManagedImageStatusSnapshot? snapshot);
            string status = snapshot?.Summary ?? "—";
            var item = new ListViewItem(entry.DisplayName);
            item.SubItems.Add(location);
            item.SubItems.Add(entry.MonitorStatus ? "On" : "Off");
            item.SubItems.Add(status);
            item.SubItems.Add("Right-click");
            item.Tag = entry;
            list.Items.Add(item);
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

    private void OnEdit(ListView list, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        EditSelected(list);
    }

    private void EditSelected(ListView list)
    {
        if (list.SelectedItems.Count == 0 || list.SelectedItems[0].Tag is not ManagedImageEntry existing)
        {
            return;
        }

        using var dialog = new AddManagedImageDialog(existing);
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
        {
            return;
        }

        ManagedImageEntry? updated = dialog.BuildEntry(existing.Id);
        if (updated is null)
        {
            return;
        }

        ManagedImageEntry[] next = baseline.ManagedImages
            .Select(entry => entry.Id == existing.Id ? updated : entry)
            .ToArray();
        SaveEntries(next);
    }

    private void RemoveSelected(ListView list)
    {
        if (list.SelectedItems.Count == 0 || list.SelectedItems[0].Tag is not ManagedImageEntry existing)
        {
            return;
        }

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

    private async Task InstallSelectedAsync(ListView list)
    {
        if (list.SelectedItems.Count == 0 || list.SelectedItems[0].Tag is not ManagedImageEntry existing)
        {
            return;
        }

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
        await RefreshStatusAsync();
    }

    private void ToggleMonitor(ListView list)
    {
        if (list.SelectedItems.Count == 0 || list.SelectedItems[0].Tag is not ManagedImageEntry existing)
        {
            return;
        }

        ManagedImageEntry updated = existing with { MonitorStatus = !existing.MonitorStatus };
        SaveEntries(baseline.ManagedImages.Select(entry => entry.Id == existing.Id ? updated : entry).ToArray());
    }

    private void OpenBuildPath(ListView list)
    {
        if (list.SelectedItems.Count == 0 || list.SelectedItems[0].Tag is not ManagedImageEntry existing)
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
        PopulateList(runnerList, ManagedImageCategory.GitHubRunner);
        PopulateList(otherList, ManagedImageCategory.OtherWorkload);
        ManagedImagesChanged?.Invoke(this, EventArgs.Empty);
        _ = RefreshStatusAsync();
    }
}
