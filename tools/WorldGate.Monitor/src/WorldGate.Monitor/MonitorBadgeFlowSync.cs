using System.Windows.Forms;
using WorldGate.Monitor.Core.ManagedImages;
using WorldGate.Monitor.Core.UiSync;
using WorldGate.Monitor.Core.WslRuntime;

namespace WorldGate.Monitor;

internal static class MonitorBadgeFlowSync
{
    public static void SyncWslRuntimeBadges(
        FlowLayoutPanel row,
        Dictionary<string, StatusBadgeControl> badgesByKey,
        WslRuntimeSnapshot snapshot,
        int contentWidth)
    {
        IReadOnlyList<WslRuntimeBadgeItem> items = WslRuntimeBadgeItems.FromSnapshot(snapshot);
        IReadOnlyList<string> desiredOrder = items.Select(item => item.Key).ToList();
        IReadOnlyList<string> currentOrder = ReadControlOrder(row);

        OrderedKeyListSync.Plan plan = OrderedKeyListSync.Compare(desiredOrder, currentOrder);
        row.SuspendLayout();
        try
        {
            if (!plan.KeysAndOrderUnchanged)
            {
                ApplyStructuralChanges(row, badgesByKey, plan);
            }

            foreach (WslRuntimeBadgeItem item in items)
            {
                StatusBadgeControl badge = badgesByKey[item.Key];
                badge.SetCaption(item.Caption);
                badge.ApplySnapshot(item.Snapshot);
                MonitorBadgeLayout.ConstrainBadgeWidth(badge, contentWidth);
            }
        }
        finally
        {
            row.ResumeLayout(performLayout: true);
        }
    }

    public static void SyncManagedImageBadges(
        FlowLayoutPanel row,
        Dictionary<string, ManagedImageStatusBadgeControl> badgesByKey,
        IReadOnlyList<ManagedImageStatusSnapshot> snapshots,
        int contentWidth)
    {
        IReadOnlyList<string> desiredOrder = snapshots.Select(snapshot => snapshot.EntryId).ToList();
        IReadOnlyList<string> currentOrder = ReadControlOrder(row);

        OrderedKeyListSync.Plan plan = OrderedKeyListSync.Compare(desiredOrder, currentOrder);
        row.SuspendLayout();
        try
        {
            if (!plan.KeysAndOrderUnchanged)
            {
                ApplyStructuralChanges(row, badgesByKey, plan);
            }

            foreach (ManagedImageStatusSnapshot snapshot in snapshots)
            {
                ManagedImageStatusBadgeControl badge = badgesByKey[snapshot.EntryId];
                badge.ApplySnapshot(snapshot);
                MonitorBadgeLayout.ConstrainBadgeWidth(badge, contentWidth);
            }
        }
        finally
        {
            row.ResumeLayout(performLayout: true);
        }
    }

    private static List<string> ReadControlOrder(FlowLayoutPanel row)
    {
        var order = new List<string>(row.Controls.Count);
        foreach (Control control in row.Controls)
        {
            if (control.Tag is string key && !string.IsNullOrEmpty(key))
            {
                order.Add(key);
            }
        }

        return order;
    }

    private static void ApplyStructuralChanges<TBadge>(
        FlowLayoutPanel row,
        Dictionary<string, TBadge> badgesByKey,
        OrderedKeyListSync.Plan plan)
        where TBadge : Control, new()
    {
        foreach (string removedKey in plan.RemovedKeys)
        {
            if (!badgesByKey.TryGetValue(removedKey, out TBadge? badge))
            {
                continue;
            }

            row.Controls.Remove(badge);
            badge.Dispose();
            badgesByKey.Remove(removedKey);
        }

        for (int index = 0; index < plan.DesiredOrder.Count; index++)
        {
            string key = plan.DesiredOrder[index];
            if (!badgesByKey.TryGetValue(key, out TBadge? badge))
            {
                badge = new TBadge { Tag = key };
                badgesByKey[key] = badge;
                row.Controls.Add(badge);
            }

            if (row.Controls.GetChildIndex(badge) != index)
            {
                row.Controls.SetChildIndex(badge, index);
            }
        }
    }
}
