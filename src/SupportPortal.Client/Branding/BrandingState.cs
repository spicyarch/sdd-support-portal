using System.Net;
using System.Net.Http.Json;
using SupportPortal.Contracts.Branding;

namespace SupportPortal.Client.Branding;

public sealed class BrandingState
{
    private readonly HttpClient httpClient;
    private bool loaded;
    private string? etag;

    public BrandingState(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public EffectiveBrandingResponse Current { get; private set; } = CreateDefault();

    public event Action? Changed;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (loaded)
        {
            return;
        }

        loaded = true;
        await RefreshAsync(cancellationToken);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "branding");
            if (!string.IsNullOrWhiteSpace(etag))
            {
                request.Headers.TryAddWithoutValidation("If-None-Match", $"\"{etag}\"");
            }

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                return;
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                return;
            }

            var candidate = await response.Content.ReadFromJsonAsync<EffectiveBrandingResponse>(cancellationToken: cancellationToken);
            if (candidate is null)
            {
                return;
            }

            Apply(candidate);
        }
        catch (HttpRequestException)
        {
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }
    }

    public string CssVariables =>
        $"--brand-primary:{Current.PrimaryColor};--brand-accent:{Current.AccentColor};--brand-focus:{Current.FocusColor};";

    public void Apply(EffectiveBrandingResponse candidate)
    {
        if (!IsSafe(candidate))
        {
            return;
        }

        Current = candidate;
        etag = candidate.ProfileVersion;
        Changed?.Invoke();
    }

    public static EffectiveBrandingResponse CreateDefault() => new(
        "Support Portal",
        "SP",
        "SP",
        null,
        null,
        "#135E96",
        "#006B54",
        "#006B54",
        new SupportContactResponse("Support Operations", "support@example.com"),
        null,
        "DEFAULT");

    private static bool IsSafe(EffectiveBrandingResponse brand) =>
        !string.IsNullOrWhiteSpace(brand.ProductName) &&
        !string.IsNullOrWhiteSpace(brand.ShortProductName) &&
        !string.IsNullOrWhiteSpace(brand.Initials) &&
        IsHexColor(brand.PrimaryColor) &&
        IsHexColor(brand.AccentColor) &&
        IsHexColor(brand.FocusColor) &&
        (brand.LogoUrl is null || IsSafeUrl(brand.LogoUrl)) &&
        (brand.FaviconUrl is null || IsSafeUrl(brand.FaviconUrl));

    private static bool IsHexColor(string value) =>
        value.Length == 7 && value[0] == '#' && value[1..].All(Uri.IsHexDigit);

    private static bool IsSafeUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (StringComparer.OrdinalIgnoreCase.Equals(uri.Scheme, Uri.UriSchemeHttps) ||
         StringComparer.OrdinalIgnoreCase.Equals(uri.Scheme, Uri.UriSchemeHttp) &&
         (StringComparer.OrdinalIgnoreCase.Equals(uri.Host, "localhost") ||
          StringComparer.OrdinalIgnoreCase.Equals(uri.Host, "127.0.0.1")));
}