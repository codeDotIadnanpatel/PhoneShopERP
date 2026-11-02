using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PhoneShopErp.Api.Integration.Tests;

/// <summary>
/// Proves the API host boots and exposes its transport baseline without any
/// database, cache, secret or external dependency. These tests are intentionally
/// about infrastructure only; there are no business endpoints in Phase 00.
/// </summary>
public sealed class ApiHostTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiHostTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task LivenessProbe_IsAvailableWithoutInfrastructure()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnknownRoute_ReturnsProblemDetails()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/__does-not-exist", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
