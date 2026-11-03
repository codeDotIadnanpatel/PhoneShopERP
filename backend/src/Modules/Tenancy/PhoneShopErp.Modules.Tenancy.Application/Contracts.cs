namespace PhoneShopErp.Modules.Tenancy.Application;

/// <summary>
/// A tenant as exposed by the API. It carries no identity or business data.
/// </summary>
public sealed record TenantSummary(
    Guid Id,
    string Slug,
    string DisplayName,
    string Status,
    string DefaultLocale,
    string DefaultTimezone,
    int RowVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateTenantRequest(
    string Slug,
    string DisplayName,
    string DefaultLocale,
    string DefaultTimezone);

public sealed record SuspendTenantRequest(string Reason);

public sealed record LegalEntitySummary(
    Guid Id,
    string Code,
    string DisplayName,
    string CountryCode,
    string Status,
    int RowVersion);

public sealed record CreateLegalEntityRequest(string Code, string DisplayName, string CountryCode);

public sealed record UpdateLegalEntityRequest(string? DisplayName, string? Status, int RowVersion);

public sealed record BranchSummary(
    Guid Id,
    Guid LegalEntityId,
    string Code,
    string DisplayName,
    string Timezone,
    string Locale,
    string Status,
    int RowVersion);

public sealed record CreateBranchRequest(
    Guid LegalEntityId,
    string Code,
    string DisplayName,
    string Timezone,
    string Locale);

public sealed record UpdateBranchRequest(
    string? DisplayName,
    string? Timezone,
    string? Locale,
    string? Status,
    int RowVersion);

public sealed record StockLocationSummary(
    Guid Id,
    Guid BranchId,
    string Code,
    string DisplayName,
    string LocationType,
    string Status,
    int RowVersion);

public sealed record CreateStockLocationRequest(
    Guid BranchId,
    string Code,
    string DisplayName,
    string LocationType);

public sealed record RegisterSummary(
    Guid Id,
    Guid BranchId,
    string Code,
    string DisplayName,
    string Status,
    int RowVersion);

public sealed record CreateRegisterRequest(Guid BranchId, string Code, string DisplayName);

/// <summary>
/// Cursor page. The cursor is a UUIDv7 identifier, so it is time ordered and does
/// not expose an offset that shifts under concurrent inserts.
/// </summary>
public sealed record Page<T>(IReadOnlyList<T> Items, Guid? NextCursor);

public sealed record PageRequest(int? Limit, Guid? After)
{
    public const int DefaultLimit = 50;
    public const int MaximumLimit = 200;

    public int EffectiveLimit =>
        Limit is null or < 1 ? DefaultLimit : Math.Min(Limit.Value, MaximumLimit);
}

/// <summary>
/// The profile of the authenticated caller, resolved from the validated token and
/// the ERP user row. It is never taken from a request body.
/// </summary>
public sealed record TenantOwnerProfile(string Subject, string DisplayName, string? Email);
