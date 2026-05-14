using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Unit tests for <see cref="AspireC4LifecycleHook"/> internal helpers.
/// </summary>
public sealed class AspireC4LifecycleHookTests
{
	// ── SelectDashboardBaseUrl ────────────────────────────────────────────────

	[Test]
	public async Task SelectDashboardBaseUrl_HttpsNamedEndpoint_ReturnsBaseUrl()
	{
		// Arrange
		var urls = new[] { new UrlSnapshot("https", "https://localhost:17134/", IsInternal: false) };

		// Act
		var result = AspireC4LifecycleHook.SelectDashboardBaseUrl(urls);

		// Assert
		await Assert.That(result).IsEqualTo("https://localhost:17134");
	}

	[Test]
	public async Task SelectDashboardBaseUrl_HttpsNamedEndpoint_WithLoginToken_StripsPath()
	{
		// Arrange
		// The aspire-dashboard appends /login?t=... to the URL when a browser token is configured.
		var urls = new[] { new UrlSnapshot("https", "https://localhost:17134/login?t=abc123", IsInternal: false) };

		// Act
		var result = AspireC4LifecycleHook.SelectDashboardBaseUrl(urls);

		// Assert
		await Assert.That(result).IsEqualTo("https://localhost:17134");
	}

	[Test]
	[Arguments("otlp-http")]
	[Arguments("otlp-grpc")]
	[Arguments("resource-service")]
	public async Task SelectDashboardBaseUrl_OtlpOrServiceEndpointBeforeBrowserEndpoint_SelectsBrowserEndpoint(
		string otlpName
	)
	{
		// Arrange
		// Regression test: when the OTLP/service HTTPS endpoint appears BEFORE the browser
		// frontend in the URL list, the browser endpoint ("https") must still win.
		var urls = new[]
		{
			new UrlSnapshot(otlpName, "https://localhost:22000/", IsInternal: false),
			new UrlSnapshot("https", "https://localhost:17134/", IsInternal: false),
		};

		// Act
		var result = AspireC4LifecycleHook.SelectDashboardBaseUrl(urls);

		// Assert
		await Assert.That(result).IsEqualTo("https://localhost:17134");
	}

	[Test]
	public async Task SelectDashboardBaseUrl_MultipleNonBrowserHttpsUrls_SelectsNamedHttpsFirst()
	{
		// Arrange
		// Three non-internal HTTPS URLs — only the one named "https" is the browser frontend.
		var urls = new[]
		{
			new UrlSnapshot("otlp-http", "https://localhost:22001/", IsInternal: false),
			new UrlSnapshot("https", "https://localhost:17134/login?t=token", IsInternal: false),
			new UrlSnapshot("otlp-grpc", "https://localhost:22002/", IsInternal: false),
		};

		// Act
		var result = AspireC4LifecycleHook.SelectDashboardBaseUrl(urls);

		// Assert
		await Assert.That(result).IsEqualTo("https://localhost:17134");
	}

	[Test]
	public async Task SelectDashboardBaseUrl_HttpNamedEndpointOnly_ReturnsBaseUrl()
	{
		// Arrange
		// HTTP-only setup (no TLS) — the browser frontend is still named "http".
		var urls = new[] { new UrlSnapshot("http", "http://localhost:15000/", IsInternal: false) };

		// Act
		var result = AspireC4LifecycleHook.SelectDashboardBaseUrl(urls);

		// Assert
		await Assert.That(result).IsEqualTo("http://localhost:15000");
	}

	[Test]
	public async Task SelectDashboardBaseUrl_NullNameFallsBackToScheme()
	{
		// Arrange
		// If no endpoint has a "https"/"http" name, fall back to scheme-based selection.
		var urls = new[] { new UrlSnapshot(Name: null, "https://localhost:17134/", IsInternal: false) };

		// Act
		var result = AspireC4LifecycleHook.SelectDashboardBaseUrl(urls);

		// Assert
		await Assert.That(result).IsEqualTo("https://localhost:17134");
	}

