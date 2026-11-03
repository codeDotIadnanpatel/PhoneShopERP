using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using PhoneShopErp.Modules.IdentityAccess.Infrastructure;
using Xunit;

namespace PhoneShopErp.Platform.Tests;

public sealed class OidcSubjectTokenValidatorTests : IDisposable
{
    private const string Issuer = "https://identity.example.test";
    private const string Audience = "phoneshoperp-api";

    private readonly RSA signingKey = RSA.Create(2048);
    private readonly RSA otherKey = RSA.Create(2048);
    private readonly OpenIdConnectConfiguration configuration;

    public OidcSubjectTokenValidatorTests()
    {
        configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
        configuration.SigningKeys.Add(new RsaSecurityKey(signingKey) { KeyId = "test-key" });
    }

    public void Dispose()
    {
        signingKey.Dispose();
        otherKey.Dispose();
    }

    [Fact]
    public async Task ValidateAsync_ReturnsOnlyTheFourAllowedClaims()
    {
        var token = CreateToken(claims: new Dictionary<string, object>
        {
            ["sid"] = "provider-session-1",
            ["name"] = "Owner Person",
            ["email"] = "owner@example.test",
            ["tenant_id"] = "11111111-1111-1111-1111-111111111111",
            ["roles"] = "owner",
            ["permissions"] = "tenant.suspend",
        });

        var result = await ValidateAsync(token);

        Assert.True(result.IsValid);
        Assert.Null(result.FailureCode);
        Assert.NotNull(result.Principal);
        Assert.Equal("subject-1", result.Principal.Subject);
        Assert.Equal("provider-session-1", result.Principal.ProviderSessionReference);
        Assert.Equal("Owner Person", result.Principal.DisplayName);
        Assert.Equal("owner@example.test", result.Principal.Email);
    }

