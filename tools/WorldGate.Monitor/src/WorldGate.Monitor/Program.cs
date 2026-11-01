using System.Windows.Forms;
using WorldGate.Monitor.Core;

namespace WorldGate.Monitor;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var configurationStore = new ConfigurationStore();
        ConfigurationLoadResult loaded = configurationStore.LoadOrCreate();
        var seedService = new ManagedImageSeedService(configurationStore, TimeSpan.FromSeconds(45));
        seedService.TrySeedIfEmptyAsync(loaded.Configuration, loaded.ConfigurationPath).GetAwaiter().GetResult();
        loaded = configurationStore.LoadOrCreate();
        int pollIntervalSeconds = loaded.Error is null ? loaded.Configuration.PollIntervalSeconds : 10;
        int queueWarningSeconds = loaded.Error is null ? loaded.Configuration.QueueWarningSeconds : 90;

        var commandRunner = new WslCommandRunner(loaded.Configuration.WslDistribution, TimeSpan.FromSeconds(20));
        var poller = new StatusPoller(loaded.Configuration, commandRunner, loaded.Error);
        var notificationPlanner = new NotificationPlanner(TimeSpan.FromSeconds(queueWarningSeconds));
        var context = new MonitorApplicationContext(
            poller,
            notificationPlanner,
            loaded.ConfigurationPath,
            pollIntervalSeconds);

        Application.Run(context);
    }
}
