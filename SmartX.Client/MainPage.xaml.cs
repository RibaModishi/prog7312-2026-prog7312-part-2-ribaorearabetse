using SmartX.Client.Services;

namespace SmartX.Client;

public partial class MainPage : ContentPage
{
    private readonly SmartXApiClient _api;
    private bool _isNavigating;

    public MainPage(SmartXApiClient api)
    {
        InitializeComponent();
        _api = api;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            var devices = await _api.CountSensorsAsync();
            var alerts = await _api.GetActiveAlertsAsync();
            var open = alerts.IsSuccess ? alerts.Groups.Count : 0;
            StatusLabel.Text = $"{devices} registered devices · {open} open alert groups";
        }
        catch (Exception)
        {
            StatusLabel.Text = "Signed in. The gateway status will appear once the API answers.";
        }
    }

    private async void OnIngestionTapped(object? sender, TappedEventArgs e)
    {
        // A card is a large target and a double tap fires twice, which would push the registration
        // screen onto the stack twice over.
        if (_isNavigating)
        {
            return;
        }

        _isNavigating = true;

        try
        {
            await Shell.Current.GoToAsync("registration");
        }
        finally
        {
            _isNavigating = false;
        }
    }

    private async void OnCommandStreamTapped(object? sender, TappedEventArgs e)
    {
        if (_isNavigating)
        {
            return;
        }

        _isNavigating = true;

        try
        {
            await Shell.Current.GoToAsync("command-stream");
        }
        finally
        {
            _isNavigating = false;
        }
    }
}
