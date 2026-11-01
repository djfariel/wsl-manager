using System.Drawing;
using System.Windows.Forms;
using WorldGate.Monitor.Core.WslConfig;
using WorldGate.Monitor.Core.WslRuntime;

namespace WorldGate.Monitor;

internal sealed class WslConfigPageControl : UserControl
{
    private static readonly Color DirtyRowBackColor = Color.FromArgb(255, 251, 204);

    private readonly WslConfigFileService configFileService = new();
    private readonly WslLifecycleService lifecycleService = new();
    private WslConfigDocument workingCopy = WslConfigDocument.CreateDefault();
    private WslConfigDocument baselineDocument = WslConfigDocument.CreateDefault();
    private string? savedBaseline;
    private bool suppressDirtyEvents;

    private readonly List<ConfigFieldRow> fieldRows = [];
    private readonly HelpProvider helpProvider = new();
    private readonly ToolTip fieldToolTip = new() { AutoPopDelay = 12000, InitialDelay = 400, ReshowDelay = 200, ShowAlways = true };

    private readonly Label pathLabel = new() { AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(640, 0) };
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

    private Panel? configScrollPanel;
    private TableLayoutPanel? configFieldLayout;

    public event EventHandler? DirtyStateChanged;

    internal Func<Task>? WslRestartCompleted { get; set; }

