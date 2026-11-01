using System.Drawing;
using System.Windows.Forms;
using WorldGate.Monitor.Core.ManagedImages;

namespace WorldGate.Monitor;

internal sealed class HomePageControl : UserControl
{
    private const int HomeBadgeMaxContentWidth = 640;

    private readonly Panel monitoredScrollHost;
    private readonly FlowLayoutPanel monitoredPanel = new()
    {
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        Dock = DockStyle.Top,
        Padding = new Padding(4),
    };

    private readonly Label emptyLabel = new()
    {
        AutoSize = true,
        ForeColor = SystemColors.GrayText,
        Text = "No monitored images. Enable “Monitor status” on the Runners & images page.",
        MaximumSize = new Size(640, 0),
        Padding = new Padding(8, 4, 8, 8),
    };

    public HomePageControl()
    {
        Dock = DockStyle.Fill;
        BackColor = SystemColors.Control;
        Font = new Font("Segoe UI", 9F);

        var title = new Label
        {
            Text = "Monitored images",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
            Padding = new Padding(12, 12, 8, 4),
        };

        monitoredScrollHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var stack = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Dock = DockStyle.Top,
        };
        stack.Controls.Add(title);
        stack.Controls.Add(emptyLabel);
        stack.Controls.Add(monitoredPanel);
        monitoredScrollHost.Controls.Add(stack);
        monitoredScrollHost.Resize += (_, _) => SyncMonitoredBadgeWidths();
        Controls.Add(monitoredScrollHost);
    }

    public void ApplyMonitoredSnapshots(IReadOnlyList<ManagedImageStatusSnapshot> snapshots)
    {
        monitoredPanel.Controls.Clear();
        if (snapshots.Count == 0)
        {
            emptyLabel.Visible = true;
            return;
        }

        emptyLabel.Visible = false;
        int contentWidth = GetMonitoredBadgeContentWidth();
        foreach (ManagedImageStatusSnapshot snapshot in snapshots)
        {
            var badge = new ManagedImageStatusBadgeControl();
            badge.ApplySnapshot(snapshot);
            MonitorBadgeLayout.ConstrainBadgeWidth(badge, contentWidth);
            monitoredPanel.Controls.Add(badge);
        }

        SyncMonitoredBadgeWidths();
    }

    private int GetMonitoredBadgeContentWidth()
    {
        int inner = monitoredScrollHost.ClientSize.Width;
        if (inner <= 0)
        {
            inner = HomeBadgeMaxContentWidth;
        }

        return Math.Min(HomeBadgeMaxContentWidth, MonitorBadgeLayout.BadgeContentWidth(inner));
    }

    private void SyncMonitoredBadgeWidths()
    {
        if (monitoredScrollHost.ClientSize.Width <= 0)
        {
            return;
        }

        int stackWidth = monitoredScrollHost.ClientSize.Width;
        monitoredPanel.Width = stackWidth;
        monitoredPanel.MaximumSize = new Size(stackWidth, 0);

        int contentWidth = GetMonitoredBadgeContentWidth();
        foreach (Control control in monitoredPanel.Controls)
        {
            MonitorBadgeLayout.ConstrainBadgeWidth(control, contentWidth);
        }
    }
}
