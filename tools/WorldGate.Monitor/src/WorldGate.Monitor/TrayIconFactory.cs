using System.Drawing;
using System.Runtime.InteropServices;
using WorldGate.Monitor.Core;

namespace WorldGate.Monitor;

internal static class TrayIconFactory
{
    public static Icon Create(MonitorDisplayState state)
    {
        Color color = state switch
        {
            MonitorDisplayState.Healthy => Color.Green,
            MonitorDisplayState.Queued => Color.Goldenrod,
            MonitorDisplayState.Running => Color.DodgerBlue,
            MonitorDisplayState.Degraded => Color.Red,
            _ => Color.Gray,
        };

        using var bitmap = new Bitmap(16, 16);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        using var brush = new SolidBrush(color);
        graphics.FillEllipse(brush, 2, 2, 12, 12);

        IntPtr handle = bitmap.GetHicon();
        try
        {
            using Icon borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
