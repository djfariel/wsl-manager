using System.Windows.Forms;
using WorldGate.Monitor.Core.ManagedImages;

namespace WorldGate.Monitor;

internal static class ManagedImageListViewContext
{
    public static string? GetEntryIdAt(ListView list, Point clientLocation)
    {
        ListViewItem? item = list.GetItemAt(clientLocation.X, clientLocation.Y);
        return ReadEntryId(item);
    }

    public static string? ReadEntryId(ListViewItem? item)
    {
        if (item?.Tag is not ManagedImageEntry entry || string.IsNullOrWhiteSpace(entry.Id))
        {
            return null;
        }

        return entry.Id;
    }

    public static void FocusItemAt(ListView list, Point clientLocation)
    {
        ListViewItem? item = list.GetItemAt(clientLocation.X, clientLocation.Y);
        if (item is null)
        {
            return;
        }

        item.Selected = true;
        item.Focused = true;
    }

    public static void BindContextMenu(ListView list, ContextMenuStrip menu)
    {
        list.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Right)
            {
                return;
            }

            FocusItemAt(list, e.Location);
            menu.Tag = GetEntryIdAt(list, e.Location);
        };

        menu.Opening += (_, _) =>
        {
            Point client = list.PointToClient(Cursor.Position);
            string? entryId = GetEntryIdAt(list, client);
            if (!string.IsNullOrWhiteSpace(entryId))
            {
                menu.Tag = entryId;
            }

            bool hasTarget = menu.Tag is string id && !string.IsNullOrWhiteSpace(id);
            foreach (ToolStripItem item in menu.Items)
            {
                item.Enabled = hasTarget;
            }
        };

        menu.Closed += (_, _) => menu.Tag = null;
    }

    public static string? GetContextEntryId(ContextMenuStrip menu) =>
        menu.Tag as string;
}
