namespace AddressValidation.Api.Infrastructure.Providers.Smarty;

using System.Net.Http;

/// <summary>
/// Delegating handler that appends Smarty <c>auth-id</c> and <c>auth-token</c>
/// query parameters to every outgoing request.
/// Credentials are injected at construction time and never appear in source code.
/// </summary>
internal sealed class SmartyAuthHandler : DelegatingHandler
{
    private readonly string _authId;
    private readonly string _authToken;

    public SmartyAuthHandler(string authId, string authToken)
    {
        if (string.IsNullOrWhiteSpace(authId)) throw new ArgumentException("Smarty auth-id is required.", nameof(authId));
        if (string.IsNullOrWhiteSpace(authToken)) throw new ArgumentException("Smarty auth-token is required.", nameof(authToken));

        _authId = authId;
        _authToken = authToken;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var originalUri = request.RequestUri ?? throw new InvalidOperationException("Request URI is null.");
        var separator = string.IsNullOrEmpty(originalUri.Query) ? "?" : "&";
        var newUri = new Uri(
            $"{originalUri}{separator}auth-id={Uri.EscapeDataString(_authId)}&auth-token={Uri.EscapeDataString(_authToken)}");

        request.RequestUri = newUri;

        return base.SendAsync(request, cancellationToken);
    }
}
