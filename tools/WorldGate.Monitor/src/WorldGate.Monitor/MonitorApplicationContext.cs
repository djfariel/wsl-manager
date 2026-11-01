using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using WorldGate.Monitor.Core;

namespace WorldGate.Monitor;

internal sealed class MonitorApplicationContext : ApplicationContext
{
    private readonly StatusPoller poller;
    private readonly NotificationPlanner notificationPlanner;
    private readonly string configurationPath;
    private readonly CancellationTokenSource shutdown = new();
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private readonly System.Windows.Forms.Timer pollTimer;
    private readonly NotifyIcon trayIcon;
    private readonly ContextMenuStrip menu;
    private readonly ToolStripMenuItem stateItem;
    private readonly ToolStripMenuItem jobItem;
    private readonly ToolStripMenuItem arcItem;
    private readonly ToolStripMenuItem runnerItem;
    private readonly ToolStripMenuItem openRunItem;
    private Icon currentIcon;
    private MonitorSnapshot? currentSnapshot;
    private bool disposed;

    public MonitorApplicationContext(
        StatusPoller poller,
        NotificationPlanner notificationPlanner,
        string configurationPath,
        int pollIntervalSeconds)
    {
        this.poller = poller;
        this.notificationPlanner = notificationPlanner;
        this.configurationPath = configurationPath;

        stateItem = new ToolStripMenuItem("WorldGate: starting") { Enabled = false };
        jobItem = new ToolStripMenuItem("GitHub: checking") { Enabled = false };
        arcItem = new ToolStripMenuItem("ARC: checking") { Enabled = false };
        runnerItem = new ToolStripMenuItem("Runners: checking") { Enabled = false };
        openRunItem = new ToolStripMenuItem("Open GitHub Run", null, (_, _) => OpenCurrentRun()) { Enabled = false };
        var refreshItem = new ToolStripMenuItem("Refresh Now", null, async (_, _) => await RefreshAsync());
        var testNotificationItem = new ToolStripMenuItem("Test Notification", null, (_, _) => TestNotification());
        var configurationItem = new ToolStripMenuItem("Open Configuration", null, (_, _) => OpenConfiguration());
        var exitItem = new ToolStripMenuItem("Exit", null, (_, _) => ExitThread());

        menu = new ContextMenuStrip();
        menu.Items.AddRange(new ToolStripItem[]
        {
            stateItem,
            jobItem,
            arcItem,
            runnerItem,
            new ToolStripSeparator(),
            openRunItem,
            refreshItem,
            testNotificationItem,
            configurationItem,
            new ToolStripSeparator(),
            exitItem,
        });

        currentIcon = TrayIconFactory.Create(MonitorDisplayState.Unavailable);
        trayIcon = new NotifyIcon
        {
            Icon = currentIcon,
            Text = "WorldGate: STARTING",
            ContextMenuStrip = menu,
            Visible = true,
        };

        pollTimer = new System.Windows.Forms.Timer
        {
            Interval = checked(Math.Clamp(pollIntervalSeconds, 2, 300) * 1000),
        };
        pollTimer.Tick += async (_, _) => await RefreshAsync();
        pollTimer.Start();

        Application.Idle += RefreshOnFirstIdle;
    }

    protected override void ExitThreadCore()
    {
        DisposeResources();
        base.ExitThreadCore();
    }

    private async void RefreshOnFirstIdle(object? sender, EventArgs e)
    {
        Application.Idle -= RefreshOnFirstIdle;
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (disposed || !await refreshGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            MonitorSnapshot snapshot = await poller.PollAsync(shutdown.Token);
            DateTimeOffset now = DateTimeOffset.Now;
            currentSnapshot = snapshot;
            UpdateTray(snapshot, now);

            foreach (MonitorNotification notification in notificationPlanner.Evaluate(snapshot, now))
            {
                ShowNotification(notification);
            }
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            MonitorSnapshot snapshot = MonitorStateResolver.Resolve(
                GitHubObservation.Unavailable($"Monitor error: {exception.Message}"),
                ArcObservation.Unavailable($"Monitor error: {exception.Message}"));
            currentSnapshot = snapshot;
            UpdateTray(snapshot, DateTimeOffset.Now);
        }
        finally
        {
            refreshGate.Release();
        }
    }

