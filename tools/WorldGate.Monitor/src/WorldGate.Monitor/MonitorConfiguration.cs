namespace WorldGate.Monitor;

internal sealed record MonitorConfiguration
{
    public string Repository { get; init; } = "djfariel/WorldGate";

    public string RunnerScaleSetName { get; init; } = "worldgate-linux";

    public string ControllerNamespace { get; init; } = "arc-systems";

    public string RunnerNamespace { get; init; } = "arc-runners";

    public string? WslDistribution { get; init; }

    public int PollIntervalSeconds { get; init; } = 10;

    public int QueueWarningSeconds { get; init; } = 90;

    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Repository) || Repository.Count(character => character == '/') != 1)
        {
            return "repository must be in owner/name form";
        }

        if (string.IsNullOrWhiteSpace(RunnerScaleSetName))
        {
            return "runnerScaleSetName is required";
        }

        if (string.IsNullOrWhiteSpace(ControllerNamespace) || string.IsNullOrWhiteSpace(RunnerNamespace))
        {
            return "controllerNamespace and runnerNamespace are required";
        }

        if (PollIntervalSeconds is < 2 or > 300)
        {
            return "pollIntervalSeconds must be between 2 and 300";
        }

        if (QueueWarningSeconds is < 10 or > 3600)
        {
            return "queueWarningSeconds must be between 10 and 3600";
        }

        return null;
    }
}
