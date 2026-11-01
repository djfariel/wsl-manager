using System.Drawing;
using System.Windows.Forms;

namespace WorldGate.Monitor;

internal sealed class SettingsPageControl : UserControl
{
    private static readonly Color DirtyRowBackColor = Color.FromArgb(255, 251, 204);

    private readonly ConfigurationStore configurationStore = new();
    private readonly ToolTip fieldToolTip = new() { AutoPopDelay = 12000, InitialDelay = 400, ReshowDelay = 200, ShowAlways = true };

    private MonitorConfiguration baseline = new();
    private string configurationPath = string.Empty;
    private bool suppressDirtyEvents;

    private readonly Label pathLabel = new() { AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(640, 0) };
    private readonly TextBox repositoryBox = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
    private readonly TextBox pollIntervalBox = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
    private readonly TextBox wslDistributionBox = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
    private readonly Button saveButton = new() { Text = "Save settings", AutoSize = true, MinimumSize = new Size(120, 28) };
    private readonly Button reloadButton = new() { Text = "Reload", AutoSize = true, MinimumSize = new Size(90, 28) };

    public event EventHandler? DirtyStateChanged;

    public SettingsPageControl()
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(8, 4, 8, 8),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        pathLabel.Margin = new Padding(4, 4, 4, 8);
        root.Controls.Add(pathLabel, 0, 0);

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(4, 0, 4, 0),
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddFieldRow(fields, "repository", "repository (owner/name)", repositoryBox,
            "GitHub repository monitored for Actions and ARC (owner/name).");
        AddFieldRow(fields, "pollIntervalSeconds", "pollIntervalSeconds", pollIntervalBox,
            "Tray poll interval in seconds (2–300). Restart the tray app to apply.");
        AddFieldRow(fields, "wslDistribution", "wslDistribution", wslDistributionBox,
            "WSL distribution name for kubectl/gh commands. Leave empty to use the default distro.");

