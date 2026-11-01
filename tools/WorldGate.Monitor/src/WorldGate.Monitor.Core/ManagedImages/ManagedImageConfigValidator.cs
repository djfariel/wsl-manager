namespace WorldGate.Monitor.Core.ManagedImages;

public static class ManagedImageConfigValidator
{
    public static string? ValidateEntry(ManagedImageEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Id))
        {
            return "managed image id is required";
        }

        if (string.IsNullOrWhiteSpace(entry.DisplayName))
        {
            return "managed image displayName is required";
        }

        if (string.IsNullOrWhiteSpace(entry.ImageReference))
        {
            return "managed image imageReference is required";
        }

        if (entry.ProbeBackend == ManagedImageProbeBackend.Kubernetes
            && string.IsNullOrWhiteSpace(entry.KubernetesNamespace))
        {
            return "kubernetesNamespace is required for Kubernetes probe backend";
        }

        return null;
    }

    public static string? ValidateCollection(IReadOnlyList<ManagedImageEntry>? entries)
    {
        if (entries is null || entries.Count == 0)
        {
            return null;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (ManagedImageEntry entry in entries)
        {
            string? entryError = ValidateEntry(entry);
            if (entryError is not null)
            {
                return entryError;
            }

            if (!ids.Add(entry.Id))
            {
                return $"duplicate managed image id: {entry.Id}";
            }
        }

        return null;
    }
}
