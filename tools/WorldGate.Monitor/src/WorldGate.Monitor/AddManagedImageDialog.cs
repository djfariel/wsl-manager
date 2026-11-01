using System.Drawing;
using System.Windows.Forms;
using WorldGate.Monitor.Core.ManagedImages;

namespace WorldGate.Monitor;

internal sealed class AddManagedImageDialog : Form
{
    private readonly TextBox nameBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox imageRefBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox buildPathBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox k8sNamespaceBox = new() { Dock = DockStyle.Fill };
    private readonly ComboBox categoryBox = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox backendBox = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox monitorBox = new() { Text = "Monitor status", AutoSize = true, Checked = true };
    private readonly ToolTip fieldToolTip = new();
    private readonly HelpProvider helpProvider = new();

    public AddManagedImageDialog(ManagedImageEntry? existing = null)
    {
        Text = existing is null ? "Add image" : "Edit image";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(560, 380);
        Font = new Font("Segoe UI", 9F);
        helpProvider.HelpNamespace = string.Empty;

        categoryBox.Items.AddRange(["GitHub Actions runner", "Other Docker / Kubernetes"]);
        categoryBox.SelectedIndex = 0;
        backendBox.Items.AddRange(["Docker (local image / containers)", "Kubernetes (pods using image)"]);
        backendBox.SelectedIndex = 0;
        backendBox.SelectedIndexChanged += (_, _) => UpdateK8sVisibility();

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Padding = new Padding(12),
            AutoSize = true,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(layout, "displayName", "Display name", nameBox);
        AddRow(layout, "imageReference", "Image reference", imageRefBox);
        AddRow(layout, "buildContextPath", "Build context path", buildPathBox);
        AddRow(layout, "category", "Category", categoryBox);
        AddRow(layout, "probeBackend", "Status probe", backendBox);
        AddRow(layout, "kubernetesNamespace", "Kubernetes namespace", k8sNamespaceBox);

        int monitorRow = layout.RowCount;
        layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(CreateHelpIndicator("monitorStatus", monitorBox), 0, monitorRow);
        layout.Controls.Add(new Label { Text = "Monitor status", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 8, 6) }, 1, monitorRow);
        monitorBox.Margin = new Padding(0, 6, 0, 6);
        layout.Controls.Add(monitorBox, 2, monitorRow);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Bottom,
            AutoSize = true,
            Padding = new Padding(12, 8, 12, 12),
        };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;

        Controls.Add(layout);
        Controls.Add(buttons);

        if (existing is not null)
        {
            nameBox.Text = existing.DisplayName;
            imageRefBox.Text = existing.ImageReference;
            buildPathBox.Text = existing.BuildContextPath ?? string.Empty;
            categoryBox.SelectedIndex = existing.Category == ManagedImageCategory.GitHubRunner ? 0 : 1;
            backendBox.SelectedIndex = existing.ProbeBackend == ManagedImageProbeBackend.Kubernetes ? 1 : 0;
            k8sNamespaceBox.Text = existing.KubernetesNamespace ?? string.Empty;
            monitorBox.Checked = existing.MonitorStatus;
        }
        else
        {
            k8sNamespaceBox.Text = "arc-runners";
        }

        UpdateK8sVisibility();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            fieldToolTip.Dispose();
            helpProvider.Dispose();
        }

        base.Dispose(disposing);
    }

    public ManagedImageEntry? BuildEntry(string? existingId = null)
    {
        string name = nameBox.Text.Trim();
        string imageRef = imageRefBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(imageRef))
        {
            MessageBox.Show(this, "Display name and image reference are required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        var category = categoryBox.SelectedIndex == 0
            ? ManagedImageCategory.GitHubRunner
            : ManagedImageCategory.OtherWorkload;
        var backend = backendBox.SelectedIndex == 1
            ? ManagedImageProbeBackend.Kubernetes
            : ManagedImageProbeBackend.Docker;

        string? buildPath = string.IsNullOrWhiteSpace(buildPathBox.Text) ? null : buildPathBox.Text.Trim();
        string? k8sNs = string.IsNullOrWhiteSpace(k8sNamespaceBox.Text) ? null : k8sNamespaceBox.Text.Trim();

        if (backend == ManagedImageProbeBackend.Kubernetes && string.IsNullOrWhiteSpace(k8sNs))
        {
            MessageBox.Show(this, "Kubernetes namespace is required for Kubernetes probes.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        return new ManagedImageEntry
        {
            Id = existingId ?? Guid.NewGuid().ToString("N"),
            DisplayName = name,
            ImageReference = imageRef,
            BuildContextPath = buildPath,
            Category = category,
            ProbeBackend = backend,
            KubernetesNamespace = k8sNs,
            MonitorStatus = monitorBox.Checked,
        };
    }

    private void UpdateK8sVisibility()
    {
        bool k8s = backendBox.SelectedIndex == 1;
        k8sNamespaceBox.Enabled = k8s;
    }

    private void AddRow(TableLayoutPanel layout, string fieldKey, string labelText, Control editor)
    {
        int row = layout.RowCount;
        layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(CreateHelpIndicator(fieldKey, editor), 0, row);
        layout.Controls.Add(new Label { Text = labelText, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 8, 6) }, 1, row);
        editor.Margin = new Padding(0, 3, 0, 3);
        layout.Controls.Add(editor, 2, row);
    }

    private Control CreateHelpIndicator(string fieldKey, Control editor)
    {
        string helpText = ManagedImageFieldHelp.Get(fieldKey);
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
}
