using System.Drawing;
using System.Windows.Forms;
using WorldGate.Monitor.Core.WslRuntime;

namespace WorldGate.Monitor;

internal sealed class WslManagerMainForm : Form
{
    private const int StatusPollIntervalMs = 5000;
    private const int NavColumnWidth = 180;
    private const int MonitorColumnWidth = 250;

    private enum AppPage
    {
        Home,
        WslConfig,
        Settings,
    }

    private readonly WslRuntimeStatusService runtimeStatusService = new();
    private readonly StatusBadgeControl wslRuntimeBadge = new();
    private readonly System.Windows.Forms.Timer statusPollTimer = new() { Interval = StatusPollIntervalMs };

    private readonly HomePageControl homePage = new();
    private readonly WslConfigPageControl wslConfigPage = new();
    private readonly SettingsPageControl settingsPage = new();

    private readonly ListBox navigationList = new()
    {
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.None,
        IntegralHeight = false,
        ItemHeight = 28,
    };

    private readonly Panel pageHost = new() { Dock = DockStyle.Fill, Padding = new Padding(4, 8, 4, 8) };

    private AppPage currentPage = AppPage.Home;
    private bool suppressNavigationEvents;

    public WslManagerMainForm()
    {
        Text = "WSL Manager";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(920, 560);
        ClientSize = new Size(980, 640);
        Font = new Font("Segoe UI", 9F);

        wslRuntimeBadge.SetCaption("WSL runtime");

        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(0),
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, NavColumnWidth));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, MonitorColumnWidth));

        var navPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 12, 4, 8),
            BackColor = Color.FromArgb(243, 244, 246),
        };
        navigationList.Items.AddRange(["Home", "WSL configuration", "Settings"]);
        navigationList.SelectedIndex = 0;
        navigationList.SelectedIndexChanged += OnNavigationSelectedIndexChanged;
        navPanel.Controls.Add(navigationList);

        pageHost.Controls.Add(homePage);
        pageHost.Controls.Add(wslConfigPage);
        pageHost.Controls.Add(settingsPage);
        ShowPage(AppPage.Home, syncNavigation: false);

        var monitorPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 12, 12, 8),
        };
        var monitorGroup = new GroupBox
        {
            Text = "Monitor",
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 4, 8, 8),
        };
        var badgeRow = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = true,
            Padding = new Padding(4),
        };
        badgeRow.Controls.Add(wslRuntimeBadge);
        monitorGroup.Controls.Add(badgeRow);
        monitorPanel.Controls.Add(monitorGroup);

        shell.Controls.Add(navPanel, 0, 0);
        shell.Controls.Add(pageHost, 1, 0);
        shell.Controls.Add(monitorPanel, 2, 0);
        Controls.Add(shell);

        wslConfigPage.DirtyStateChanged += (_, _) => UpdateTitle();
        wslConfigPage.WslRestartCompleted = RefreshRuntimeBadgeAsync;
        settingsPage.DirtyStateChanged += (_, _) => UpdateTitle();

        statusPollTimer.Tick += async (_, _) => await RefreshRuntimeBadgeAsync();
        Shown += async (_, _) =>
        {
            wslConfigPage.SyncScrollLayoutOnShow();
            statusPollTimer.Start();
            await RefreshRuntimeBadgeAsync();
        };
        FormClosed += (_, _) => statusPollTimer.Stop();

        UpdateTitle();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            if (wslConfigPage.IsDirty())
            {
                DialogResult result = MessageBox.Show(
                    this,
                    "You have unsaved .wslconfig changes. Close without saving?",
                    "Unsaved changes",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                if (result != DialogResult.Yes)
                {
                    e.Cancel = true;
                    base.OnFormClosing(e);
                    return;
                }
            }

            if (settingsPage.IsDirty())
            {
                DialogResult result = MessageBox.Show(
                    this,
                    "You have unsaved monitor settings. Close without saving?",
                    "Unsaved changes",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                if (result != DialogResult.Yes)
                {
                    e.Cancel = true;
                }
            }
        }

        base.OnFormClosing(e);
    }

    internal void ReloadOnShow()
    {
        wslConfigPage.ReloadOnShow();
        settingsPage.ReloadOnShow();
    }

    private void OnNavigationSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (suppressNavigationEvents)
        {
            return;
        }

        AppPage target = NavigationIndexToPage(navigationList.SelectedIndex);
        if (target == currentPage)
        {
            return;
        }

        if (!TryLeaveCurrentPage())
        {
            suppressNavigationEvents = true;
            try
            {
                navigationList.SelectedIndex = PageToNavigationIndex(currentPage);
            }
            finally
            {
                suppressNavigationEvents = false;
            }

            return;
        }

        ShowPage(target, syncNavigation: true);
    }

    private bool TryLeaveCurrentPage()
    {
        if (currentPage == AppPage.WslConfig && wslConfigPage.IsDirty())
        {
            return ConfirmDiscard("You have unsaved .wslconfig changes. Leave this page without saving?");
        }

        if (currentPage == AppPage.Settings && settingsPage.IsDirty())
        {
            return ConfirmDiscard("You have unsaved monitor settings. Leave this page without saving?");
        }

        return true;
    }

    private bool ConfirmDiscard(string message)
    {
        DialogResult result = MessageBox.Show(
            this,
            message,
            "Unsaved changes",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        return result == DialogResult.Yes;
    }

    private void ShowPage(AppPage page, bool syncNavigation)
    {
        currentPage = page;
        homePage.Visible = page == AppPage.Home;
        wslConfigPage.Visible = page == AppPage.WslConfig;
        settingsPage.Visible = page == AppPage.Settings;

        if (page == AppPage.WslConfig)
        {
            wslConfigPage.SyncScrollLayoutOnShow();
        }

        if (syncNavigation)
        {
            suppressNavigationEvents = true;
            try
            {
                navigationList.SelectedIndex = PageToNavigationIndex(page);
            }
            finally
            {
                suppressNavigationEvents = false;
            }
        }
    }

    private static AppPage NavigationIndexToPage(int index) => index switch
    {
        1 => AppPage.WslConfig,
        2 => AppPage.Settings,
        _ => AppPage.Home,
    };

    private static int PageToNavigationIndex(AppPage page) => page switch
    {
        AppPage.WslConfig => 1,
        AppPage.Settings => 2,
        _ => 0,
    };

    private async Task RefreshRuntimeBadgeAsync()
    {
        WslRuntimeSnapshot snapshot = await Task.Run(runtimeStatusService.GetSnapshot);
        if (IsDisposed)
        {
            return;
        }

        wslRuntimeBadge.ApplySnapshot(snapshot);
    }

    private void UpdateTitle()
    {
        bool dirty = wslConfigPage.IsDirty() || settingsPage.IsDirty();
        Text = dirty ? "WSL Manager *" : "WSL Manager";
    }
}
