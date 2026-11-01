using System.Text.Json;

namespace WorldGate.Monitor.Core.ManagedImages;

public sealed record DiscoveredPodImage(string ImageReference, string Namespace);

public static class KubectlPodImageDiscovery
{
    public static IReadOnlyList<DiscoveredPodImage> ParsePodsJson(string? podsJson)
    {
        if (string.IsNullOrWhiteSpace(podsJson))
        {
            return [];
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(podsJson);
            if (!document.RootElement.TryGetProperty("items", out JsonElement items) || items.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var discovered = new List<DiscoveredPodImage>();
            foreach (JsonElement pod in items.EnumerateArray())
            {
                string ns = pod.TryGetProperty("metadata", out JsonElement metadata)
                    && metadata.TryGetProperty("namespace", out JsonElement nsElement)
                    ? nsElement.GetString() ?? string.Empty
                    : string.Empty;

                if (string.IsNullOrWhiteSpace(ns)
                    || !pod.TryGetProperty("spec", out JsonElement spec)
                    || !spec.TryGetProperty("containers", out JsonElement containers)
                    || containers.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (JsonElement container in containers.EnumerateArray())
                {
                    if (!container.TryGetProperty("image", out JsonElement imageElement))
                    {
                        continue;
                    }

                    string image = imageElement.GetString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(image))
                    {
                        continue;
                    }

                    discovered.Add(new DiscoveredPodImage(image.Trim(), ns));
                }
            }

            return discovered;
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
