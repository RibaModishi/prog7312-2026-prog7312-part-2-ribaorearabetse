using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SmartX.Client.Services;
using SmartX.Shared.Realtime;
using SmartX.Shared.Telemetry;

namespace SmartX.Client;

public partial class CommandStreamPage : ContentPage
{
    private readonly SmartXApiClient _api;
    private readonly ObservableCollection<DeviceCard> _cards = [];
    private readonly PeriodicTimer _timer = new(TimeSpan.FromSeconds(5));
    private CancellationTokenSource? _poll;
    private bool _busy;
    private bool _cardsBound;
    private string _suggestionKey = string.Empty;

    private static readonly (string Label, string Value)[] Categories =
    [
        ("Everything", "all"),
        ("Moisture", nameof(SensorCategory.Environmental)),
        ("Power", nameof(SensorCategory.PowerConsumption)),
        ("Pumps and relays", nameof(SensorCategory.Actuator))
    ];

    private static readonly (string Label, string Value)[] Conditions =
    [
        ("Any condition", "all"),
        ("All good", "healthy"),
        ("Needs attention", "threshold"),
        ("Not responding", "disconnected")
    ];

    public CommandStreamPage(SmartXApiClient api)
    {
        InitializeComponent();
        _api = api;
        CategoryPicker.ItemsSource = Categories.Select(item => item.Label).ToList();
        AlertPicker.ItemsSource = Conditions.Select(item => item.Label).ToList();
        CategoryPicker.SelectedIndex = 0;
        AlertPicker.SelectedIndex = 0;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _poll?.Cancel();
        _poll = new CancellationTokenSource();
        _ = PollAsync(_poll.Token);
        _ = RefreshAsync(recordSearch: false);
    }

    protected override void OnDisappearing()
    {
        _poll?.Cancel();
        base.OnDisappearing();
    }

