using System.Drawing;
using System.Windows.Forms;
using WorldGate.Monitor.Core.WslConfig;
using WorldGate.Monitor.Core.WslRuntime;

namespace WorldGate.Monitor;

internal sealed class WslManagerMainForm : Form
{
    private const int StatusPollIntervalMs = 5000;

    private readonly WslConfigFileService configFileService = new();
    private readonly WslLifecycleService lifecycleService = new();
    private readonly WslRuntimeStatusService runtimeStatusService = new();
    private WslConfigDocument workingCopy = WslConfigDocument.CreateDefault();
    private WslConfigDocument baselineDocument = WslConfigDocument.CreateDefault();
    private string? savedBaseline;
    private bool suppressDirtyEvents;

    private static readonly Color DirtyRowBackColor = Color.FromArgb(255, 251, 204);
    private readonly List<ConfigFieldRow> fieldRows = [];

    private readonly HelpProvider helpProvider = new();
    private readonly ToolTip fieldToolTip = new() { AutoPopDelay = 12000, InitialDelay = 400, ReshowDelay = 200, ShowAlways = true };

    private readonly Label pathLabel = new() { AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(640, 0) };
    private readonly StatusBadgeControl wslRuntimeBadge = new();
    private readonly System.Windows.Forms.Timer statusPollTimer = new() { Interval = StatusPollIntervalMs };

    private readonly TextBox memoryBox = CreateTextBox();
    private readonly TextBox processorsBox = CreateTextBox();
    private readonly TextBox swapBox = CreateTextBox();
    private readonly TextBox defaultVhdSizeBox = CreateTextBox();
    private readonly TextBox vmIdleTimeoutBox = CreateTextBox();
    private readonly TextBox swapFileBox = CreateTextBox();
    private readonly TextBox kernelBox = CreateTextBox();
    private readonly TextBox kernelCommandLineBox = CreateTextBox();
    private readonly ComboBox localhostForwardingCombo = CreateBooleanCombo();
    private readonly ComboBox nestedVirtualizationCombo = CreateBooleanCombo();
    private readonly ComboBox debugConsoleCombo = CreateBooleanCombo();
    private readonly ComboBox safeModeCombo = CreateBooleanCombo();
    private readonly ComboBox guiApplicationsCombo = CreateBooleanCombo();
    private readonly ComboBox pageReportingCombo = CreateBooleanCombo();
    private readonly ComboBox sparseVhdCombo = CreateBooleanCombo();
    private readonly ComboBox dnsTunnelingCombo = CreateBooleanCombo();
    private readonly ComboBox firewallCombo = CreateBooleanCombo();
    private readonly ComboBox autoProxyCombo = CreateBooleanCombo();
    private readonly ComboBox autoMemoryReclaimCombo = CreateAutoMemoryReclaimCombo();
    private readonly Button saveButton = new() { Text = "Save .wslconfig", AutoSize = true, MinimumSize = new Size(120, 28) };
    private readonly Button restartButton = new() { Text = "Restart WSL", AutoSize = true, MinimumSize = new Size(120, 28) };
    private readonly Button reloadButton = new() { Text = "Reload", AutoSize = true, MinimumSize = new Size(90, 28) };

    public WslManagerMainForm()
    {
        Text = "WSL Manager";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(680, 560);
        ClientSize = new Size(760, 640);
        Font = new Font("Segoe UI", 9F);
        helpProvider.HelpNamespace = string.Empty;

        wslRuntimeBadge.SetCaption("WSL runtime");

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var statusGroup = CreateGroupBox("Monitor", DockStyle.Top);
        var badgeRow = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(4),
        };
        badgeRow.Controls.Add(wslRuntimeBadge);
        statusGroup.Controls.Add(badgeRow);

        var configGroup = CreateGroupBox("WSL configuration", DockStyle.Fill);

