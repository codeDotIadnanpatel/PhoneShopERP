using Npgsql;
using Xunit;

namespace PhoneShopErp.Database.Tests;

/// <summary>
/// Role model and privilege surface. These are catalog-level assertions executed
/// against the real cluster, not against the migration text.
/// </summary>
[Collection(PostgresDatabaseGroup.Name)]
public sealed class RoleAndGrantTests
{
    private readonly DatabaseFixture fixture;

    public RoleAndGrantTests(DatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    private TestDatabase Database => fixture.Database;

    [Fact]
    public async Task Roles_HaveTheApprovedAttributes()
    {
        var rows = await Database.QueryAsync(
            """
            SELECT rolname, rolcanlogin, rolsuper, rolcreatedb, rolcreaterole, rolbypassrls
            FROM pg_roles
            WHERE rolname IN ('erp_owner', 'erp_migrator', 'erp_app', 'erp_platform_admin')
            ORDER BY rolname;
            """,
            "postgres");

        Assert.Equal(4, rows.Count);

        var byName = rows.ToDictionary(row => (string)row[0]!, row => row);
        AssertRole(byName["erp_app"], canLogin: true, bypassRls: false);
        AssertRole(byName["erp_migrator"], canLogin: true, bypassRls: false);
        AssertRole(byName["erp_owner"], canLogin: false, bypassRls: false);
        AssertRole(byName["erp_platform_admin"], canLogin: true, bypassRls: true);

        static void AssertRole(object?[] row, bool canLogin, bool bypassRls)
        {
            Assert.Equal(canLogin, (bool)row[1]!);
            Assert.False((bool)row[2]!, "No ERP role may be a superuser.");
            Assert.False((bool)row[3]!, "No ERP role may create databases.");
            Assert.False((bool)row[4]!, "No ERP role may create roles.");
            Assert.Equal(bypassRls, (bool)row[5]!);
        }
    }

    [Fact]
    public async Task RuntimeRole_CannotAssumeTheOwnerOrPlatformRole()
    {
        var owner = await PostgresAssert.RejectsAsync(Database, "SET ROLE erp_owner;", "erp_app", "42501");
        var platform = await PostgresAssert.RejectsAsync(Database, "SET ROLE erp_platform_admin;", "erp_app", "42501");

        Assert.Contains("permission denied", owner.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("permission denied", platform.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RuntimeRole_CannotCreateSchemaOrObjects()
    {
        await PostgresAssert.RejectsAsync(Database, "CREATE SCHEMA hostile;", "erp_app", "42501");
        await PostgresAssert.RejectsAsync(Database, "CREATE TABLE public.hostile (id int);", "erp_app", "42501");
        await PostgresAssert.RejectsAsync(Database, "CREATE TABLE tenancy.hostile (id int);", "erp_app", "42501");
    }

    [Fact]
    public async Task RuntimeRole_HasNoDeleteOrUpdateOnTheAuditLedger()
    {
        await PostgresAssert.RejectsAsync(
            Database,
            "UPDATE platform.audit_event SET outcome = 'denied';",
            "erp_app",
            "42501");

        await PostgresAssert.RejectsAsync(
            Database,
            "DELETE FROM platform.audit_event;",
            "erp_app",
            "42501");

        await PostgresAssert.RejectsAsync(
            Database,
            "TRUNCATE platform.audit_event;",
            "erp_app",
            "42501");
    }

    [Fact]
    public async Task AuditLedger_IsAppendOnlyEvenForASuperuser()
    {
        var tenant = await fixture.CreateTenantAsync("grant-append-only");
        var auditId = await Database.ScalarAsync<Guid>(
            $"SELECT id FROM platform.audit_event WHERE tenant_id = '{tenant.TenantId}' LIMIT 1",
            "postgres");
        Assert.NotEqual(Guid.Empty, auditId);

        var update = await PostgresAssert.RejectsAsync(
            Database,
            $"UPDATE platform.audit_event SET reason = 'rewritten' WHERE id = '{auditId}';",
            "postgres",
            "42501");
        Assert.Contains("append-only", update.Message, StringComparison.OrdinalIgnoreCase);

        var delete = await PostgresAssert.RejectsAsync(
            Database,
            $"DELETE FROM platform.audit_event WHERE id = '{auditId}';",
            "postgres",
            "42501");
        Assert.Contains("append-only", delete.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RuntimeRole_CannotReadSecurityEvents()
    {
        await Database.ExecuteAsync(
            $"""
            INSERT INTO platform.security_event (id, occurred_at, category, severity, outcome)
            VALUES ('{Guid.NewGuid()}', now(), 'authentication', 'info', 'success');
            """,
            "erp_app");

        await PostgresAssert.RejectsAsync(
            Database,
            "SELECT * FROM platform.security_event;",
            "erp_app",
            "42501");

        var visible = await Database.ScalarAsync<long>(
            "SELECT count(*) FROM platform.security_event;",
            "postgres");
        Assert.True(visible >= 1);
    }

    [Fact]
    public async Task RuntimeRole_CannotWriteTheMigrationJournal()
    {
        await PostgresAssert.RejectsAsync(
            Database,
            "UPDATE platform.schema_migration SET checksum = repeat('0', 64);",
            "erp_app",
            "42501");

        await PostgresAssert.RejectsAsync(
            Database,
            "INSERT INTO platform.schema_migration (script_name, checksum) VALUES ('x.sql', repeat('0', 64));",
            "erp_app",
            "42501");
    }

    [Fact]
    public async Task TenantEnumerationFunction_IsMigrationOnlyAndOwnedByTheBypassRole()
    {
        var owner = await Database.ScalarAsync<string>(
            "SELECT pg_get_userbyid(proowner) FROM pg_proc WHERE proname = 'tenant_ids';",
            "postgres");
        Assert.Equal("erp_platform_admin", owner);

        var securityDefiner = await Database.ScalarAsync<bool>(
            "SELECT prosecdef FROM pg_proc WHERE proname = 'tenant_ids';",
            "postgres");
        Assert.True(securityDefiner);

        var appCanExecute = await Database.ScalarAsync<bool>(
            "SELECT has_function_privilege('erp_app', 'platform.tenant_ids()', 'EXECUTE');",
            "postgres");
        Assert.False(appCanExecute);

        var ownerCanExecute = await Database.ScalarAsync<bool>(
            "SELECT has_function_privilege('erp_owner', 'platform.tenant_ids()', 'EXECUTE');",
            "postgres");
        Assert.True(ownerCanExecute);

        await PostgresAssert.RejectsAsync(
            Database,
            "SELECT * FROM platform.tenant_ids();",
            "erp_app",
            "42501");
    }

    [Fact]
    public async Task RuntimeRole_CannotModifyOrDropPolicies()
    {
        await PostgresAssert.RejectsAsync(
            Database,
            "ALTER TABLE tenancy.tenant DISABLE ROW LEVEL SECURITY;",
            "erp_app",
            "42501");

        await PostgresAssert.RejectsAsync(
            Database,
            "DROP POLICY tenant_read ON tenancy.tenant;",
            "erp_app",
            "42501");

        await PostgresAssert.RejectsAsync(
            Database,
            "DROP TABLE platform.audit_event;",
            "erp_app",
            "42501");
    }

    [Fact]
    public async Task MigrationCredential_StartsAsTheOwnerButIsNotASuperuser()
    {
        var row = await Database.QueryAsync(
            "SELECT session_user, current_user, (SELECT rolsuper FROM pg_roles WHERE rolname = current_user) FROM (SELECT 1) x;",
            "erp_migrator");

        Assert.Equal("erp_migrator", (string)row[0][0]!);
        Assert.Equal("erp_owner", (string)row[0][1]!);
        Assert.False((bool)row[0][2]!);
    }

    [Fact]
    public async Task PlatformAdminBypassesRls_ButTheRuntimeRoleNeverDoes()
    {
        var tenants = await fixture.CreateTenantAsync("grant-platform-bypass");
        await fixture.CreateTenantAsync("grant-platform-bypass-2");

        var appVisible = await Database.ScalarAsync<long>("SELECT count(*) FROM tenancy.tenant;", "erp_app");
        var adminVisible = await Database.ScalarAsync<long>("SELECT count(*) FROM tenancy.tenant;", "postgres");

        Assert.Equal(0, appVisible);
        Assert.True(adminVisible >= 2);
        Assert.NotEqual(Guid.Empty, tenants.TenantId);
    }
}
