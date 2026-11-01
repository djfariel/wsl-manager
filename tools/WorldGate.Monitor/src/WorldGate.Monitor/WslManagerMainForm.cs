using System.Drawing;
using System.Windows.Forms;
using WorldGate.Monitor.Core.WslConfig;

namespace WorldGate.Monitor;

internal sealed class WslManagerMainForm : Form
{
    private readonly WslConfigFileService configFileService = new();
    private readonly WslLifecycleService lifecycleService = new();
    private WslConfigDocument workingCopy = WslConfigDocument.CreateDefault();
    private string? savedBaseline;
    private bool suppressDirtyEvents;

    private readonly Label pathLabel = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
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
    private readonly Button saveButton = new() { Text = "Save .wslconfig", AutoSize = true };
    private readonly Button restartButton = new() { Text = "Restart WSL", AutoSize = true };
    private readonly Button reloadButton = new() { Text = "Reload", AutoSize = true };

    public WslManagerMainForm()
    {
        Text = "WSL Manager";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(640, 520);
        ClientSize = new Size(720, 580);
        Font = new Font("Segoe UI", 9F);

        var header = new Label
        {
            Text = "WSL 2 global settings (.wslconfig)",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
        };

        var scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(12),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(layout, "memory (e.g. 48GB)", memoryBox);
        AddRow(layout, "processors (count)", processorsBox);
        AddRow(layout, "swap (e.g. 8GB)", swapBox);
        AddRow(layout, "defaultVhdSize", defaultVhdSizeBox);
        AddRow(layout, "localhostForwarding", localhostForwardingCombo);
        AddRow(layout, "nestedVirtualization", nestedVirtualizationCombo);
        AddRow(layout, "vmIdleTimeout (ms, -1 = off)", vmIdleTimeoutBox);
        AddRow(layout, "debugConsole", debugConsoleCombo);
        AddRow(layout, "safeMode", safeModeCombo);
        AddRow(layout, "guiApplications", guiApplicationsCombo);
        AddRow(layout, "pageReporting", pageReportingCombo);
        AddRow(layout, "sparseVhd", sparseVhdCombo);
        AddRow(layout, "dnsTunneling", dnsTunnelingCombo);
        AddRow(layout, "firewall", firewallCombo);
        AddRow(layout, "autoProxy", autoProxyCombo);
        AddRow(layout, "autoMemoryReclaim", autoMemoryReclaimCombo);
        AddRow(layout, "swapFile (path)", swapFileBox);
        AddRow(layout, "kernel (path)", kernelBox);
        AddRow(layout, "kernelCommandLine", kernelCommandLineBox);

        scroll.Controls.Add(layout);

        var buttonBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12, 8, 12, 12),
            WrapContents = false,
        };
        buttonBar.Controls.Add(restartButton);
        buttonBar.Controls.Add(saveButton);
        buttonBar.Controls.Add(reloadButton);

        var top = new Panel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12, 12, 12, 0) };
        top.Controls.Add(pathLabel);
        top.Controls.Add(header);
        header.Location = new Point(0, pathLabel.Height + 4);
        pathLabel.Dock = DockStyle.Top;
        header.Dock = DockStyle.Top;

        Controls.Add(scroll);
        Controls.Add(buttonBar);
        Controls.Add(top);

        saveButton.Click += async (_, _) => await SaveAsync(promptRestart: true);
        restartButton.Click += (_, _) => RestartWsl(showSuccess: true);
        reloadButton.Click += (_, _) => ReloadFromDisk();

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
        pathLabel.Text = configFileService.DefaultPath;
        UpdateTitle();

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
        ApplyControlsToDocument();
        string current = WslConfigIniSerializer.Serialize(workingCopy);
        return savedBaseline is null || !string.Equals(current, savedBaseline, StringComparison.Ordinal);
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

    private static void AddRow(TableLayoutPanel layout, string labelText, Control editor)
    {
        int row = layout.RowCount;
        layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Padding = new Padding(0, 6, 8, 6),
        };
        editor.Margin = new Padding(0, 3, 0, 3);
        layout.Controls.Add(label, 0, row);
        layout.Controls.Add(editor, 1, row);
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
