using System.Drawing;
using System.Windows.Forms;
using WorldGate.Monitor.Core.WslRuntime;

namespace WorldGate.Monitor;

internal sealed class StatusBadgeControl : Panel
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

    public StatusBadgeControl()
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

    public void SetCaption(string caption) => captionLabel.Text = caption;

    public void SetContentWidth(int contentWidth)
    {
        int inner = Math.Max(64, contentWidth);
        captionLabel.MaximumSize = new Size(inner, 0);
        valueLabel.MaximumSize = new Size(inner, 0);
        captionLabel.AutoEllipsis = true;
        valueLabel.AutoEllipsis = true;
    }

    public void ApplySnapshot(WslRuntimeSnapshot snapshot)
    {
        switch (snapshot.State)
        {
            case WslRuntimeState.Running:
                BackColor = Color.FromArgb(220, 252, 231);
                ForeColor = Color.FromArgb(22, 101, 52);
                valueLabel.Text = FormatRunningDetail(snapshot);
                break;
            case WslRuntimeState.Stopped:
                BackColor = Color.FromArgb(243, 244, 246);
                ForeColor = Color.FromArgb(55, 65, 81);
                valueLabel.Text = "Off — VM not running";
                break;
            default:
                BackColor = Color.FromArgb(254, 226, 226);
                ForeColor = Color.FromArgb(153, 27, 27);
                valueLabel.Text = string.IsNullOrWhiteSpace(snapshot.ErrorMessage)
                    ? "Status unknown"
                    : snapshot.ErrorMessage;
                break;
        }

        captionLabel.ForeColor = ForeColor;
        valueLabel.ForeColor = ForeColor;
    }

    private static string FormatRunningDetail(WslRuntimeSnapshot snapshot)
    {
        if (snapshot.RunningDistributions is { Count: > 0 } distros)
        {
            return distros.Count == 1
                ? $"On — {distros[0]}"
                : $"On — {distros.Count} distros";
        }

        return "On — utility VM active";
    }
}
