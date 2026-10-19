using System.Net.Http.Headers;

namespace SmartX.Client.Services;

public sealed class SessionAuthHandler(OperatorSession session) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(session.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