	[Test]
	public async Task SelectDashboardBaseUrl_AllUrlsInternal_ReturnsNull()
	{
		// Arrange
		var urls = new[]
		{
			new UrlSnapshot("https", "https://localhost:17134/", IsInternal: true),
			new UrlSnapshot("http", "http://localhost:17134/", IsInternal: true),
		};

		// Act
		var result = AspireC4LifecycleHook.SelectDashboardBaseUrl(urls);

		// Assert
		await Assert.That(result).IsNull();
	}

	[Test]
	public async Task SelectDashboardBaseUrl_EmptyList_ReturnsNull()
	{
		// Arrange

		// Act
		var result = AspireC4LifecycleHook.SelectDashboardBaseUrl([]);

		// Assert
		await Assert.That(result).IsNull();
	}

	[Test]
	public async Task SelectDashboardBaseUrl_HttpsFallbackHttpBeforeHttps_SelectsHttps()
	{
		// Arrange
		// When both named-endpoint checks miss (null names), scheme-based fallback
		// must prefer HTTPS over HTTP.
		var urls = new[]
		{
			new UrlSnapshot(Name: null, "http://localhost:15000/", IsInternal: false),
			new UrlSnapshot(Name: null, "https://localhost:17134/", IsInternal: false),
		};

		// Act
		var result = AspireC4LifecycleHook.SelectDashboardBaseUrl(urls);

		// Assert
		await Assert.That(result).IsEqualTo("https://localhost:17134");
	}

	// ── ResolveAspireBrowserToken ─────────────────────────────────────────────

	[Test]
	public async Task ResolveAspireBrowserToken_TokenDisabled_WithConfiguredToken_ReturnsNull()
	{
		// Arrange
		// Default behaviour: IncludeAspireTokenInDashboardLinks = false → never expose the token.
		var config = CreateConfig("super-secret");
		var options = CreateOptions(includeToken: false);

		// Act
		var result = AspireC4LifecycleHook.ResolveAspireBrowserToken(config, options);

		// Assert
		await Assert.That(result).IsNull();
	}

	[Test]
	public async Task ResolveAspireBrowserToken_TokenEnabled_WithConfiguredToken_ReturnsToken()
	{
		// Arrange
		// When opt-in is set and a token exists in configuration, the token is returned
		// so that dashboard links are built as /login?t=<token>&returnUrl=<path>.
		var config = CreateConfig("super-secret");
		var options = CreateOptions(includeToken: true);

		// Act
		var result = AspireC4LifecycleHook.ResolveAspireBrowserToken(config, options);

		// Assert
		await Assert.That(result).IsEqualTo("super-secret");
	}

	[Test]
	public async Task ResolveAspireBrowserToken_TokenEnabled_WithNoConfiguredToken_ReturnsNull()
	{
		// Arrange
		// Opt-in is set but no BrowserToken key exists in configuration (e.g. auth disabled).
		var config = CreateConfig(browserToken: null);
		var options = CreateOptions(includeToken: true);

		// Act
		var result = AspireC4LifecycleHook.ResolveAspireBrowserToken(config, options);

		// Assert
		await Assert.That(result).IsNull();
	}

	[Test]
	public async Task ResolveAspireBrowserToken_DefaultOptions_TokenIsDisabledByDefault()
	{
		// Arrange
		// Safety default: IncludeAspireTokenInDashboardLinks must be false out of the box
		// so that tokens are never accidentally embedded in generated diagrams.
		var config = CreateConfig("should-not-appear");
		var options = new AspireC4DiagramOptions();

		// Act
		var result = AspireC4LifecycleHook.ResolveAspireBrowserToken(config, options);

		// Assert
		await Assert.That(result).IsNull();
	}

	static IConfiguration CreateConfig(string? browserToken)
	{
		var config = Substitute.For<IConfiguration>();
		config["AppHost:BrowserToken"].Returns(browserToken);
		return config;
	}

	static AspireC4DiagramOptions CreateOptions(bool includeToken) =>
		new() { IncludeAspireTokenInDashboardLinks = includeToken };
}
