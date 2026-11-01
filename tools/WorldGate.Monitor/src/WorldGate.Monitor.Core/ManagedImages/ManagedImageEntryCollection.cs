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
