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

    public AddManagedImageDialog(ManagedImageEntry? existing = null)
    {
        Text = existing is null ? "Add image" : "Edit image";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 360);
        Font = new Font("Segoe UI", 9F);

        categoryBox.Items.AddRange(["GitHub Actions runner", "Other Docker / Kubernetes"]);
        categoryBox.SelectedIndex = 0;
        backendBox.Items.AddRange(["Docker (local image / containers)", "Kubernetes (pods using image)"]);
        backendBox.SelectedIndex = 0;
        backendBox.SelectedIndexChanged += (_, _) => UpdateK8sVisibility();

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
            AutoSize = true,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(layout, "Display name", nameBox);
        AddRow(layout, "Image reference", imageRefBox);
        AddRow(layout, "Build context path", buildPathBox);
        AddRow(layout, "Category", categoryBox);
        AddRow(layout, "Status probe", backendBox);
        AddRow(layout, "Kubernetes namespace", k8sNamespaceBox);

        var monitorRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        monitorRow.Controls.Add(monitorBox);
        layout.Controls.Add(new Label { Text = string.Empty, AutoSize = true }, 0, layout.RowCount - 1);
        layout.SetColumnSpan(monitorRow, 2);
        layout.Controls.Add(monitorRow, 0, layout.RowCount);
        layout.RowCount++;

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

        UpdateK8sVisibility();
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

    private static void AddRow(TableLayoutPanel layout, string labelText, Control editor)
    {
        int row = layout.RowCount;
        layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = labelText, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 8, 6) }, 0, row);
        editor.Margin = new Padding(0, 3, 0, 3);
        layout.Controls.Add(editor, 1, row);
    }
}
