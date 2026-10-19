using SmartX.Client.Services;
using SmartX.Shared.Realtime;
using SmartX.Shared.Telemetry;

namespace SmartX.Client;

public partial class CommandStreamPage : ContentPage
{
    private readonly SmartXApiClient _api;
    private readonly PeriodicTimer _timer = new(TimeSpan.FromSeconds(4));
    private readonly CancellationTokenSource _poll = new();

    public CommandStreamPage(SmartXApiClient api)
    {
        InitializeComponent();
        _api = api;
        CategoryPicker.ItemsSource = new[] { "all", nameof(SensorCategory.Environmental), nameof(SensorCategory.PowerConsumption), nameof(SensorCategory.Actuator) };
        AlertPicker.ItemsSource = new[] { "all", "healthy", "threshold", "disconnected" };
        CategoryPicker.SelectedIndex = 0;
        AlertPicker.SelectedIndex = 0;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = PollAsync();
        _ = RefreshAsync(recordSearch: false);
    }

    protected override void OnDisappearing()
    {
        _poll.Cancel();
        base.OnDisappearing();
    }

    private async Task PollAsync()
    {
        try
        {
            while (await _timer.WaitForNextTickAsync(_poll.Token))
            {
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
        var category = CategoryPicker.SelectedItem as string;
        var alertState = AlertPicker.SelectedItem as string;
        var query = QueryEntry.Text;
        var outcome = recordSearch
            ? await _api.SearchCommandStreamAsync(category, alertState, query)
            : await _api.GetCommandStreamAsync(category, alertState, query);

        if (!outcome.IsSuccess)
        {
            StatusLabel.Text = outcome.ErrorMessage;
            return;
        }

        StatusLabel.Text = $"{outcome.Snapshot.Devices.Count} devices on the stream.";
        RenderSuggestions(outcome.Snapshot.Suggestions);
        RenderDevices(outcome.Snapshot.Devices);
    }

    private void RenderSuggestions(IReadOnlyList<SuggestedAction> suggestions)
    {
        SuggestionsLayout.Children.Clear();
        if (suggestions.Count == 0)
        {
            SuggestionsLayout.Children.Add(new Label
            {
                Text = "No pattern yet. Issue a pump command after a low moisture reading and the engine will suggest it here.",
                Style = (Style)Application.Current!.Resources["PillarDescription"]
            });
            return;
        }

        foreach (var suggestion in suggestions)
        {
            SuggestionsLayout.Children.Add(new Label
            {
                Text = $"{suggestion.Title}: {suggestion.Detail}",
                Style = (Style)Application.Current!.Resources["PillarDescription"]
            });
        }
    }

    private void RenderDevices(IReadOnlyList<CommandStreamDevice> devices)
    {
        DeviceLayout.Children.Clear();
        foreach (var device in devices)
        {
            var card = new VerticalStackLayout { Spacing = 4 };
            card.Children.Add(new Label
            {
                Text = $"{device.DisplayName}  ·  {device.Category}  ·  {device.AlertState}",
                Style = (Style)Application.Current!.Resources["PillarTitle"]
            });
            card.Children.Add(new Label
            {
                Text = $"Last {device.LastValue?.ToString("0.#") ?? "—"}   {Spark(device.Sparkline)}",
                Style = (Style)Application.Current!.Resources["PillarDescription"]
            });
            card.Children.Add(new Label
            {
                Text = device.DeploymentPath,
                Style = (Style)Application.Current!.Resources["PillarDescription"]
            });

            if (device.Category == SensorCategory.Actuator)
            {
                var actions = new HorizontalStackLayout { Spacing = 8 };
                actions.Children.Add(CommandButton(device.SensorId, "StartPump", "Start pump"));
                actions.Children.Add(CommandButton(device.SensorId, "StopPump", "Stop pump"));
                actions.Children.Add(CommandButton(device.SensorId, "ToggleRelay", "Toggle relay"));
                card.Children.Add(actions);
            }

            DeviceLayout.Children.Add(new Border
            {
                Style = (Style)Application.Current!.Resources["PillarCard"],
                Content = card
            });
        }
    }

    private Button CommandButton(string sensorId, string action, string label)
    {
        var button = new Button
        {
            Text = label,
            Style = (Style)Application.Current!.Resources["SecondaryButton"]
        };
        button.Clicked += async (_, _) =>
        {
            var error = await _api.IssueCommandAsync(sensorId, action);
            StatusLabel.Text = error ?? $"{label} sent for {sensorId}.";
            await RefreshAsync(recordSearch: false);
        };
        return button;
    }

    private async void OnUndoClicked(object? sender, EventArgs e)
    {
        var error = await _api.UndoCommandAsync();
        StatusLabel.Text = error ?? "Last override undone.";
        await RefreshAsync(recordSearch: false);
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

        var result = await _api.ImportDevicesAsync(file);
        StatusLabel.Text = result ?? "Import finished.";
        await RefreshAsync(recordSearch: false);
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync("..");

    private static string Spark(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return "no samples";
        }

        const string bars = "▁▂▃▄▅▆▇█";
        var min = values.Min();
        var max = values.Max();
        var span = Math.Max(max - min, 0.001);
        return string.Concat(values.Select(value => bars[(int)Math.Clamp((value - min) / span * (bars.Length - 1), 0, bars.Length - 1)]));
    }
}
