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


// References (IIE Harvard). Accessed: 19 October 2026.
// Cormen, T.H., Leiserson, C.E., Rivest, R.L. and Stein, C. (2022) Introduction to algorithms. 4th edn. Cambridge, MA: MIT Press.
// East, D. (n.d.) Getting started with Firebase Auth on the Web. Available at: https://www.youtube.com/watch?v=-OKrloDzGpU
// Firebase (n.d.) Firebase Authentication REST API. Available at: https://firebase.google.com/docs/reference/rest/auth
// Firebase (n.d.) Get started with Firebase Authentication on websites. Available at: https://firebase.google.com/docs/auth/web/start
// Firebase (n.d.) Verify ID tokens. Available at: https://firebase.google.com/docs/auth/admin/verify-id-tokens
// Google Cloud (n.d.) Method: accounts.signInWithPassword. Available at: https://docs.cloud.google.com/identity-platform/docs/reference/rest/v1/accounts/signInWithPassword
// Janbodnar (n.d.) C# PriorityQueue. Available at: https://zetcode.com/csharp/priority-queue/
// Microsoft (n.d.) Dictionary<TKey,TValue> class. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2
// Microsoft (n.d.) HashSet<T> class. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1
// Microsoft (n.d.) Overview of ASP.NET Core authentication. Available at: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/
// Microsoft (n.d.) PriorityQueue<TElement,TPriority> class. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.priorityqueue-2
// Microsoft (n.d.) Queue<T> class. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.queue-1
// Microsoft (n.d.) SortedDictionary<TKey,TValue> class. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.sorteddictionary-2
// Microsoft (n.d.) Stack<T> class. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.stack-1
// Microsoft (n.d.) Use HttpClientFactory to implement resilient HTTP requests. Available at: https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory
// NetMentor (2021) PriorityQueue in C#. Available at: https://www.netmentor.es/entrada/en/priorityqueue-csharp
// .NET Core Tutorials (n.d.) PriorityQueue in C# .NET. Available at: https://dotnetcoretutorials.com/priorityqueue-in-net/
// Roy Chowdhury, K. (2024) Learn Advanced C# Part 1: Step by step build PriorityQueue. Available at: https://www.youtube.com/watch?v=ikbp6vph-6s
// YouTube (n.d.) C# Collections tutorial: List, HashSet, Dictionary, Queue and Stack. Available at: https://www.youtube.com/watch?v=4Mi4OuSMo4A
// YouTube (n.d.) C# collections: Stack, Queue and Dictionary. Available at: https://www.youtube.com/watch?v=GmftVtxrKUc
// YouTube (n.d.) C# hands on: queues. Available at: https://www.youtube.com/watch?v=huzE208-ha8
// YouTube (n.d.) Master the PriorityQueue, a collection in .NET. Available at: https://www.youtube.com/watch?v=d262kUQKT0E