    private void UpdateTray(MonitorSnapshot snapshot, DateTimeOffset now)
    {
        Icon replacement = TrayIconFactory.Create(snapshot.DisplayState);
        trayIcon.Icon = replacement;
        Icon previous = currentIcon;
        currentIcon = replacement;
        previous.Dispose();

        trayIcon.Text = TooltipFormatter.Format(snapshot, now);
        stateItem.Text = $"WorldGate: {snapshot.DisplayState}";

        GitHubJobObservation? job = snapshot.GitHub.ActiveJob ?? snapshot.GitHub.LatestCompletedJob;
        jobItem.Text = job is null
            ? snapshot.GitHub.Available ? "GitHub: no monitored job" : $"GitHub: {ShortError(snapshot.GitHub.Error)}"
            : job.PullRequestNumber is { } number
                ? $"GitHub: PR #{number} — {job.RunName} / {job.JobName}"
                : $"GitHub: {job.RunName} / {job.JobName}";

        arcItem.Text = snapshot.Arc.Available
            ? $"ARC: controller {(snapshot.Arc.ControllerReady ? "ready" : "not ready")}; listener {(snapshot.Arc.ListenerReady ? "ready" : "not ready")}" 
            : $"ARC: {ShortError(snapshot.Arc.Error)}";

        runnerItem.Text = snapshot.Arc.ActiveRunnerCount switch
        {
            0 => "Runners: idle",
            1 => $"Runner: {snapshot.Arc.RunnerPodName} ({snapshot.Arc.RunnerPods[0].Phase})",
            int count => $"Runners: {count} active",
        };

        openRunItem.Enabled = !string.IsNullOrWhiteSpace(job?.RunUrl);
    }

    private void ShowNotification(MonitorNotification notification)
    {
        NotificationPresentation presentation = NotificationPresentation.From(notification);
        ToolTipIcon icon = presentation.Severity switch
        {
            MonitorNotificationSeverity.Error => ToolTipIcon.Error,
            MonitorNotificationSeverity.Warning => ToolTipIcon.Warning,
            _ => ToolTipIcon.Info,
        };

        trayIcon.ShowBalloonTip(
            presentation.DurationMilliseconds,
            presentation.Title,
            presentation.Message,
            icon);
    }

    private void TestNotification()
    {
        ShowNotification(new MonitorNotification(
            MonitorNotificationKind.Started,
            "WorldGate notification test",
            "Native Windows notifications are working.",
            null));
    }

    private void OpenCurrentRun()
    {
        GitHubJobObservation? job = currentSnapshot?.GitHub.ActiveJob ?? currentSnapshot?.GitHub.LatestCompletedJob;
        if (string.IsNullOrWhiteSpace(job?.RunUrl))
        {
            return;
        }

        TryStart(new ProcessStartInfo(job.RunUrl) { UseShellExecute = true });
    }

    private void OpenConfiguration()
    {
        var startInfo = new ProcessStartInfo("notepad.exe") { UseShellExecute = false };
        startInfo.ArgumentList.Add(configurationPath);
        TryStart(startInfo);
    }

    private void TryStart(ProcessStartInfo startInfo)
    {
        try
        {
            Process.Start(startInfo);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            ShowNotification(new MonitorNotification(
                MonitorNotificationKind.Failed,
                "WorldGate Monitor",
                exception.Message,
                null));
        }
    }

    private static string ShortError(string? error)
    {
        const int max = 90;
        if (string.IsNullOrWhiteSpace(error))
        {
            return "unavailable";
        }

        string singleLine = error.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return singleLine.Length <= max ? singleLine : singleLine[..(max - 1)] + "…";
    }

    private void DisposeResources()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Application.Idle -= RefreshOnFirstIdle;
        pollTimer.Stop();
        pollTimer.Dispose();
        shutdown.Cancel();
        trayIcon.Visible = false;
        trayIcon.Dispose();
        currentIcon.Dispose();
        menu.Dispose();
    }
}
