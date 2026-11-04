using Xunit;

namespace PhoneShopErp.Database.Tests;

/// <summary>
/// Catalog invariants that must hold for every tenant-owned table. These catch a
/// new table shipped without forced RLS or without a tenant-safe foreign key.
/// </summary>
[Collection(PostgresDatabaseGroup.Name)]
public sealed class SchemaInvariantTests
{
    private static readonly string[] ManagedSchemas = ["platform", "tenancy", "identity", "saas"];

    /// <summary>
    /// Reviewed global reference tables. They carry no tenant business data and
    /// are read-only to the runtime role, so RLS does not apply.
    /// </summary>
    private static readonly string[] GlobalTables =
    [
        "identity.permission",
        "platform.country",
        "platform.currency",
        "platform.schema_migration",
        "platform.tax_category",
        "saas.plan",
        "saas.plan_feature",
    ];

    private readonly DatabaseFixture fixture;

    public SchemaInvariantTests(DatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    private TestDatabase Database => fixture.Database;

    [Fact]
    public async Task EveryTenantOwnedTable_HasForcedRowLevelSecurity()
    {
        var tables = await Database.QueryAsync(
            """
            SELECT n.nspname || '.' || c.relname, c.relrowsecurity, c.relforcerowsecurity
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname IN ('platform', 'tenancy', 'identity', 'saas')
              AND c.relkind = 'r'
            ORDER BY 1;
            """,
            "postgres");

        var withoutRls = tables
            .Where(row => !(bool)row[1]!)
            .Select(row => (string)row[0]!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(GlobalTables.OrderBy(name => name, StringComparer.Ordinal), withoutRls);

        foreach (var row in tables)
        {
            var name = (string)row[0]!;
            if (GlobalTables.Contains(name, StringComparer.Ordinal))
            {
                continue;
            }

            Assert.True((bool)row[1]!, $"{name} has RLS disabled.");
            Assert.True((bool)row[2]!, $"{name} has RLS enabled but not FORCED.");
        }
    }

    [Fact]
    public async Task EveryRlsTable_HasAtLeastOnePolicy()
    {
        var rows = await Database.QueryAsync(
            """
            SELECT n.nspname || '.' || c.relname, count(p.polname)
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            LEFT JOIN pg_policy p ON p.polrelid = c.oid
            WHERE n.nspname IN ('platform', 'tenancy', 'identity', 'saas')
              AND c.relkind = 'r'
              AND c.relrowsecurity
            GROUP BY 1
            HAVING count(p.polname) = 0;
            """,
            "postgres");

        Assert.Empty(rows);
    }

    [Fact]
    public async Task EveryTenantColumn_IsNotNull()
    {
        var rows = await Database.QueryAsync(
            """
            SELECT c.table_schema || '.' || c.table_name
            FROM information_schema.columns c
            JOIN information_schema.tables t
              ON t.table_schema = c.table_schema AND t.table_name = c.table_name
            WHERE c.column_name = 'tenant_id'
              AND c.table_schema IN ('platform', 'tenancy', 'identity', 'saas')
              AND t.table_type = 'BASE TABLE'
              AND c.is_nullable = 'YES';
            """,
            "postgres");

        Assert.Empty(rows);
    }

    [Fact]
    public async Task EveryForeignKeyToATenantOwnedTable_CarriesTenantId()
    {
        var rows = await Database.QueryAsync(
            """
            SELECT conrelid::regclass::text || ' -> ' || confrelid::regclass::text,
                   pg_get_constraintdef(oid)
            FROM pg_constraint c
            WHERE contype = 'f'
              AND connamespace IN (
                  'platform'::regnamespace, 'tenancy'::regnamespace,
                  'identity'::regnamespace, 'saas'::regnamespace)
              AND EXISTS (
                  SELECT 1 FROM pg_attribute referenced
                  WHERE referenced.attrelid = c.confrelid
                    AND referenced.attname = 'tenant_id'
                    AND referenced.attnum > 0
                    AND NOT referenced.attisdropped)
              AND NOT EXISTS (
                  SELECT 1 FROM pg_attribute child
                  WHERE child.attrelid = c.conrelid
                    AND child.attname = 'tenant_id'
                    AND child.attnum = ANY(c.conkey));
            """,
            "postgres");

        Assert.Empty(rows);
    }

    [Fact]
    public async Task AppendOnlyLedgers_HaveTheRejectMutationTrigger()
    {
        var rows = await Database.QueryAsync(
            """
            -- information_schema.triggers has one row per triggering event, so a
            -- BEFORE UPDATE OR DELETE trigger appears twice. The invariant is that
            -- each ledger has the guard, not how many events it fires on.
            SELECT DISTINCT event_object_table, trigger_name
            FROM information_schema.triggers
            WHERE trigger_name IN ('trg_audit_event_append_only', 'trg_security_event_append_only')
            ORDER BY 1;
            """,
            "postgres");

        Assert.Equal(
            ["audit_event", "security_event"],
            rows.Select(row => (string)row[0]!).ToArray());
    }

    [Fact]
    public async Task EveryFunctionInTheManagedSchemas_PinsItsSearchPath()
    {
        var rows = await Database.QueryAsync(
            """
            SELECT n.nspname || '.' || p.proname
            FROM pg_proc p
            JOIN pg_namespace n ON n.oid = p.pronamespace
            WHERE n.nspname = ANY(ARRAY['platform', 'tenancy', 'identity', 'saas'])
              AND NOT EXISTS (
                  SELECT 1 FROM unnest(p.proconfig) config
                  WHERE config LIKE 'search_path=%');
            """,
            "postgres");

        Assert.Empty(rows);
    }

    [Fact]
    public async Task OnlyTheReviewedEnumerationFunction_IsSecurityDefiner()
    {
        var rows = await Database.QueryAsync(
            """
            SELECT n.nspname || '.' || p.proname
            FROM pg_proc p
            JOIN pg_namespace n ON n.oid = p.pronamespace
            WHERE n.nspname = ANY(ARRAY['platform', 'tenancy', 'identity', 'saas'])
              AND p.prosecdef
            ORDER BY 1;
            """,
            "postgres");

        Assert.Equal(["platform.tenant_ids"], rows.Select(row => (string)row[0]!).ToArray());
    }

    [Fact]
    public async Task TenantTables_HaveAnIndexLeadingWithTenantId()
    {
        var rows = await Database.QueryAsync(
            """
            SELECT c.relname
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname IN ('platform', 'tenancy', 'identity', 'saas')
              AND c.relkind = 'r'
              AND c.relrowsecurity
              AND EXISTS (
                  SELECT 1 FROM information_schema.columns col
                  WHERE col.table_schema = n.nspname
                    AND col.table_name = c.relname
                    AND col.column_name = 'tenant_id')
              AND NOT EXISTS (
                  SELECT 1
                  FROM pg_index i
                  JOIN pg_attribute a ON a.attrelid = i.indrelid AND a.attnum = i.indkey[0]
                  WHERE i.indrelid = c.oid AND a.attname = 'tenant_id')
              AND c.relname <> 'tenant'
              AND c.relname <> 'security_event';
            """,
            "postgres");

        Assert.Empty(rows);
    }

    [Fact]
    public async Task TenantRoot_KeepsTenantIdInStepWithId()
    {
        var definition = await Database.ScalarAsync<string>(
            """
            SELECT pg_get_constraintdef(oid)
            FROM pg_constraint
            WHERE conrelid = 'tenancy.tenant'::regclass AND conname = 'ck_tenant_tenant_id_matches_id';
            """,
            "postgres");

        Assert.Contains("tenant_id = id", definition, StringComparison.Ordinal);
    }
}
