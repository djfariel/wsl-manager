using System.Text.Json;

namespace WorldGate.Monitor.Core.ManagedImages;

public static class KubernetesPodImageParser
{
    public static bool TrySummarizePodsUsingImage(
        string? podsJson,
        string imageReference,
        out int running,
        out int total,
        out string? error)
    {
        running = 0;
        total = 0;
        error = null;

        if (string.IsNullOrWhiteSpace(podsJson))
        {
            error = "Empty kubectl response.";
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(podsJson);
            if (!document.RootElement.TryGetProperty("items", out JsonElement items) || items.ValueKind != JsonValueKind.Array)
            {
                error = "kubectl JSON missing items array.";
                return false;
            }

            string needle = NormalizeImageRef(imageReference);
            foreach (JsonElement pod in items.EnumerateArray())
            {
                if (!PodUsesImage(pod, needle))
                {
                    continue;
                }

                total++;
                string phase = pod.TryGetProperty("status", out JsonElement status)
                    && status.TryGetProperty("phase", out JsonElement phaseElement)
                    ? phaseElement.GetString() ?? string.Empty
                    : string.Empty;

                if (phase.Equals("Running", StringComparison.OrdinalIgnoreCase))
                {
                    running++;
                }
            }

            return true;
        }
        catch (JsonException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    /// <summary>
    /// Returns pod names in the kubectl JSON whose containers use <paramref name="imageReference"/>.
    /// </summary>
    public static IReadOnlyList<string> ListPodNamesUsingImage(string? podsJson, string imageReference)
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

            string needle = NormalizeImageRef(imageReference);
            var names = new List<string>();
            foreach (JsonElement pod in items.EnumerateArray())
            {
                if (!pod.TryGetProperty("metadata", out JsonElement metadata)
                    || !metadata.TryGetProperty("name", out JsonElement nameElement))
                {
                    continue;
                }

                string? name = nameElement.GetString();
                if (string.IsNullOrWhiteSpace(name) || !PodUsesImage(pod, needle))
                {
                    continue;
                }

                names.Add(name);
            }

            return names;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static bool PodUsesImage(JsonElement pod, string normalizedNeedle)
    {
        if (!pod.TryGetProperty("spec", out JsonElement spec)
            || !spec.TryGetProperty("containers", out JsonElement containers)
            || containers.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (JsonElement container in containers.EnumerateArray())
        {
            if (!container.TryGetProperty("image", out JsonElement imageElement))
            {
                continue;
            }

            string image = imageElement.GetString() ?? string.Empty;
            if (ImageMatches(image, normalizedNeedle))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ImageMatches(string containerImage, string normalizedNeedle)
    {
        if (string.IsNullOrWhiteSpace(containerImage))
        {
            return false;
        }

        return string.Equals(NormalizeImageRef(containerImage), normalizedNeedle, StringComparison.OrdinalIgnoreCase)
            || containerImage.Contains(normalizedNeedle, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeImageRef(string reference)
    {
        string trimmed = reference.Trim();
        int at = trimmed.IndexOf('@');
        if (at > 0)
        {
            trimmed = trimmed[..at];
        }

        return trimmed;
    }
}