        var todo = new Label
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Padding = new Padding(32, 12, 4, 4),
            Text = "TODO: runnerScaleSetName, controllerNamespace, runnerNamespace, queueWarningSeconds",
        };

        var fieldsHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var fieldsStack = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Dock = DockStyle.Top,
        };
        fieldsStack.Controls.Add(fields);
        fieldsStack.Controls.Add(todo);
        fieldsHost.Controls.Add(fieldsStack);
        root.Controls.Add(fieldsHost, 0, 1);

        var buttonBar = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(4, 8, 4, 4),
            WrapContents = true,
        };
        buttonBar.Controls.Add(saveButton);
        buttonBar.Controls.Add(reloadButton);
        root.Controls.Add(buttonBar, 0, 2);

        Controls.Add(root);

        saveButton.Click += (_, _) => SaveToDisk();
        reloadButton.Click += (_, _) => ReloadFromDisk();

        repositoryBox.TextChanged += OnFieldChanged;
        pollIntervalBox.TextChanged += OnFieldChanged;
        wslDistributionBox.TextChanged += OnFieldChanged;

        ReloadFromDisk(silent: true);
    }

    public bool IsDirty() => !ConfigurationsEqual(ReadControls(), baseline);

    public void ReloadOnShow()
    {
        if (IsDirty())
        {
            return;
        }

        ReloadFromDisk(silent: true);
    }

    private void OnFieldChanged(object? sender, EventArgs e)
    {
        if (suppressDirtyEvents)
        {
            return;
        }

        ApplyDirtyStyles();
        RaiseDirtyStateChanged();
    }

    private void RaiseDirtyStateChanged() => DirtyStateChanged?.Invoke(this, EventArgs.Empty);

    private void ReloadFromDisk(bool silent = false)
    {
        if (!silent && IsDirty())
        {
            DialogResult result = MessageBox.Show(
                FindForm(),
                "Reload from disk and discard unsaved edits?",
                "Reload settings",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (result != DialogResult.Yes)
            {
                return;
            }
        }

        ConfigurationLoadResult loaded = configurationStore.LoadOrCreate();
        configurationPath = loaded.ConfigurationPath;
        baseline = loaded.Configuration with { };
        pathLabel.Text = string.IsNullOrWhiteSpace(loaded.Error)
            ? loaded.ConfigurationPath
            : $"{loaded.ConfigurationPath} — {loaded.Error}";

        suppressDirtyEvents = true;
        try
        {
            ApplyConfigurationToControls(baseline);
        }
        finally
        {
            suppressDirtyEvents = false;
        }

        ApplyDirtyStyles();
        RaiseDirtyStateChanged();
    }

    private void SaveToDisk()
    {
        MonitorConfiguration? edited = ReadControls();
        if (edited is null)
        {
            MessageBox.Show(
                FindForm(),
                "pollIntervalSeconds must be a whole number between 2 and 300.",
                "Validation failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        ConfigurationSaveResult saveResult = configurationStore.Save(edited, configurationPath);
        if (!saveResult.Succeeded)
        {
            MessageBox.Show(
                FindForm(),
                saveResult.Error ?? "Save failed.",
                "Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        baseline = edited;
        ApplyDirtyStyles();
        RaiseDirtyStateChanged();

        MessageBox.Show(
            FindForm(),
            "Saved config.json. Restart the tray app for poll interval and WSL distribution changes to take effect.",
            "Settings saved",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private MonitorConfiguration? ReadControls()
    {
        if (!int.TryParse(pollIntervalBox.Text.Trim(), out int pollInterval))
        {
            return null;
        }

        string repo = repositoryBox.Text.Trim();
        string? wslDist = string.IsNullOrWhiteSpace(wslDistributionBox.Text)
            ? null
            : wslDistributionBox.Text.Trim();

        return baseline with
        {
            Repository = repo,
            PollIntervalSeconds = pollInterval,
            WslDistribution = wslDist,
        };
    }

    private void ApplyConfigurationToControls(MonitorConfiguration configuration)
    {
        repositoryBox.Text = configuration.Repository;
        pollIntervalBox.Text = configuration.PollIntervalSeconds.ToString();
        wslDistributionBox.Text = configuration.WslDistribution ?? string.Empty;
    }

    private void ApplyDirtyStyles()
    {
        ApplyEditorDirty(repositoryBox, !string.Equals(Normalize(repositoryBox.Text), Normalize(baseline.Repository), StringComparison.Ordinal));
        ApplyEditorDirty(
            pollIntervalBox,
            !int.TryParse(pollIntervalBox.Text.Trim(), out int poll) || poll != baseline.PollIntervalSeconds);
        ApplyEditorDirty(
            wslDistributionBox,
            !string.Equals(NormalizeOptional(wslDistributionBox.Text), NormalizeOptional(baseline.WslDistribution), StringComparison.Ordinal));
    }

    private void ApplyEditorDirty(TextBox box, bool dirty)
    {
        box.BackColor = dirty ? DirtyRowBackColor : SystemColors.Window;
        FontStyle style = dirty ? FontStyle.Bold : FontStyle.Regular;
        if (box.Font.Style != style)
        {
            box.Font = new Font(Font, style);
        }
    }

    private static bool ConfigurationsEqual(MonitorConfiguration? current, MonitorConfiguration baselineConfig)
    {
        if (current is null)
        {
            return false;
        }

        return string.Equals(Normalize(current.Repository), Normalize(baselineConfig.Repository), StringComparison.Ordinal)
            && current.PollIntervalSeconds == baselineConfig.PollIntervalSeconds
            && string.Equals(NormalizeOptional(current.WslDistribution), NormalizeOptional(baselineConfig.WslDistribution), StringComparison.Ordinal);
    }

    private static string Normalize(string value) => value.Trim();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void AddFieldRow(TableLayoutPanel layout, string fieldKey, string labelText, Control editor, string helpText)
    {
        int row = layout.RowCount;
        layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var help = new Label
        {
            Text = "?",
            AutoSize = true,
            Anchor = AnchorStyles.None,
            ForeColor = Color.FromArgb(0, 102, 204),
            Font = new Font(Font.FontFamily, Font.Size, FontStyle.Bold),
            Cursor = Cursors.Help,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 6, 0, 6),
        };
        fieldToolTip.SetToolTip(help, helpText);
        fieldToolTip.SetToolTip(editor, helpText);

        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Padding = new Padding(0, 6, 8, 6),
        };
        editor.Margin = new Padding(0, 3, 0, 3);

        layout.Controls.Add(help, 0, row);
        layout.Controls.Add(label, 1, row);
        layout.Controls.Add(editor, 2, row);
    }
}
