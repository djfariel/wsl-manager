using System.Text.Json;

namespace WorldGate.Monitor.Core;

public static class ArcObservationParser
{
    public static bool TryParse(
        string controllerJson,
        string runnerJson,
        string runnerScaleSetName,
        out ArcObservation observation,
        out string? error)
    {
        try
        {
            IReadOnlyList<PodInfo> controllerPods = ParsePodList(controllerJson);
            IReadOnlyList<PodInfo> runnerPods = ParsePodList(runnerJson);

            bool controllerReady = controllerPods.Any(pod => !IsListener(pod) && pod.Ready);
            PodInfo? listener = controllerPods
                .Concat(runnerPods)
                .Where(pod => MatchesScaleSet(pod, runnerScaleSetName) && IsListener(pod))
                .OrderByDescending(pod => pod.Ready)
                .FirstOrDefault();

            IReadOnlyList<RunnerPodObservation> activeRunners = runnerPods
                .Where(pod =>
                    MatchesScaleSet(pod, runnerScaleSetName) &&
                    !IsListener(pod) &&
                    (pod.Phase.Equals("Running", StringComparison.OrdinalIgnoreCase) ||
                     pod.Phase.Equals("Pending", StringComparison.OrdinalIgnoreCase)))
                .Select(pod => new RunnerPodObservation(pod.Name, pod.Phase, pod.Ready))
                .ToArray();

            observation = new ArcObservation(
                true,
                null,
                controllerReady,
                listener?.Ready == true,
                activeRunners);
            error = null;
            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            observation = ArcObservation.Unavailable($"Unable to parse ARC pod state: {exception.Message}");
            error = observation.Error;
            return false;
        }
    }

    private static IReadOnlyList<PodInfo> ParsePodList(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("items", out JsonElement items) || items.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Kubernetes response does not contain items array.");
        }

        var pods = new List<PodInfo>();
        foreach (JsonElement item in items.EnumerateArray())
        {
            JsonElement metadata = item.GetProperty("metadata");
            string name = metadata.GetProperty("name").GetString() ?? string.Empty;
            var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (metadata.TryGetProperty("labels", out JsonElement labelsElement) && labelsElement.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty label in labelsElement.EnumerateObject())
                {
                    labels[label.Name] = label.Value.ValueKind == JsonValueKind.String
                        ? label.Value.GetString() ?? string.Empty
                        : label.Value.ToString();
                }
            }

            var containerNames = new List<string>();
            if (item.TryGetProperty("spec", out JsonElement spec) &&
                spec.TryGetProperty("containers", out JsonElement containers) &&
                containers.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement container in containers.EnumerateArray())
                {
                    if (container.TryGetProperty("name", out JsonElement containerName) &&
                        containerName.ValueKind == JsonValueKind.String &&
                        containerName.GetString() is { } value)
                    {
                        containerNames.Add(value);
                    }
                }
            }

            string phase = "Unknown";
            bool ready = false;
            if (item.TryGetProperty("status", out JsonElement status))
            {
                if (status.TryGetProperty("phase", out JsonElement phaseElement) &&
                    phaseElement.ValueKind == JsonValueKind.String &&
                    phaseElement.GetString() is { } phaseValue)
                {
                    phase = phaseValue;
                }

                if (status.TryGetProperty("conditions", out JsonElement conditions) && conditions.ValueKind == JsonValueKind.Array)
                {
                    ready = conditions.EnumerateArray().Any(condition =>
                        condition.TryGetProperty("type", out JsonElement type) &&
                        type.ValueKind == JsonValueKind.String &&
                        type.GetString()?.Equals("Ready", StringComparison.OrdinalIgnoreCase) == true &&
                        condition.TryGetProperty("status", out JsonElement conditionStatus) &&
                        conditionStatus.ValueKind == JsonValueKind.String &&
                        conditionStatus.GetString()?.Equals("True", StringComparison.OrdinalIgnoreCase) == true);
                }
            }

            pods.Add(new PodInfo(name, labels, containerNames, phase, ready));
        }

        return pods;
    }

    private static bool MatchesScaleSet(PodInfo pod, string runnerScaleSetName)
    {
        KeyValuePair<string, string>[] scaleSetLabels = pod.Labels
            .Where(label => label.Key.Contains("scale-set-name", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (scaleSetLabels.Length > 0)
        {
            return scaleSetLabels.Any(label =>
                label.Value.Equals(runnerScaleSetName, StringComparison.OrdinalIgnoreCase));
        }

        return pod.Name.Contains(runnerScaleSetName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsListener(PodInfo pod) =>
        pod.Name.Contains("listener", StringComparison.OrdinalIgnoreCase) ||
        pod.ContainerNames.Any(name => name.Contains("listener", StringComparison.OrdinalIgnoreCase));

    private sealed record PodInfo(
        string Name,
        IReadOnlyDictionary<string, string> Labels,
        IReadOnlyList<string> ContainerNames,
        string Phase,
        bool Ready);
}
