using System.Drawing;

namespace WorldGate.Monitor;

internal static class MonitorBadgeLayout
{
    public const int MonitorColumnWidth = 260;

    public static int BadgeContentWidth(int columnInnerWidth) =>
        Math.Max(96, columnInnerWidth - 24);

    public static void ConstrainBadgeWidth(Control badge, int contentWidth)
    {
        badge.Width = contentWidth + badge.Padding.Horizontal;

        if (badge is ManagedImageStatusBadgeControl managedBadge)
        {
            managedBadge.SetContentWidth(contentWidth);
        }
        else if (badge is StatusBadgeControl runtimeBadge)
        {
            runtimeBadge.SetContentWidth(contentWidth);
        }
    }
}
