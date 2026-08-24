using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SupportPortal.Client;
using SupportPortal.Client.Branding;
using SupportPortal.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var authenticationMode = builder.Configuration["Authentication:Mode"] ?? "Development";
if (StringComparer.OrdinalIgnoreCase.Equals(authenticationMode, "Entra"))
{
	builder.Services.AddMsalAuthentication(options =>
	{
		options.ProviderOptions.Authentication.Authority = builder.Configuration["Entra:Authority"] ?? "https://login.microsoftonline.com/";
		options.ProviderOptions.Authentication.ClientId = builder.Configuration["Entra:ClientId"] ?? string.Empty;
		options.ProviderOptions.Authentication.ValidateAuthority = bool.TryParse(builder.Configuration["Entra:ValidateAuthority"], out var validateAuthority) && validateAuthority;
		var scope = builder.Configuration["Entra:ApiScope"];
		if (!string.IsNullOrWhiteSpace(scope))
		{
			options.ProviderOptions.DefaultAccessTokenScopes.Add(scope);
		}
	});
}
else
{
	builder.Services.AddAuthorizationCore();
}

builder.Services.AddScoped<DevelopmentIdentityState>();
builder.Services.AddTransient<ApiAuthenticationHandler>();
builder.Services.AddScoped(sp =>
{
	var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:7071/api/v1";
	var handler = sp.GetRequiredService<ApiAuthenticationHandler>();
	handler.InnerHandler = new HttpClientHandler();
	return new HttpClient(handler)
	{
		BaseAddress = new Uri($"{apiBaseUrl.TrimEnd('/')}/")
	};
});
builder.Services.AddScoped<SupportPortalApiClient>();
builder.Services.AddScoped<BrandingState>();
builder.Services.AddScoped<RequestRefreshService>();

await builder.Build().RunAsync();
