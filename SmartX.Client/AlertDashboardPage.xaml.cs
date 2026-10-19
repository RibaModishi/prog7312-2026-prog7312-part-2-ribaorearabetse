using SmartX.Client.Services;

namespace SmartX.Client;

public partial class AlertDashboardPage : ContentPage
{
    private readonly SmartXApiClient _apiClient;

    // Dispatcher timer, not System.Threading.Timer: Tick already runs on the UI thread, so the
    // CollectionView update does not need a second marshal. Stopped and unsubscribed in
    // OnDisappearing so navigating away cannot leave a background poll alive.
    private IDispatcherTimer? _pollTimer;
    private CancellationTokenSource? _lifetime;
    private bool _isRefreshing;
    private string _snapshot = string.Empty;

    public AlertDashboardPage(SmartXApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _lifetime = new CancellationTokenSource();
        StartPolling();
        _ = RefreshAsync(showSpinner: false);
    }

    protected override void OnDisappearing()
    {
        // Stop first so a Tick already queued cannot start another refresh after cancellation.
        StopPolling();
        _lifetime?.Cancel();
        _lifetime?.Dispose();
        _lifetime = null;

        base.OnDisappearing();
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync("..");

    private async void OnRefreshClicked(object? sender, EventArgs e) =>
        await RefreshAsync(showSpinner: true);

    private async void OnPollTick(object? sender, EventArgs e) =>
        await RefreshAsync(showSpinner: false);

    private void StartPolling()
    {
        // Idempotent: if OnAppearing fires twice without a matching disappear (platform quirks),
        // the previous timer is torn down before a new one is created.
        StopPolling();

        _pollTimer = Dispatcher.CreateTimer();
        _pollTimer.Interval = TimeSpan.FromSeconds(5);
        _pollTimer.Tick += OnPollTick;
        _pollTimer.Start();
    }

    private void StopPolling()
    {
        if (_pollTimer is null)
        {
            return;
        }

        _pollTimer.Stop();
        _pollTimer.Tick -= OnPollTick;
        _pollTimer = null;
    }

    private async Task RefreshAsync(bool showSpinner)
    {
        if (_isRefreshing)
        {
            return;
        }

        var token = _lifetime?.Token ?? CancellationToken.None;
        _isRefreshing = true;
        if (showSpinner)
        {
            SetBusy(true);
        }

        try
        {
            var outcome = await _apiClient.GetActiveAlertsAsync();

            if (token.IsCancellationRequested)
            {
                return;
            }

            if (!outcome.IsSuccess)
            {
                StatusLabel.TextColor = (Color)Application.Current!.Resources["SmartXDanger"];
                StatusLabel.Text = outcome.ErrorMessage;
                return;
            }

            var snapshot = string.Join("|", outcome.Groups.Select(group =>
                $"{group.SensorId}:{group.Severity}:{group.Alerts.Count}:{group.WindowEnd:O}"));
            if (snapshot != _snapshot)
            {
                _snapshot = snapshot;
                AlertList.ItemsSource = outcome.Groups.ToList();
            }

            StatusLabel.TextColor = (Color)Application.Current!.Resources["SmartXTextSecondary"];
            var summary = outcome.Groups.Count == 0
                ? "No open incidents. The greenhouse is quiet."
                : $"{outcome.Groups.Count} open incident{(outcome.Groups.Count == 1 ? "" : "s")}.";
            if (StatusLabel.Text != summary)
            {
                StatusLabel.Text = summary;
            }
        }
        finally
        {
            _isRefreshing = false;
            if (showSpinner && !token.IsCancellationRequested)
            {
                SetBusy(false);
            }
        }
    }

    private void SetBusy(bool isBusy)
    {
        RefreshButton.IsEnabled = !isBusy;
        RefreshSpinner.IsVisible = isBusy;
        RefreshSpinner.IsRunning = isBusy;
    }
}