        var configBody = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(4, 0, 4, 0),
        };
        configBody.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        configBody.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        configBody.Controls.Add(pathLabel, 0, 0);
        pathLabel.Margin = new Padding(4, 4, 4, 6);
        pathLabel.Padding = Padding.Empty;

        var scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = SystemColors.Window,
            AutoScrollMargin = new Size(0, 8),
            Padding = new Padding(8, 12, 8, 8),
        };

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            Location = new Point(0, 0),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddFieldRow(layout, "memory", "memory (e.g. 48GB)", memoryBox);
        AddFieldRow(layout, "processors", "processors (count)", processorsBox);
        AddFieldRow(layout, "swap", "swap (e.g. 8GB)", swapBox);
        AddFieldRow(layout, "defaultVhdSize", "defaultVhdSize", defaultVhdSizeBox);
        AddFieldRow(layout, "localhostForwarding", "localhostForwarding", localhostForwardingCombo);
        AddFieldRow(layout, "nestedVirtualization", "nestedVirtualization", nestedVirtualizationCombo);
        AddFieldRow(layout, "vmIdleTimeout", "vmIdleTimeout (ms, -1 = off)", vmIdleTimeoutBox);
        AddFieldRow(layout, "debugConsole", "debugConsole", debugConsoleCombo);
        AddFieldRow(layout, "safeMode", "safeMode", safeModeCombo);
        AddFieldRow(layout, "guiApplications", "guiApplications", guiApplicationsCombo);
        AddFieldRow(layout, "pageReporting", "pageReporting", pageReportingCombo);
        AddFieldRow(layout, "sparseVhd", "sparseVhd", sparseVhdCombo);
        AddFieldRow(layout, "dnsTunneling", "dnsTunneling", dnsTunnelingCombo);
        AddFieldRow(layout, "firewall", "firewall", firewallCombo);
        AddFieldRow(layout, "autoProxy", "autoProxy", autoProxyCombo);
        AddFieldRow(layout, "autoMemoryReclaim", "autoMemoryReclaim", autoMemoryReclaimCombo);
        AddFieldRow(layout, "swapFile", "swapFile (path)", swapFileBox);
        AddFieldRow(layout, "kernel", "kernel (path)", kernelBox);
        AddFieldRow(layout, "kernelCommandLine", "kernelCommandLine", kernelCommandLineBox);

        scroll.Controls.Add(layout);
        scroll.Resize += (_, _) => SyncConfigLayoutWidth(scroll, layout);
        configBody.Controls.Add(scroll, 0, 1);
        configGroup.Controls.Add(configBody);

        var actionsGroup = CreateGroupBox("Actions", DockStyle.Top);
        var buttonBar = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8, 4, 8, 4),
            WrapContents = true,
        };
        buttonBar.Controls.Add(restartButton);
        buttonBar.Controls.Add(saveButton);
        buttonBar.Controls.Add(reloadButton);
        actionsGroup.Controls.Add(buttonBar);

        root.Controls.Add(statusGroup, 0, 0);
        root.Controls.Add(configGroup, 0, 1);
        root.Controls.Add(actionsGroup, 0, 2);
        Controls.Add(root);

        saveButton.Click += async (_, _) => await SaveAsync(promptRestart: true);
        restartButton.Click += async (_, _) =>
        {
            RestartWsl(showSuccess: true);
            await RefreshRuntimeBadgeAsync();
        };
        reloadButton.Click += (_, _) => ReloadFromDisk();

        statusPollTimer.Tick += async (_, _) => await RefreshRuntimeBadgeAsync();
        Shown += async (_, _) =>
        {
            SyncConfigLayoutWidth(scroll, layout);
            scroll.AutoScrollPosition = new Point(0, 0);
            statusPollTimer.Start();
            await RefreshRuntimeBadgeAsync();
        };
        FormClosed += (_, _) => statusPollTimer.Stop();

        WireDirtyTracking();
        ReloadFromDisk();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (IsDirty() && e.CloseReason == CloseReason.UserClosing)
        {
            DialogResult result = MessageBox.Show(
                this,
                "You have unsaved .wslconfig changes. Close without saving?",
                "Unsaved changes",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (result != DialogResult.Yes)
            {
                e.Cancel = true;
            }
        }

        base.OnFormClosing(e);
    }

    private async Task RefreshRuntimeBadgeAsync()
    {
        WslRuntimeSnapshot snapshot = await Task.Run(runtimeStatusService.GetSnapshot);
        if (IsDisposed)
        {
            return;
        }

        wslRuntimeBadge.ApplySnapshot(snapshot);
    }

    private void ReloadFromDisk()
    {
        if (IsDirty())
        {
            DialogResult result = MessageBox.Show(
                this,
                "Reload from disk and discard unsaved edits?",
                "Reload .wslconfig",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (result != DialogResult.Yes)
            {
                return;
            }
        }

        WslConfigLoadResult loaded = configFileService.LoadOrCreateDefault();
        if (!loaded.Succeeded || loaded.Document is null)
        {
            MessageBox.Show(this, loaded.Error ?? "Unable to load .wslconfig.", "WSL Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        workingCopy = loaded.Document.Clone();
        baselineDocument = workingCopy.Clone();
        savedBaseline = loaded.SavedBaseline ?? WslConfigIniSerializer.Serialize(workingCopy);
        pathLabel.Text = loaded.CreatedFromDefault
            ? $"File will be created on save: {loaded.Path}"
            : loaded.Path;

        suppressDirtyEvents = true;
        try
        {
            memoryBox.Text = workingCopy.Memory ?? string.Empty;
            processorsBox.Text = workingCopy.Processors ?? string.Empty;
            swapBox.Text = workingCopy.Swap ?? string.Empty;
            defaultVhdSizeBox.Text = workingCopy.DefaultVhdSize ?? string.Empty;
            vmIdleTimeoutBox.Text = workingCopy.VmIdleTimeout ?? string.Empty;
            swapFileBox.Text = workingCopy.SwapFile ?? string.Empty;
            kernelBox.Text = workingCopy.Kernel ?? string.Empty;
            kernelCommandLineBox.Text = workingCopy.KernelCommandLine ?? string.Empty;
            SetBooleanCombo(localhostForwardingCombo, workingCopy.LocalhostForwarding);
            SetBooleanCombo(nestedVirtualizationCombo, workingCopy.NestedVirtualization);
            SetBooleanCombo(debugConsoleCombo, workingCopy.DebugConsole);
            SetBooleanCombo(safeModeCombo, workingCopy.SafeMode);
            SetBooleanCombo(guiApplicationsCombo, workingCopy.GuiApplications);
            SetBooleanCombo(pageReportingCombo, workingCopy.PageReporting);
            SetBooleanCombo(sparseVhdCombo, workingCopy.SparseVhd);
            SetBooleanCombo(dnsTunnelingCombo, workingCopy.DnsTunneling);
            SetBooleanCombo(firewallCombo, workingCopy.Firewall);
            SetBooleanCombo(autoProxyCombo, workingCopy.AutoProxy);
            SetAutoMemoryReclaimCombo(autoMemoryReclaimCombo, workingCopy.AutoMemoryReclaim);
        }
        finally
        {
            suppressDirtyEvents = false;
        }

        UpdateTitle();
        RefreshFieldRowStyles();
    }

    private async Task SaveAsync(bool promptRestart)
    {
        ApplyControlsToDocument();

        IReadOnlyList<string> validationErrors = WslConfigValidator.Validate(workingCopy);
        if (validationErrors.Count > 0)
        {
            MessageBox.Show(
                this,
                string.Join(Environment.NewLine, validationErrors),
                "Validation failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        WslConfigSaveResult saveResult = configFileService.Save(workingCopy);
        if (saveResult.ValidationErrors is { Count: > 0 })
        {
            MessageBox.Show(
                this,
                string.Join(Environment.NewLine, saveResult.ValidationErrors),
                "Validation failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (!saveResult.Succeeded)
        {
            MessageBox.Show(this, saveResult.Error ?? "Save failed.", "WSL Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        savedBaseline = saveResult.SavedBaseline;
        baselineDocument = workingCopy.Clone();
        pathLabel.Text = configFileService.DefaultPath;
        UpdateTitle();
        RefreshFieldRowStyles();

        if (promptRestart)
        {
            DialogResult restart = MessageBox.Show(
                this,
                "Saved .wslconfig. Most WSL 2 settings apply only after a full WSL shutdown.\r\n\r\nRestart WSL now (wsl --shutdown)? Running distros will stop.",
                "Restart WSL?",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1);
            if (restart == DialogResult.Yes)
            {
                RestartWsl(showSuccess: true);
                await RefreshRuntimeBadgeAsync();
            }
        }
    }

    private void RestartWsl(bool showSuccess)
    {
        WslShutdownResult result = lifecycleService.TryShutdown();
        if (!result.Succeeded)
        {
            MessageBox.Show(this, result.Error ?? "WSL shutdown failed.", "Restart WSL", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (showSuccess)
        {
            MessageBox.Show(
                this,
                "WSL has been shut down. It will start again the next time you launch a distribution.",
                "Restart WSL",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private void ApplyControlsToDocument()
    {
        workingCopy.Memory = NullIfEmpty(memoryBox.Text);
        workingCopy.Processors = NullIfEmpty(processorsBox.Text);
        workingCopy.Swap = NullIfEmpty(swapBox.Text);
        workingCopy.DefaultVhdSize = NullIfEmpty(defaultVhdSizeBox.Text);
        workingCopy.VmIdleTimeout = NullIfEmpty(vmIdleTimeoutBox.Text);
        workingCopy.SwapFile = NullIfEmpty(swapFileBox.Text);
        workingCopy.Kernel = NullIfEmpty(kernelBox.Text);
        workingCopy.KernelCommandLine = NullIfEmpty(kernelCommandLineBox.Text);
        workingCopy.LocalhostForwarding = GetBooleanCombo(localhostForwardingCombo);
        workingCopy.NestedVirtualization = GetBooleanCombo(nestedVirtualizationCombo);
        workingCopy.DebugConsole = GetBooleanCombo(debugConsoleCombo);
        workingCopy.SafeMode = GetBooleanCombo(safeModeCombo);
        workingCopy.GuiApplications = GetBooleanCombo(guiApplicationsCombo);
        workingCopy.PageReporting = GetBooleanCombo(pageReportingCombo);
        workingCopy.SparseVhd = GetBooleanCombo(sparseVhdCombo);
        workingCopy.DnsTunneling = GetBooleanCombo(dnsTunnelingCombo);
        workingCopy.Firewall = GetBooleanCombo(firewallCombo);
        workingCopy.AutoProxy = GetBooleanCombo(autoProxyCombo);
        workingCopy.AutoMemoryReclaim = GetAutoMemoryReclaimCombo(autoMemoryReclaimCombo);
    }

    private bool IsDirty()
    {
        if (savedBaseline is null)
        {
            return false;
        }

        ApplyControlsToDocument();
        string current = WslConfigIniSerializer.Serialize(workingCopy);
        return !string.Equals(current, savedBaseline, StringComparison.Ordinal);
    }

    internal void ReloadOnShow()
    {
        if (IsDirty())
        {
            return;
        }

        ReloadFromDisk();
    }

    private void UpdateTitle()
    {
        Text = IsDirty() ? "WSL Manager *" : "WSL Manager";
    }

    private void WireDirtyTracking()
    {
        void MarkDirty(object? _, EventArgs __)
        {
            if (!suppressDirtyEvents)
            {
                UpdateTitle();
                RefreshFieldRowStyles();
            }
        }

        foreach (Control control in new Control[]
                 {
                     memoryBox, processorsBox, swapBox, defaultVhdSizeBox, vmIdleTimeoutBox,
                     swapFileBox, kernelBox, kernelCommandLineBox,
                 })
        {
            control.TextChanged += MarkDirty;
        }

        foreach (ComboBox combo in new ComboBox[]
                 {
                     localhostForwardingCombo, nestedVirtualizationCombo, debugConsoleCombo, safeModeCombo,
                     guiApplicationsCombo, pageReportingCombo, sparseVhdCombo, dnsTunnelingCombo,
                     firewallCombo, autoProxyCombo, autoMemoryReclaimCombo,
                 })
        {
            combo.SelectedIndexChanged += MarkDirty;
        }
    }

    private void AddFieldRow(TableLayoutPanel layout, string fieldKey, string labelText, Control editor)
    {
        int row = layout.RowCount;
        layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var helpButton = CreateHelpIndicator(fieldKey, editor);
        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Padding = new Padding(0, 6, 8, 6),
        };
        editor.Margin = new Padding(0, 3, 0, 3);

        layout.Controls.Add(helpButton, 0, row);
        layout.Controls.Add(label, 1, row);
        layout.Controls.Add(editor, 2, row);

        fieldRows.Add(new ConfigFieldRow(fieldKey, helpButton, label, editor, () => GetControlValue(fieldKey, editor)));
    }

    private string? GetControlValue(string fieldKey, Control editor) => editor switch
    {
        TextBox textBox => NullIfEmpty(textBox.Text),
        ComboBox combo => fieldKey.Equals("autoMemoryReclaim", StringComparison.OrdinalIgnoreCase)
            ? GetAutoMemoryReclaimCombo(combo)
            : GetBooleanCombo(combo),
        _ => null,
    };

    private void RefreshFieldRowStyles()
    {
        foreach (ConfigFieldRow row in fieldRows)
        {
            string? current = row.GetCurrentValue();
            string? baseline = baselineDocument.GetKnown(row.FieldKey);
            bool rowDirty = !FieldValuesEqual(current, baseline);
            ApplyFieldRowStyle(row, rowDirty);
        }
    }

    private static bool FieldValuesEqual(string? current, string? baseline) =>
        string.Equals(NormalizeFieldValue(current), NormalizeFieldValue(baseline), StringComparison.Ordinal);

    private static string? NormalizeFieldValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void ApplyFieldRowStyle(ConfigFieldRow row, bool rowDirty)
    {
        row.Editor.BackColor = rowDirty ? DirtyRowBackColor : SystemColors.Window;

        FontStyle editorStyle = rowDirty ? FontStyle.Bold : FontStyle.Regular;
        if (row.Editor.Font.Style != editorStyle)
        {
            row.Editor.Font = new Font(Font, editorStyle);
        }
    }

    private static void SyncConfigLayoutWidth(Panel scroll, TableLayoutPanel layout)
    {
        int width = scroll.ClientSize.Width - scroll.Padding.Horizontal;
        if (width > 0 && layout.Width != width)
        {
            layout.Width = width;
        }
    }

    private sealed class ConfigFieldRow(
        string fieldKey,
        Control help,
        Label caption,
        Control editor,
        Func<string?> getCurrentValue)
    {
        public string FieldKey { get; } = fieldKey;
        public Control Help { get; } = help;
        public Label Caption { get; } = caption;
        public Control Editor { get; } = editor;
        public Func<string?> GetCurrentValue { get; } = getCurrentValue;
    }

    private Control CreateHelpIndicator(string fieldKey, Control editor)
    {
        string helpText = WslConfigFieldHelp.Get(fieldKey);
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
        helpProvider.SetShowHelp(help, true);
        helpProvider.SetHelpString(help, helpText);
        helpProvider.SetShowHelp(editor, true);
        helpProvider.SetHelpString(editor, helpText);

        return help;
    }

    private static GroupBox CreateGroupBox(string title, DockStyle dock) =>
        new()
        {
            Text = title,
            AutoSize = dock == DockStyle.Top,
            Dock = dock,
            Padding = new Padding(8, 4, 8, 8),
        };

    private static TextBox CreateTextBox() => new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };

    private static ComboBox CreateBooleanCombo()
    {
        var combo = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        combo.Items.AddRange(["(not set)", "true", "false"]);
        combo.SelectedIndex = 0;
        return combo;
    }

    private static ComboBox CreateAutoMemoryReclaimCombo()
    {
        var combo = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        combo.Items.AddRange(["(not set)", "disabled", "gradual"]);
        combo.SelectedIndex = 0;
        return combo;
    }

    private static string? NullIfEmpty(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static void SetBooleanCombo(ComboBox combo, string? value)
    {
        combo.SelectedIndex = value switch
        {
            null or "" => 0,
            _ when value.Equals("true", StringComparison.OrdinalIgnoreCase) => 1,
            _ when value.Equals("false", StringComparison.OrdinalIgnoreCase) => 2,
            _ => 0,
        };
    }

    private static string? GetBooleanCombo(ComboBox combo) => combo.SelectedIndex switch
    {
        1 => "true",
        2 => "false",
        _ => null,
    };

    private static void SetAutoMemoryReclaimCombo(ComboBox combo, string? value)
    {
        combo.SelectedIndex = value switch
        {
            null or "" => 0,
            _ when value.Equals("disabled", StringComparison.OrdinalIgnoreCase) => 1,
            _ when value.Equals("gradual", StringComparison.OrdinalIgnoreCase) => 2,
            _ => 0,
        };
    }

    private static string? GetAutoMemoryReclaimCombo(ComboBox combo) => combo.SelectedIndex switch
    {
        1 => "disabled",
        2 => "gradual",
        _ => null,
    };
}
