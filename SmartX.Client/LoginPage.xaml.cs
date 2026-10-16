using SmartX.Client.Services;

namespace SmartX.Client;

public partial class LoginPage : ContentPage
{
    private readonly SmartXApiClient _apiClient;

    public LoginPage(SmartXApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    private async void OnSignInClicked(object? sender, EventArgs e)
    {
        var username = UsernameEntry.Text?.Trim() ?? string.Empty;
        var password = PasswordEntry.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ShowFeedback("Enter both a username and a password.");
            return;
        }

        SetBusy(true);

        try
        {
            var outcome = await _apiClient.LoginAsync(username, password);
            if (!outcome.IsSuccess)
            {
                ShowFeedback(outcome.ErrorMessage!);
                return;
            }

            await Shell.Current.GoToAsync("//home");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool isBusy)
    {
        SignInButton.IsEnabled = !isBusy;
        SignInButton.Text = isBusy ? "Signing in…" : "Sign In";
        SignInSpinner.IsVisible = isBusy;
        SignInSpinner.IsRunning = isBusy;
    }

    private void ShowFeedback(string message)
    {
        FeedbackLabel.Text = message;
        FeedbackLabel.TextColor = (Color)Application.Current!.Resources["SmartXDanger"];
        FeedbackCard.Stroke = (Color)Application.Current.Resources["SmartXDanger"];
        FeedbackCard.IsVisible = true;
    }
}
