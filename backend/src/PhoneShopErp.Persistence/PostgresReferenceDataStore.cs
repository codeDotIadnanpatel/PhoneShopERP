using Npgsql;

namespace PhoneShopErp.Persistence;

public sealed record CurrencyReference(string Code, short? NumericCode, short? MinorUnit, string DisplayName);

public sealed record CountryReference(string Code, string Alpha3Code, string DisplayName);

public sealed record TaxCategoryReference(string Code, string DisplayName);

public interface IReferenceDataStore
{
    Task<IReadOnlyList<CurrencyReference>> GetCurrenciesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<CountryReference>> GetCountriesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<TaxCategoryReference>> GetTaxCategoriesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Vendor-owned reference data. These reads deliberately need no tenant context:
/// the tables carry no <c>tenant_id</c> and are identical for every tenant.
/// </summary>
public sealed class PostgresReferenceDataStore : IReferenceDataStore
{
    private readonly IDbConnectionFactory connectionFactory;

    public PostgresReferenceDataStore(IDbConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public Task<IReadOnlyList<CurrencyReference>> GetCurrenciesAsync(CancellationToken cancellationToken) =>
        QueryAsync(
            """
            SELECT code, numeric_code, minor_unit, display_name
            FROM platform.currency
            WHERE is_active
            ORDER BY code;
            """,
            reader => new CurrencyReference(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetInt16(1),
                reader.IsDBNull(2) ? null : reader.GetInt16(2),
                reader.GetString(3)),
            cancellationToken);

    public Task<IReadOnlyList<CountryReference>> GetCountriesAsync(CancellationToken cancellationToken) =>
        QueryAsync(
            """
            SELECT code, alpha3_code, display_name
            FROM platform.country
            WHERE is_active
            ORDER BY code;
            """,
            reader => new CountryReference(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2)),
            cancellationToken);

    public Task<IReadOnlyList<TaxCategoryReference>> GetTaxCategoriesAsync(CancellationToken cancellationToken) =>
        QueryAsync(
            """
            SELECT code, display_name
            FROM platform.tax_category
            WHERE is_active
            ORDER BY code;
            """,
            reader => new TaxCategoryReference(
                reader.GetString(0),
                reader.GetString(1)),
            cancellationToken);

    private async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        Func<NpgsqlDataReader, T> map,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var results = new List<T>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(map(reader));
        }

        return results;
    }
}
