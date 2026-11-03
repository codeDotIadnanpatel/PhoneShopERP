using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using PhoneShopErp.Modules.IdentityAccess.Application;
using ApplicationTokenValidationResult = PhoneShopErp.Modules.IdentityAccess.Application.TokenValidationResult;

namespace PhoneShopErp.Modules.IdentityAccess.Infrastructure;

/// <summary>
/// Validates a provider access/ID token and reduces it to an
/// <see cref="AuthenticatedPrincipal"/>.
///
/// Only four values ever leave this type: the subject, the provider session
/// reference (<c>sid</c>), the display name and the email. Tenant, membership,
/// role and permission claims are ignored entirely, so a token cannot widen ERP
/// authorisation (ADR-0013). Signature, issuer, audience, lifetime and algorithm
/// are validated; nothing is trusted because it merely parses.
///
/// The signing keys and issuer are supplied by an <see cref="OpenIdConnectConfiguration"/>.
/// Production discovery against a chosen identity provider is not configured in
/// Phase 01 (D-002 remains open).
/// </summary>
public sealed class OidcSubjectTokenValidator : ISubjectTokenValidator
{
    private static readonly string[] AllowedAlgorithms =
    [
        SecurityAlgorithms.RsaSha256,
        SecurityAlgorithms.RsaSha384,
        SecurityAlgorithms.RsaSha512,
        SecurityAlgorithms.EcdsaSha256,
        SecurityAlgorithms.EcdsaSha384,
        SecurityAlgorithms.EcdsaSha512,
    ];

    private readonly JsonWebTokenHandler handler = new()
    {
        // The token is validated, never re-serialised or logged.
        MapInboundClaims = false,
    };

    private readonly TokenValidationParameters parameters;

    public OidcSubjectTokenValidator(
        OpenIdConnectConfiguration configuration,
        string? expectedAudience,
        TimeSpan? clockSkew = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (string.IsNullOrWhiteSpace(configuration.Issuer))
        {
            throw new ArgumentException(
                "The identity provider configuration must declare an issuer.",
                nameof(configuration));
        }

        if (configuration.SigningKeys.Count == 0)
        {
            throw new ArgumentException(
                "The identity provider configuration must expose at least one signing key.",
                nameof(configuration));
        }

        parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = configuration.Issuer,
            ValidateAudience = !string.IsNullOrWhiteSpace(expectedAudience),
            ValidAudience = expectedAudience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = configuration.SigningKeys,
            ValidAlgorithms = AllowedAlgorithms,
            ClockSkew = clockSkew ?? TimeSpan.FromSeconds(30),
            NameClaimType = "name",
        };
    }

    public async Task<ApplicationTokenValidationResult> ValidateAsync(
        string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return ApplicationTokenValidationResult.Invalid("token_missing");
        }

        var result = await handler.ValidateTokenAsync(token, parameters).ConfigureAwait(false);
        if (!result.IsValid)
        {
            return ApplicationTokenValidationResult.Invalid(MapFailure(result.Exception));
        }

        // JsonWebTokenHandler does not enforce RequireExpirationTime for a token
        // that simply omits "exp", so the claim is checked explicitly. An
        // access token without a bounded lifetime is never accepted.
        if (result.SecurityToken is not JsonWebToken validatedToken
            || !validatedToken.TryGetPayloadValue<long>("exp", out var expiry)
            || expiry <= 0)
        {
            return ApplicationTokenValidationResult.Invalid("token_expiry_missing");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var subject = ReadClaim(result, "sub");
        if (string.IsNullOrWhiteSpace(subject) || subject.Length > 255)
        {
            return ApplicationTokenValidationResult.Invalid("token_subject_missing");
        }

        return ApplicationTokenValidationResult.Valid(
            new AuthenticatedPrincipal(
                subject,
                ReadClaim(result, "sid"),
                ReadClaim(result, "name"),
                ReadClaim(result, "email")));
    }

    private static string? ReadClaim(Microsoft.IdentityModel.Tokens.TokenValidationResult result, string type)
    {
        var value = result.ClaimsIdentity?.FindFirst(type)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Maps a validation failure to a stable, non-leaking code. Microsoft's
    /// handler reports an expired or not-yet-valid token as the base
    /// <see cref="SecurityTokenInvalidLifetimeException"/> in some versions, so
    /// the more specific types are checked first and the base falls back to one
    /// lifetime code rather than the unhelpful generic one.
    /// </summary>
    private static string MapFailure(Exception? exception) =>
        exception switch
        {
            SecurityTokenExpiredException => "token_expired",
            SecurityTokenNotYetValidException => "token_not_yet_valid",
            SecurityTokenInvalidLifetimeException => "token_lifetime_invalid",
            SecurityTokenInvalidSignatureException => "token_signature_invalid",
            SecurityTokenInvalidIssuerException => "token_issuer_invalid",
            SecurityTokenInvalidAudienceException => "token_audience_invalid",
            SecurityTokenNoExpirationException => "token_expiry_missing",
            SecurityTokenInvalidAlgorithmException => "token_algorithm_invalid",
            SecurityTokenDecryptionFailedException => "token_decryption_failed",
            SecurityTokenMalformedException => "token_malformed",
            _ => "token_invalid",
        };
}
