namespace WorldGate.Monitor.Core.UiSync;

/// <summary>
/// Compares desired monitor badge key order with the keys currently shown in a panel.
/// </summary>
public static class OrderedKeyListSync
{
    public readonly record struct Plan(
        IReadOnlyList<string> DesiredOrder,
        IReadOnlyList<string> RemovedKeys,
        IReadOnlyList<string> AddedKeys,
        bool KeysAndOrderUnchanged);

    public static Plan Compare(IReadOnlyList<string> desiredOrder, IReadOnlyList<string> currentOrder)
    {
        var desiredSet = new HashSet<string>(desiredOrder, StringComparer.Ordinal);
        var currentSet = new HashSet<string>(currentOrder, StringComparer.Ordinal);

        var removed = currentOrder.Where(key => !desiredSet.Contains(key)).ToList();
        var added = desiredOrder.Where(key => !currentSet.Contains(key)).ToList();
        bool unchanged = removed.Count == 0 && added.Count == 0 && desiredOrder.SequenceEqual(currentOrder);

        return new Plan(desiredOrder, removed, added, unchanged);
    }
}
