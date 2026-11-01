using System.Drawing;
using System.Windows.Forms;
using WorldGate.Monitor.Core.WslRuntime;

namespace WorldGate.Monitor;

internal sealed class StatusBadgeControl : Panel
{
    private readonly ToolTip runtimeToolTip = new();

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
        AutoSize = false;
        Padding = new Padding(10, 6, 10, 6);
        Margin = new Padding(0, 0, 0, 8);
        BorderStyle = BorderStyle.FixedSingle;

        var stack = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Dock = DockStyle.Top,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        stack.Controls.Add(captionLabel, 0, 0);
        stack.Controls.Add(valueLabel, 0, 1);
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
        captionLabel.Width = inner;
        valueLabel.Width = inner;
        SyncHeightFromContent();
    }

    private void SyncHeightFromContent()
    {
        if (Controls.Count == 0 || Controls[0] is not Control stack)
        {
            return;
        }

        stack.PerformLayout();
        int contentHeight = stack.PreferredSize.Height + Padding.Vertical;
        Height = Math.Max(contentHeight, 28);
        MinimumSize = new Size(Width, Height);
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

        string toolTip = BuildToolTip(snapshot);
        runtimeToolTip.SetToolTip(this, toolTip);
        runtimeToolTip.SetToolTip(captionLabel, toolTip);
        runtimeToolTip.SetToolTip(valueLabel, toolTip);
        SyncHeightFromContent();
    }

    private static string BuildToolTip(WslRuntimeSnapshot snapshot)
    {
        return snapshot.State switch
        {
            WslRuntimeState.Running => BuildRunningToolTip(snapshot),
            WslRuntimeState.Stopped =>
                "WSL utility VM is not running (no vmmem / vmmemWSL process). Stopped distributions do not count toward this badge.",
            _ => string.IsNullOrWhiteSpace(snapshot.ErrorMessage)
                ? "Could not determine WSL runtime status."
                : snapshot.ErrorMessage,
        };
    }

    private static string BuildRunningToolTip(WslRuntimeSnapshot snapshot)
    {
        if (snapshot.RunningDistributions is { Count: > 0 } distros)
        {
            string names = string.Join(Environment.NewLine, distros.Select(d => "• " + d));
            return distros.Count == 1
                ? $"WSL utility VM is running. One distribution is running (from wsl --list --running):{Environment.NewLine}{names}"
                : $"WSL utility VM is running. {distros.Count} distributions are running (from wsl --list --running):{Environment.NewLine}{names}";
        }

        return "WSL utility VM is running (vmmem process present). wsl --list --running reported no distributions — the VM may be idle or the list command failed.";
    }

    private static string FormatRunningDetail(WslRuntimeSnapshot snapshot)
    {
        if (snapshot.RunningDistributions is { Count: > 0 } distros)
        {
            return distros.Count == 1
                ? $"On — {distros[0]} running"
                : $"On — {distros.Count} distributions running";
        }

        return "On — VM up, no distros running";
    }
}
