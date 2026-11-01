namespace WorldGate.Monitor.Core.WslRuntime;

public readonly record struct WslRuntimeBadgeItem(string Key, string Caption, WslRuntimeSnapshot Snapshot);

public static class WslRuntimeBadgeItems
{
    public const string SingleBadgeKey = "wsl-runtime";

    public static IReadOnlyList<WslRuntimeBadgeItem> FromSnapshot(WslRuntimeSnapshot snapshot)
    {
        if (snapshot.State == WslRuntimeState.Running
            && snapshot.RunningDistributions is { Count: > 1 } distros)
        {
            var items = new List<WslRuntimeBadgeItem>(distros.Count);
            foreach (string distro in distros)
            {
                string key = DistroBadgeKey(distro);
                items.Add(new WslRuntimeBadgeItem(
                    key,
                    $"WSL runtime — {distro}",
                    WslRuntimeSnapshot.Running([distro])));
            }

            return items;
        }

        return [new WslRuntimeBadgeItem(SingleBadgeKey, "WSL runtime", snapshot)];
    }

    public static string DistroBadgeKey(string distributionName) =>
        "wsl-distro:" + distributionName;
}