    private async Task PollAsync(CancellationToken token)
    {
        try
        {
            while (await _timer.WaitForNextTickAsync(token))
            {
                if (_busy)
                {
                    continue;
                }

                await RefreshAsync(recordSearch: false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async void OnSearchClicked(object? sender, EventArgs e) =>
        await RefreshAsync(recordSearch: true);

    private async Task RefreshAsync(bool recordSearch)
    {
        var category = Categories[Math.Max(CategoryPicker.SelectedIndex, 0)].Value;
        var alertState = Conditions[Math.Max(AlertPicker.SelectedIndex, 0)].Value;
        var query = QueryEntry.Text;
        var outcome = recordSearch
            ? await _api.SearchCommandStreamAsync(category, alertState, query)
            : await _api.GetCommandStreamAsync(category, alertState, query);

        if (!outcome.IsSuccess)
        {
            ShowFeedback(outcome.ErrorMessage ?? "The gateway did not answer.", FeedbackTone.Bad);
            return;
        }

        ApplyDevices(outcome.Snapshot.Devices);
        var worrying = _cards.Count(card => card.Badge is not ("All good" or "Off" or "Running"));
        var summary = worrying == 0
            ? $"{_cards.Count} devices. Everything looks calm."
            : $"{_cards.Count} devices. {worrying} need a look.";
        if (StatusLabel.Text != summary)
        {
            StatusLabel.Text = summary;
        }

        RenderSuggestions(outcome.Snapshot.Suggestions);
    }

    private void ApplyDevices(IReadOnlyList<CommandStreamDevice> devices)
    {
        var incoming = devices.Select(ToCard).ToList();
        var sameOrder = _cards.Count == incoming.Count
            && _cards.Select(card => card.SensorId).SequenceEqual(incoming.Select(card => card.SensorId));

        if (!_cardsBound)
        {
            foreach (var card in incoming)
            {
                _cards.Add(card);
            }

            DeviceList.ItemsSource = _cards;
            _cardsBound = true;
            return;
        }

        if (sameOrder)
        {
            for (var index = 0; index < incoming.Count; index++)
            {
                _cards[index].CopyFrom(incoming[index]);
            }

            return;
        }

        _cards.Clear();
        foreach (var card in incoming)
        {
            _cards.Add(card);
        }
    }

    private void RenderSuggestions(IReadOnlyList<SuggestedAction> suggestions)
    {
        var key = string.Join("\n", suggestions.Select(suggestion => suggestion.Detail + "|" + suggestion.Action));
        if (key == _suggestionKey)
        {
            return;
        }

        _suggestionKey = key;
        SuggestionsLayout.Children.Clear();
        if (suggestions.Count == 0)
        {
            SuggestionsLayout.Children.Add(new Label
            {
                Text = "Nothing to suggest yet. If you switch a pump on while a moisture sensor is dry, that pattern shows up here.",
                Style = (Style)Application.Current!.Resources["PillarDescription"]
            });
            return;
        }

        foreach (var suggestion in suggestions)
        {
            SuggestionsLayout.Children.Add(new Label
            {
                Text = suggestion.Detail,
                Style = (Style)Application.Current!.Resources["PillarDescription"],
                TextColor = (Color)Application.Current!.Resources["SmartXTextPrimary"],
                FontSize = 15
            });

            if (suggestion.Action is not null && suggestion.SensorId is not null)
            {
                var follow = new Button
                {
                    Text = "Do this",
                    Style = (Style)Application.Current!.Resources["PrimaryButton"],
                    HorizontalOptions = LayoutOptions.Start,
                    WidthRequest = 140,
                    CommandParameter = suggestion.SensorId + "|" + suggestion.Action
                };
                follow.Clicked += async (_, _) =>
                {
                    var parts = ((string)follow.CommandParameter).Split('|');
                    await SendAsync(parts[0], parts[1], "Done. That suggestion has been sent.", FeedbackTone.On);
                };
                SuggestionsLayout.Children.Add(follow);
            }
        }
    }

    private async void OnSwitchOnClicked(object? sender, EventArgs e) =>
        await SendFromButton(sender, "StartPump", "Switched on.", FeedbackTone.On);

    private async void OnSwitchOffClicked(object? sender, EventArgs e) =>
        await SendFromButton(sender, "StopPump", "Switched off.", FeedbackTone.Off);

    private async void OnFlipClicked(object? sender, EventArgs e) =>
        await SendFromButton(sender, "ToggleRelay", "Flipped.", FeedbackTone.On);

    private async Task SendFromButton(object? sender, string action, string success, FeedbackTone tone)
    {
        if (sender is not Button { CommandParameter: string sensorId })
        {
            ShowFeedback("That button was not tied to a device.", FeedbackTone.Bad);
            return;
        }

        await SendAsync(sensorId, action, success, tone);
    }

    private async Task SendAsync(string sensorId, string action, string success, FeedbackTone tone)
    {
        _busy = true;
        ShowFeedback("Sending that to the device…", FeedbackTone.Neutral);
        try
        {
            var (ok, message) = await _api.IssueCommandAsync(sensorId, action);
            ShowFeedback(ok ? success : message, ok ? tone : FeedbackTone.Bad);
            await RefreshAsync(recordSearch: false);
        }
        finally
        {
            _busy = false;
        }
    }

    private async void OnUndoClicked(object? sender, EventArgs e)
    {
        _busy = true;
        ShowFeedback("Undoing the last command…", FeedbackTone.Neutral);
        try
        {
            var (ok, message) = await _api.UndoCommandAsync();
            ShowFeedback(message, ok ? FeedbackTone.Off : FeedbackTone.Bad);
            await RefreshAsync(recordSearch: false);
        }
        finally
        {
            _busy = false;
        }
    }

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Device list",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, [".txt"] }
            })
        });

        if (file is null)
        {
            return;
        }

