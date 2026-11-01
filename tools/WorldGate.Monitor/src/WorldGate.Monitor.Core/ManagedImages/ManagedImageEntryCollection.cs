namespace WorldGate.Monitor.Core.ManagedImages;

public static class ManagedImageEntryCollection
{
    public static ManagedImageEntry? FindById(IReadOnlyList<ManagedImageEntry> entries, string entryId)
    {
        if (string.IsNullOrWhiteSpace(entryId))
        {
            return null;
        }

        return entries.FirstOrDefault(entry => string.Equals(entry.Id, entryId, StringComparison.Ordinal));
    }

    public static IReadOnlyList<ManagedImageEntry> GetMonitoredEntries(IReadOnlyList<ManagedImageEntry> entries)
    {
        if (entries is null || entries.Count == 0)
        {
            return [];
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var monitored = new List<ManagedImageEntry>();
        foreach (ManagedImageEntry entry in entries)
        {
            if (!entry.MonitorStatus || string.IsNullOrWhiteSpace(entry.Id))
            {
                continue;
            }

            if (!seenIds.Add(entry.Id))
            {
                continue;
            }

            monitored.Add(entry);
        }

        return monitored;
    }

    public static IReadOnlyList<ManagedImageEntry> ReplaceById(
        IReadOnlyList<ManagedImageEntry> entries,
        ManagedImageEntry updated)
    {
        bool replaced = false;
        var next = new ManagedImageEntry[entries.Count];
        for (int index = 0; index < entries.Count; index++)
        {
            ManagedImageEntry entry = entries[index];
            if (string.Equals(entry.Id, updated.Id, StringComparison.Ordinal))
            {
                next[index] = updated;
                replaced = true;
            }
            else
            {
                next[index] = entry;
            }
        }

        if (!replaced)
        {
            throw new InvalidOperationException($"No managed image entry with id '{updated.Id}'.");
        }

        return next;
    }
}
