using System.Windows.Forms;
using WorldGate.Monitor.Core.ManagedImages;

namespace WorldGate.Monitor;

internal static class ManagedImageListViewContext
{
    public static ListViewItem? GetItemAt(ListView list, Point clientLocation)
    {
        ListViewHitTestInfo hit = list.HitTest(clientLocation);
        return hit.Item;
    }

    public static int? GetSubItemIndex(ListViewItem item, ListViewItem.ListViewSubItem? subItem)
    {
        if (subItem is null)
        {
            return 0;
        }

        for (int index = 0; index < item.SubItems.Count; index++)
        {
            if (ReferenceEquals(item.SubItems[index], subItem))
            {
                return index;
            }
        }

        return null;
    }

    public static string? GetEntryIdAt(ListView list, Point clientLocation)
    {
        return ReadEntryId(GetItemAt(list, clientLocation));
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
        ListViewItem? item = GetItemAt(list, clientLocation);
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
                item.Tag = menu.Tag;
                item.Enabled = hasTarget;
            }
        };

        menu.Closed += (_, _) =>
        {
            if (menu.IsHandleCreated)
            {
                menu.BeginInvoke(() => menu.Tag = null);
            }
            else
            {
                menu.Tag = null;
            }
        };
    }

    public static string? GetContextEntryId(ContextMenuStrip menu, ToolStripItem? clickedItem = null)
    {
        if (clickedItem?.Tag is string itemId && !string.IsNullOrWhiteSpace(itemId))
        {
            return itemId;
        }

        return menu.Tag as string;
    }
}
