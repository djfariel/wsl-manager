using System.Drawing;
using System.Windows.Forms;
using WorldGate.Monitor.Core.ManagedImages;

namespace WorldGate.Monitor;

internal sealed class RemoveManagedImageDialog : Form
{
    private readonly CheckBox listOnlyCheckBox = new()
    {
        AutoSize = true,
        Text = "Keep image on disk (remove from list only)",
    };

    private readonly Label planLabel = new()
    {
        AutoSize = false,
        MaximumSize = new Size(520, 0),
        Dock = DockStyle.Fill,
    };

    public RemoveManagedImageDialog(ManagedImageEntry entry)
    {
        Text = "Remove image";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(540, 320);
        Font = new Font("Segoe UI", 9F);

        listOnlyCheckBox.CheckedChanged += (_, _) => RefreshPlanText(entry);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0),
        };
        var removeButton = new Button { Text = "Remove", DialogResult = DialogResult.OK, AutoSize = true };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.Add(removeButton);
        buttons.Controls.Add(cancelButton);
        AcceptButton = removeButton;
        CancelButton = cancelButton;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(planLabel, 0, 0);
        layout.Controls.Add(listOnlyCheckBox, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        Controls.Add(layout);

        RefreshPlanText(entry);
    }

    public bool RemoveFromListOnly => listOnlyCheckBox.Checked;

    private void RefreshPlanText(ManagedImageEntry entry)
    {
        planLabel.Text = ManagedImageRemoveHelper.DescribeRemovePlan(entry, listOnlyCheckBox.Checked);
    }
}
