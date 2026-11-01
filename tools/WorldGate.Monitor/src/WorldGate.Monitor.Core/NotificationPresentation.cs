namespace WorldGate.Monitor.Core;

public enum MonitorNotificationSeverity
{
    Information,
    Warning,
    Error,
}

public sealed record NotificationPresentation(
    string Title,
    string Message,
    MonitorNotificationSeverity Severity,
    int DurationMilliseconds)
{
    public static NotificationPresentation From(MonitorNotification notification)
    {
        MonitorNotificationSeverity severity = notification.Kind switch
        {
            MonitorNotificationKind.Failed or MonitorNotificationKind.Cancelled => MonitorNotificationSeverity.Error,
            MonitorNotificationKind.RunnerDelayed => MonitorNotificationSeverity.Warning,
            _ => MonitorNotificationSeverity.Information,
        };

        return new NotificationPresentation(
            notification.Title,
            notification.Message,
            severity,
            5000);
    }
}
