using System.Drawing;
using System.Windows.Forms;
using WorldGate.Monitor.Core.ManagedImages;

namespace WorldGate.Monitor;

internal sealed class ManagedImageStatusBadgeControl : Panel
{
    private readonly Label captionLabel = new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 8.25F, FontStyle.Bold),
        Margin = Padding.Empty,
    };

    private readonly Label valueLabel = new()
    {
        AutoSize = true,
        Margin = Padding.Empty,
    };

    public ManagedImageStatusBadgeControl()
    {
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(10, 6, 10, 6);
        Margin = new Padding(0, 0, 8, 8);
        BorderStyle = BorderStyle.FixedSingle;

        var stack = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        stack.Controls.Add(captionLabel);
        stack.Controls.Add(valueLabel);
        Controls.Add(stack);
    }

    public void ApplySnapshot(ManagedImageStatusSnapshot snapshot)
    {
        captionLabel.Text = snapshot.DisplayName;
        valueLabel.Text = string.IsNullOrWhiteSpace(snapshot.ErrorMessage)
            ? snapshot.Summary
            : snapshot.ErrorMessage;

        if (!string.IsNullOrWhiteSpace(snapshot.ErrorMessage))
        {
            ApplyColors(Color.FromArgb(254, 226, 226), Color.FromArgb(153, 27, 27));
            return;
        }

        if (snapshot.Summary.Equals("WSL off", StringComparison.OrdinalIgnoreCase))
        {
            ApplyColors(Color.FromArgb(243, 244, 246), Color.FromArgb(55, 65, 81));
            return;
        }

        switch (snapshot.RuntimeState)
        {
            case ManagedImageRuntimeState.Running:
                ApplyColors(Color.FromArgb(220, 252, 231), Color.FromArgb(22, 101, 52));
                break;
            case ManagedImageRuntimeState.Partial:
                ApplyColors(Color.FromArgb(254, 243, 199), Color.FromArgb(146, 64, 14));
                break;
            case ManagedImageRuntimeState.Stopped when snapshot.ImagePresence == ManagedImagePresence.Present:
                ApplyColors(Color.FromArgb(219, 234, 254), Color.FromArgb(30, 64, 175));
                break;
            case ManagedImageRuntimeState.Stopped:
                ApplyColors(Color.FromArgb(243, 244, 246), Color.FromArgb(55, 65, 81));
                break;
            default:
                ApplyColors(Color.FromArgb(243, 244, 246), Color.FromArgb(55, 65, 81));
                break;
        }
    }

    private void ApplyColors(Color back, Color fore)
    {
        BackColor = back;
        ForeColor = fore;
        captionLabel.ForeColor = fore;
        valueLabel.ForeColor = fore;
    }
}
