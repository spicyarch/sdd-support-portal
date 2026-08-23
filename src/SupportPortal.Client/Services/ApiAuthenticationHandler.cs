using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace SupportPortal.Client.Services;

public sealed class ApiAuthenticationHandler : DelegatingHandler
{
    private readonly IConfiguration configuration;
    private readonly DevelopmentIdentityState developmentIdentity;
    private readonly IServiceProvider services;

    public ApiAuthenticationHandler(IConfiguration configuration, DevelopmentIdentityState developmentIdentity, IServiceProvider services)
    {
        this.configuration = configuration;
        this.developmentIdentity = developmentIdentity;
        this.services = services;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var mode = configuration["Authentication:Mode"] ?? "Development";
        if (StringComparer.OrdinalIgnoreCase.Equals(mode, "Entra"))
        {
            var tokenProvider = services.GetService<IAccessTokenProvider>();
            if (tokenProvider is not null)
            {
                var result = await tokenProvider.RequestAccessToken();
                if (result.TryGetToken(out var token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(developmentIdentity.IdentityKey))
        {
            request.Headers.Add("X-Development-Identity", developmentIdentity.IdentityKey);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}