    [Fact]
    public async Task ValidateAsync_DoesNotExposeTenantOrAuthorisationClaims()
    {
        var token = CreateToken(claims: new Dictionary<string, object>
        {
            ["tenant_id"] = "11111111-1111-1111-1111-111111111111",
            ["permissions"] = "tenant.suspend",
        });

        var result = await ValidateAsync(token);

        Assert.True(result.IsValid);
        var principal = Assert.IsType<PhoneShopErp.Modules.IdentityAccess.Application.AuthenticatedPrincipal>(
            result.Principal);
        Assert.Null(principal.ProviderSessionReference);
        Assert.Null(principal.Email);
        Assert.DoesNotContain("11111111", principal.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("tenant.suspend", principal.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAsync_RejectsAnExpiredToken()
    {
        var token = CreateToken(expires: DateTime.UtcNow.AddMinutes(-10));

        var result = await ValidateAsync(token);

        Assert.False(result.IsValid);
        Assert.Equal("token_lifetime_invalid", result.FailureCode);
        Assert.Null(result.Principal);
    }

    [Fact]
    public async Task ValidateAsync_RejectsATokenWithoutAnExpiry()
    {
        var token = CreateTokenWithoutExpiry();

        var result = await ValidateAsync(token);

        Assert.False(result.IsValid);
        Assert.Equal("token_expiry_missing", result.FailureCode);
        Assert.Null(result.Principal);
    }

    [Fact]
    public async Task ValidateAsync_RejectsAnotherAudience()
    {
        var token = CreateToken(audience: "some-other-api");

        var result = await ValidateAsync(token);

        Assert.False(result.IsValid);
        Assert.Equal("token_audience_invalid", result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_RejectsAnotherIssuer()
    {
        var token = CreateToken(issuer: "https://attacker.example.test");

        var result = await ValidateAsync(token);

        Assert.False(result.IsValid);
        Assert.Equal("token_issuer_invalid", result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_RejectsATokenSignedByAnotherKey()
    {
        var token = CreateToken(credentials: new SigningCredentials(
            new RsaSecurityKey(otherKey) { KeyId = "test-key" },
            SecurityAlgorithms.RsaSha256));

        var result = await ValidateAsync(token);

        Assert.False(result.IsValid);
        Assert.Equal("token_signature_invalid", result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_RejectsAnUnsignedToken()
    {
        var handler = new JsonWebTokenHandler();
        var token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Expires = DateTime.UtcNow.AddMinutes(5),
            Claims = new Dictionary<string, object> { ["sub"] = "subject-1" },
        });

        var result = await ValidateAsync(token);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_RejectsAnAlgorithmOutsideTheAllowList()
    {
        var token = CreateToken(credentials: new SigningCredentials(
            new SymmetricSecurityKey("a-shared-secret-that-is-long-enough"u8.ToArray()),
            SecurityAlgorithms.HmacSha256));

        var result = await ValidateAsync(token);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_RejectsAMissingSubject()
    {
        var token = CreateToken(claims: new Dictionary<string, object> { ["sid"] = "provider-session-1" }, includeSubject: false);

        var result = await ValidateAsync(token);

        Assert.False(result.IsValid);
        Assert.Equal("token_subject_missing", result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_RejectsAnOversizedSubject()
    {
        var token = CreateToken(claims: new Dictionary<string, object> { ["sub"] = new string('s', 256) });

        var result = await ValidateAsync(token);

        Assert.False(result.IsValid);
        Assert.Equal("token_subject_missing", result.FailureCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-jwt")]
    [InlineData("a.b.c")]
    public async Task ValidateAsync_RejectsMalformedInputWithoutThrowing(string token)
    {
        var result = await ValidateAsync(token);

        Assert.False(result.IsValid);
        Assert.Null(result.Principal);
        Assert.False(string.IsNullOrWhiteSpace(result.FailureCode));
    }

    [Fact]
    public void Constructor_RequiresAnIssuer()
    {
        var invalid = new OpenIdConnectConfiguration();
        invalid.SigningKeys.Add(new RsaSecurityKey(signingKey));

        Assert.Throws<ArgumentException>(() => new OidcSubjectTokenValidator(invalid, Audience));
    }

    [Fact]
    public void Constructor_RequiresAtLeastOneSigningKey()
    {
        var invalid = new OpenIdConnectConfiguration { Issuer = Issuer };

        Assert.Throws<ArgumentException>(() => new OidcSubjectTokenValidator(invalid, Audience));
    }

    [Fact]
    public async Task ValidateAsync_AcceptsTheTokenWhenNoAudienceIsConfigured()
    {
        var validator = new OidcSubjectTokenValidator(configuration, expectedAudience: null);
        var token = CreateToken(audience: "any-audience");

        var result = await validator.ValidateAsync(token, CancellationToken.None);

        Assert.True(result.IsValid);
    }

    private Task<PhoneShopErp.Modules.IdentityAccess.Application.TokenValidationResult> ValidateAsync(string token)
    {
        var validator = new OidcSubjectTokenValidator(configuration, Audience);
        return validator.ValidateAsync(token, CancellationToken.None);
    }

    private string CreateToken(
        string issuer = Issuer,
        string audience = Audience,
        DateTime? expires = null,
        IDictionary<string, object>? claims = null,
        SigningCredentials? credentials = null,
        bool includeSubject = true,
        bool includeExpiry = true)
    {
        var payload = new Dictionary<string, object>(claims ?? new Dictionary<string, object>(), StringComparer.Ordinal);
        if (includeSubject)
        {
            payload.TryAdd("sub", "subject-1");
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = DateTime.UtcNow.AddMinutes(-1),
            NotBefore = DateTime.UtcNow.AddMinutes(-1),
            Expires = includeExpiry ? expires ?? DateTime.UtcNow.AddMinutes(5) : null,
            Claims = payload,
            SigningCredentials = credentials ?? new SigningCredentials(
                new RsaSecurityKey(signingKey) { KeyId = "test-key" },
                SecurityAlgorithms.RsaSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    /// <summary>
    /// <see cref="JsonWebTokenHandler"/> supplies a default lifetime when the
    /// descriptor omits one, so the no-expiry case is built with the legacy
    /// handler, which omits "exp" entirely.
    /// </summary>
    private string CreateTokenWithoutExpiry()
    {
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: [new System.Security.Claims.Claim("sub", "subject-1")],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: null,
            signingCredentials: new SigningCredentials(
                new RsaSecurityKey(signingKey) { KeyId = "test-key" },
                SecurityAlgorithms.RsaSha256));

        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }
}
