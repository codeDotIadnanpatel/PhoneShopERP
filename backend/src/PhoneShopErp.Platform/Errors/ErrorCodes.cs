namespace PhoneShopErp.Platform.Errors;

/// <summary>
/// Stable machine-readable API error codes. Clients branch on these, never on a
/// message string. A new code is additive; an existing code is not renamed.
/// </summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string Unauthenticated = "UNAUTHENTICATED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string TenantContextMissing = "TENANT_CONTEXT_MISSING";
    public const string TenantContextInvalid = "TENANT_CONTEXT_INVALID";
    public const string TenantMismatch = "TENANT_MISMATCH";
    public const string IdempotencyKeyRequired = "IDEMPOTENCY_KEY_REQUIRED";
    public const string IdempotencyKeyReused = "IDEMPOTENCY_KEY_REUSED";
    public const string IdempotencyInProgress = "IDEMPOTENCY_IN_PROGRESS";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
    public const string UnsupportedMediaType = "UNSUPPORTED_MEDIA_TYPE";
    public const string RateLimited = "RATE_LIMITED";
    public const string DependencyUnavailable = "DEPENDENCY_UNAVAILABLE";
    public const string MethodNotAllowed = "METHOD_NOT_ALLOWED";
    public const string InternalError = "INTERNAL_ERROR";
    public const string NotImplemented = "NOT_IMPLEMENTED";
}