        _busy = true;
        try
        {
            var result = await _api.ImportDevicesAsync(file);
            ShowFeedback(result is null || result.StartsWith('{')
                ? "Devices loaded. Scroll down to the greenhouse."
                : result, result is not null && !result.Contains("401", StringComparison.Ordinal) ? FeedbackTone.On : FeedbackTone.Bad);
            await RefreshAsync(recordSearch: false);
        }
        finally
        {
            _busy = false;
        }
    }

    private void ShowFeedback(string message, FeedbackTone tone)
    {
        FeedbackCard.IsVisible = true;
        FeedbackCard.BackgroundColor = tone switch
        {
            FeedbackTone.On => Color.FromArgb("#1F7A45"),
            FeedbackTone.Off => Color.FromArgb("#3D4C78"),
            FeedbackTone.Bad => Color.FromArgb("#8E3A3A"),
            _ => Color.FromArgb("#1E3A2A")
        };
        FeedbackLabel.TextColor = Colors.White;
        FeedbackLabel.Text = message;
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync("..");

    private static DeviceCard ToCard(CommandStreamDevice device)
    {
        var relay = device.DisplayName.Contains("relay", StringComparison.OrdinalIgnoreCase);
        var look = Describe(device, relay);
        return new DeviceCard
        {
            SensorId = device.SensorId,
            Title = device.DisplayName,
            Badge = look.Badge,
            Story = look.Story,
            Place = device.DeploymentPath.Replace(">", "·"),
            ImageName = PictureFor(device, relay),
            Accent = look.Accent,
            Wash = look.Wash,
            ShowControls = device.Category == SensorCategory.Actuator,
            Bars = Bars(device.Sparkline, look.Accent)
        };
    }

    private static string PictureFor(CommandStreamDevice device, bool relay) => device.Category switch
    {
        SensorCategory.Environmental => "moisture_plant.jpg",
        SensorCategory.PowerConsumption => "power_glow.jpg",
        _ => relay ? "power_glow.jpg" : "water_pump.jpg"
    };

    private static DeviceLook Describe(CommandStreamDevice device, bool relay)
    {
        var calm = Color.FromArgb("#8FCB9B");
        var warn = Color.FromArgb("#E2B657");
        var down = Color.FromArgb("#E07A7A");
        var calmWash = Color.FromArgb("#1B2922");
        var onWash = Color.FromArgb("#1B4D32");
        var dryWash = Color.FromArgb("#4A3A16");
        var hotWash = Color.FromArgb("#4A2C16");
        var downWash = Color.FromArgb("#4A2222");

        if (device.AlertState == "disconnected")
        {
            return new("Not responding", "This device has stopped sending anything.", down, downWash);
        }

        if (device.Category == SensorCategory.Actuator)
        {
            var on = device.ActuatorOn == true;
            var name = relay ? "relay" : "pump";
            return on
                ? new("Running", $"The {name} is on.", calm, onWash)
                : new("Off", $"The {name} is off. Switch it on if the plants need water.", Color.FromArgb("#A8B8AE"), calmWash);
        }

        if (device.Category == SensorCategory.Environmental)
        {
            var value = device.LastValue ?? 0;
            return value < 20
                ? new("Too dry", $"Soil moisture is {value:0}%. That is below the comfortable 20%.", warn, dryWash)
                : new("All good", $"Soil moisture is {value:0}%.", calm, calmWash);
        }

        var watts = device.LastValue ?? 0;
        return watts >= 1500
            ? new("High power", $"This rail is drawing {watts:0} watts, above the usual 1500.", warn, hotWash)
            : new("All good", $"This rail is drawing {watts:0} watts.", calm, calmWash);
    }

    private readonly record struct DeviceLook(string Badge, string Story, Color Accent, Color Wash);

    private static List<ReadingBar> Bars(IReadOnlyList<double> values, Color color)
    {
        if (values.Count == 0)
        {
            return [new ReadingBar { Height = 6, Color = color }];
        }

        var min = values.Min();
        var span = Math.Max(values.Max() - min, 0.001);
        return values.TakeLast(16).Select(value => new ReadingBar
        {
            Height = 6 + ((value - min) / span * 26),
            Color = color
        }).ToList();
    }
}

public sealed class DeviceCard : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private string _badge = string.Empty;
    private string _story = string.Empty;
    private string _place = string.Empty;
    private string _imageName = "moisture_plant.jpg";
    private Color _accent = Colors.Gray;
    private Color _wash = Color.FromArgb("#1B2922");
    private bool _showControls;
    private List<ReadingBar> _bars = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    public required string SensorId { get; init; }

    public string Title { get => _title; set => Set(ref _title, value); }

    public string Badge { get => _badge; set => Set(ref _badge, value); }

    public string Story { get => _story; set => Set(ref _story, value); }

    public string Place { get => _place; set => Set(ref _place, value); }

    public string ImageName { get => _imageName; set => Set(ref _imageName, value); }

    public Color Accent { get => _accent; set => Set(ref _accent, value); }

    public Color Wash { get => _wash; set => Set(ref _wash, value); }

    public bool ShowControls { get => _showControls; set => Set(ref _showControls, value); }

    public List<ReadingBar> Bars { get => _bars; set => Set(ref _bars, value); }

    public void CopyFrom(DeviceCard other)
    {
        Title = other.Title;
        Badge = other.Badge;
        Story = other.Story;
        Place = other.Place;
        ImageName = other.ImageName;
        Accent = other.Accent;
        Wash = other.Wash;
        ShowControls = other.ShowControls;
        if (!BarsMatch(other.Bars))
        {
            Bars = other.Bars;
        }
    }

    private bool BarsMatch(List<ReadingBar> other) =>
        Bars.Count == other.Count
        && Bars.Zip(other).All(pair => Math.Abs(pair.First.Height - pair.Second.Height) < 0.5);

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public enum FeedbackTone
{
    Neutral,
    On,
    Off,
    Bad
}

public sealed class ReadingBar
{
    public double Height { get; init; }

    public required Color Color { get; init; }
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