    public WslConfigPageControl()
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9F);
        helpProvider.HelpNamespace = string.Empty;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(8, 4, 8, 8),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var configBody = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = Padding.Empty,
        };
        configBody.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        configBody.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        configBody.Controls.Add(pathLabel, 0, 0);
        pathLabel.Margin = new Padding(4, 4, 4, 6);

        configScrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = SystemColors.Window,
            AutoScrollMargin = new Size(0, 8),
            Padding = new Padding(8, 12, 8, 8),
        };

        configFieldLayout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            Location = new Point(0, 0),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
        };
        configFieldLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28));
        configFieldLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        configFieldLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddFieldRow(configFieldLayout, "memory", "memory (e.g. 48GB)", memoryBox);
        AddFieldRow(configFieldLayout, "processors", "processors (count)", processorsBox);
        AddFieldRow(configFieldLayout, "swap", "swap (e.g. 8GB)", swapBox);
        AddFieldRow(configFieldLayout, "defaultVhdSize", "defaultVhdSize", defaultVhdSizeBox);
        AddFieldRow(configFieldLayout, "localhostForwarding", "localhostForwarding", localhostForwardingCombo);
        AddFieldRow(configFieldLayout, "nestedVirtualization", "nestedVirtualization", nestedVirtualizationCombo);
        AddFieldRow(configFieldLayout, "vmIdleTimeout", "vmIdleTimeout (ms, -1 = off)", vmIdleTimeoutBox);
        AddFieldRow(configFieldLayout, "debugConsole", "debugConsole", debugConsoleCombo);
        AddFieldRow(configFieldLayout, "safeMode", "safeMode", safeModeCombo);
        AddFieldRow(configFieldLayout, "guiApplications", "guiApplications", guiApplicationsCombo);
        AddFieldRow(configFieldLayout, "pageReporting", "pageReporting", pageReportingCombo);
        AddFieldRow(configFieldLayout, "sparseVhd", "sparseVhd", sparseVhdCombo);
        AddFieldRow(configFieldLayout, "dnsTunneling", "dnsTunneling", dnsTunnelingCombo);
        AddFieldRow(configFieldLayout, "firewall", "firewall", firewallCombo);
        AddFieldRow(configFieldLayout, "autoProxy", "autoProxy", autoProxyCombo);
        AddFieldRow(configFieldLayout, "autoMemoryReclaim", "autoMemoryReclaim", autoMemoryReclaimCombo);
        AddFieldRow(configFieldLayout, "swapFile", "swapFile (path)", swapFileBox);
        AddFieldRow(configFieldLayout, "kernel", "kernel (path)", kernelBox);
        AddFieldRow(configFieldLayout, "kernelCommandLine", "kernelCommandLine", kernelCommandLineBox);

        configScrollPanel.Controls.Add(configFieldLayout);
        configScrollPanel.Resize += (_, _) => SyncConfigLayoutWidth(configScrollPanel, configFieldLayout);
        configBody.Controls.Add(configScrollPanel, 0, 1);
        root.Controls.Add(configBody, 0, 0);

        var buttonBar = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(4, 8, 4, 4),
            WrapContents = true,
        };
        buttonBar.Controls.Add(restartButton);
        buttonBar.Controls.Add(saveButton);
        buttonBar.Controls.Add(reloadButton);
        root.Controls.Add(buttonBar, 0, 1);

        Controls.Add(root);

        saveButton.Click += async (_, _) => await SaveAsync(promptRestart: true);
        restartButton.Click += async (_, _) => await RestartWslAndRefreshAsync(showSuccess: true);
        reloadButton.Click += (_, _) => ReloadFromDisk();

        WireDirtyTracking();
        ReloadFromDisk();
    }

    public bool IsDirty()
    {
        if (savedBaseline is null)
        {
            return false;
        }

        ApplyControlsToDocument();
        string current = WslConfigIniSerializer.Serialize(workingCopy);
        return !string.Equals(current, savedBaseline, StringComparison.Ordinal);
    }

    public void ReloadOnShow()
    {
        if (IsDirty())
        {
            return;
        }

        ReloadFromDisk();
    }

    internal void SyncScrollLayoutOnShow()
    {
        if (configScrollPanel is null || configFieldLayout is null)
        {
            return;
        }

        SyncConfigLayoutWidth(configScrollPanel, configFieldLayout);
        configScrollPanel.AutoScrollPosition = new Point(0, 0);
    }

    private void ReloadFromDisk()
    {
        if (IsDirty())
        {
            DialogResult result = MessageBox.Show(
                FindForm(),
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
            MessageBox.Show(FindForm(), loaded.Error ?? "Unable to load .wslconfig.", "WSL Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        RefreshFieldRowStyles();
        RaiseDirtyStateChanged();
    }

    private async Task SaveAsync(bool promptRestart)
    {
        ApplyControlsToDocument();

        IReadOnlyList<string> validationErrors = WslConfigValidator.Validate(workingCopy);
        if (validationErrors.Count > 0)
        {
            MessageBox.Show(
                FindForm(),
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
                FindForm(),
                string.Join(Environment.NewLine, saveResult.ValidationErrors),
                "Validation failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (!saveResult.Succeeded)
        {
            MessageBox.Show(FindForm(), saveResult.Error ?? "Save failed.", "WSL Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        savedBaseline = saveResult.SavedBaseline;
        baselineDocument = workingCopy.Clone();
        pathLabel.Text = configFileService.DefaultPath;
        RefreshFieldRowStyles();
        RaiseDirtyStateChanged();

        if (promptRestart)
        {
            DialogResult restart = MessageBox.Show(
                FindForm(),
                "Saved .wslconfig. Most WSL 2 settings apply only after a full WSL shutdown.\r\n\r\nRestart WSL now (wsl --shutdown)? Running distros will stop.",
                "Restart WSL?",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1);
            if (restart == DialogResult.Yes)
            {
                await RestartWslAndRefreshAsync(showSuccess: true);
            }
        }
    }

    private async Task RestartWslAndRefreshAsync(bool showSuccess)
    {
        if (!RestartWsl(showSuccess))
        {
            return;
        }

        if (WslRestartCompleted is not null)
        {
            await WslRestartCompleted();
        }
    }

    private bool RestartWsl(bool showSuccess)
    {
        WslShutdownResult result = lifecycleService.TryShutdown();
        if (!result.Succeeded)
        {
            MessageBox.Show(FindForm(), result.Error ?? "WSL shutdown failed.", "Restart WSL", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        if (showSuccess)
        {
            MessageBox.Show(
                FindForm(),
                "WSL has been shut down. It will start again the next time you launch a distribution.",
                "Restart WSL",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        return true;
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

    private void WireDirtyTracking()
    {
        void MarkDirty(object? _, EventArgs __)
        {
            if (!suppressDirtyEvents)
            {
                RefreshFieldRowStyles();
                RaiseDirtyStateChanged();
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

    private void RaiseDirtyStateChanged() => DirtyStateChanged?.Invoke(this, EventArgs.Empty);

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
