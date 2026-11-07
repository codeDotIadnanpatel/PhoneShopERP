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
    public async Task PlatformAdminConnection_IsTheActualNonSuperuserBypassRole()
    {
        // The connection is opened as erp_platform_admin, not as a superuser that
        // later assumes the role: session_user is the role itself.
        var rows = await Database.QueryAsync(
            """
            SELECT session_user,
                   current_user,
                   (SELECT rolsuper FROM pg_roles WHERE rolname = current_user),
                   (SELECT rolbypassrls FROM pg_roles WHERE rolname = current_user),
                   (SELECT rolcanlogin FROM pg_roles WHERE rolname = current_user);
            """,
            "erp_platform_admin");

        var row = Assert.Single(rows);
        Assert.Equal("erp_platform_admin", (string)row[0]!);
        Assert.Equal("erp_platform_admin", (string)row[1]!);
        Assert.False((bool)row[2]!, "The platform-admin test subject must not be a superuser.");
        Assert.True((bool)row[3]!, "erp_platform_admin is the documented BYPASSRLS role.");
        Assert.True((bool)row[4]!);
    }

    [Fact]
    public async Task RoleMembershipGraph_ContainsOnlyTheDocumentedMigrationEdges()
    {
        // The complete ERP membership graph. Exactly two SET-only edges exist:
        //   erp_migrator -> erp_owner            (the migration credential)
        //   erp_owner    -> erp_platform_admin   (ownership-transfer capability)
        // INHERIT FALSE on both means neither role passively holds the other's
        // privileges; ADMIN FALSE means no role administers another. erp_app has
        // no memberships, so it cannot reach either role by INHERIT or SET ROLE.
        var edges = await Database.QueryAsync(
            """
            SELECT m.rolname AS granted,
                   r.rolname AS member,
                   am.admin_option,
                   am.inherit_option,
                   am.set_option
            FROM pg_auth_members am
            JOIN pg_roles m ON m.oid = am.roleid
            JOIN pg_roles r ON r.oid = am.member
            WHERE m.rolname IN ('erp_owner', 'erp_migrator', 'erp_app', 'erp_platform_admin')
               OR r.rolname IN ('erp_owner', 'erp_migrator', 'erp_app', 'erp_platform_admin')
            ORDER BY r.rolname, m.rolname;
            """,
            "postgres");

        Assert.Collection(
            edges,
            edge => AssertDocumentedEdge(edge, granted: "erp_owner", member: "erp_migrator"),
            edge => AssertDocumentedEdge(edge, granted: "erp_platform_admin", member: "erp_owner"));

        // Runtime semantics of the absence of an edge, with PostgreSQL's own
        // recursive membership test rather than a hand-rolled join.
        Assert.True(await Database.ScalarAsync<bool>(
            "SELECT NOT pg_has_role('erp_app', 'erp_platform_admin', 'MEMBER');",
            "postgres"));
        Assert.True(await Database.ScalarAsync<bool>(
            "SELECT NOT pg_has_role('erp_app', 'erp_owner', 'MEMBER');",
            "postgres"));

        static void AssertDocumentedEdge(object?[] edge, string granted, string member)
        {
            Assert.Equal(granted, (string)edge[0]!);
            Assert.Equal(member, (string)edge[1]!);
            Assert.False((bool)edge[2]!, "No ERP role may administer another.");
            Assert.False((bool)edge[3]!, "No ERP role may passively inherit another's privileges.");
            Assert.True((bool)edge[4]!, "The documented migration paths require SET.");
        }
    }

    [Fact]
    public async Task MigrationCredential_DoesNotPassivelyInheritThePlatformAdminRlsBypass()
    {
        await fixture.CreateTenantAsync("grant-owner-no-passive-bypass");

        // erp_migrator connects as erp_owner. forced RLS applies to the table
        // owner, and INHERIT FALSE on the platform-admin membership means the
        // owner credential does not passively bypass it.
        Assert.Equal(
            0L,
            await Database.ScalarAsync<long>("SELECT count(*) FROM tenancy.tenant;", "erp_migrator"));
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
    public async Task PlatformAdminSeesEveryTenant_WhileTheRuntimeRoleSeesNone()
    {
        var first = await fixture.CreateTenantAsync("grant-platform-bypass");
        var second = await fixture.CreateTenantAsync("grant-platform-bypass-2");

        // Runtime role without a tenant context: the pre-tenant read path requires
        // an identity, so RLS returns no tenant rows at all.
        Assert.Equal(0L, await Database.ScalarAsync<long>("SELECT count(*) FROM tenancy.tenant;", "erp_app"));

        // Real erp_platform_admin connection; its identity is proven in
        // PlatformAdminConnection_IsTheActualNonSuperuserBypassRole.
        var visible = (await Database.QueryAsync(
                "SELECT tenant_id FROM tenancy.tenant;",
                "erp_platform_admin"))
            .Select(row => (Guid)row[0]!)
            .ToHashSet();

        Assert.Contains(first.TenantId, visible);
        Assert.Contains(second.TenantId, visible);
        Assert.True(visible.Count >= 2, "BYPASSRLS must expose every tenant to the admin role.");
    }

    [Fact]
    public async Task PlatformAdminReadsAuditRowsAcrossTenants_WhileTheRuntimeRoleIsConfined()
    {
        var first = await fixture.CreateTenantAsync("grant-audit-first");
        var second = await fixture.CreateTenantAsync("grant-audit-second");

        // Same seeded records read by the platform-admin connection: no tenant
        // context is set, yet both tenants' audit rows are visible.
        var adminFirst = await Database.ScalarAsync<long>(
            $"SELECT count(*) FROM platform.audit_event WHERE tenant_id = '{first.TenantId}';",
            "erp_platform_admin");
        var adminSecond = await Database.ScalarAsync<long>(
            $"SELECT count(*) FROM platform.audit_event WHERE tenant_id = '{second.TenantId}';",
            "erp_platform_admin");

        Assert.True(adminFirst > 0);
        Assert.True(adminSecond > 0);

        // The runtime role, scoped to the first tenant, sees exactly that tenant's
        // rows and none of the second tenant's, for the same SQL.
        var appFirst = await ScalarInTenantContextAsync<long>(
            first.TenantId,
            $"SELECT count(*) FROM platform.audit_event WHERE tenant_id = '{first.TenantId}';");
        var appSecond = await ScalarInTenantContextAsync<long>(
            first.TenantId,
            $"SELECT count(*) FROM platform.audit_event WHERE tenant_id = '{second.TenantId}';");

        Assert.Equal(adminFirst, appFirst);
        Assert.Equal(0L, appSecond);
    }

    [Fact]
    public async Task PlatformAdminCanExecuteTheMigrationOnlyTenantEnumeration()
    {
        var tenant = await fixture.CreateTenantAsync("grant-tenant-ids");

        var ids = (await Database.QueryAsync("SELECT * FROM platform.tenant_ids();", "erp_platform_admin"))
            .Select(row => (Guid)row[0]!)
            .ToHashSet();

        Assert.Contains(tenant.TenantId, ids);
        Assert.Equal(
            await Database.ScalarAsync<long>("SELECT count(*) FROM tenancy.tenant;", "erp_platform_admin"),
            (long)ids.Count);

        // The enumeration is migration-only; the runtime role stays denied.
        await PostgresAssert.RejectsAsync(
            Database,
            "SELECT count(*) FROM platform.tenant_ids();",
            "erp_app",
            "42501");
    }

    [Fact]
    public async Task PlatformAdminHasNoTenantWriteOrDdlAuthority()
    {
        var tenant = await fixture.CreateTenantAsync("grant-admin-write");

        // BYPASSRLS is a read-visibility attribute, not write authority. The role
        // holds SELECT only outside the security-event table.
        await PostgresAssert.RejectsAsync(
            Database,
            $"UPDATE tenancy.tenant SET display_name = 'hijacked' WHERE tenant_id = '{tenant.TenantId}';",
            "erp_platform_admin",
            "42501");
        await PostgresAssert.RejectsAsync(
            Database,
            "DELETE FROM tenancy.tenant;",
            "erp_platform_admin",
            "42501");
        await PostgresAssert.RejectsAsync(
            Database,
            "UPDATE identity.user SET email = 'hijacked@example.test';",
            "erp_platform_admin",
            "42501");
        await PostgresAssert.RejectsAsync(
            Database,
            $"""
            INSERT INTO platform.audit_event (id, tenant_id, actor_type, action, outcome)
            VALUES ('{Guid.NewGuid()}', '{tenant.TenantId}', 'system', 't03', 'success');
            """,
            "erp_platform_admin",
            "42501");

        // No DDL either, despite the schema usage grants.
        await PostgresAssert.RejectsAsync(Database, "CREATE SCHEMA hostile;", "erp_platform_admin", "42501");

        Assert.Equal(
            "Tenant grant-admin-write",
            await Database.ScalarAsync<string>(
                $"SELECT display_name FROM tenancy.tenant WHERE tenant_id = '{tenant.TenantId}';",
                "erp_platform_admin"));
    }

    [Fact]
    public async Task PlatformAdminReadsSecurityEventsTheRuntimeRoleMayOnlyAppend()
    {
        var eventId = Guid.NewGuid();

        await Database.ExecuteAsync(
            $"""
            INSERT INTO platform.security_event (id, occurred_at, category, severity, outcome)
            VALUES ('{eventId}', now(), 'authentication', 'info', 'success');
            """,
            "erp_platform_admin");

        Assert.Equal(
            1L,
            await Database.ScalarAsync<long>(
                $"SELECT count(*) FROM platform.security_event WHERE id = '{eventId}';",
                "erp_platform_admin"));

        // Write-only for the runtime role: it may append but never read.
        await PostgresAssert.RejectsAsync(
            Database,
            $"SELECT count(*) FROM platform.security_event WHERE id = '{eventId}';",
            "erp_app",
            "42501");

        // Even the platform admin cannot rewrite the ledger: UPDATE is not granted,
        // and the append-only trigger is the defence-in-depth backstop.
        var rewrite = await PostgresAssert.RejectsAsync(
            Database,
            $"UPDATE platform.security_event SET outcome = 'denied' WHERE id = '{eventId}';",
            "erp_platform_admin",
            "42501");
        Assert.Contains("permission denied", rewrite.Message, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<T?> ScalarInTenantContextAsync<T>(Guid tenantId, string sql)
    {
        await using var connection = new NpgsqlConnection(Database.AppConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using (var set = new NpgsqlCommand("SELECT set_config('app.tenant_id', @id, true);", connection, transaction))
        {
            set.Parameters.AddWithValue("id", tenantId.ToString());
            await set.ExecuteScalarAsync();
        }

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        var result = await command.ExecuteScalarAsync();
        await transaction.RollbackAsync();

        return result is null or DBNull ? default : (T)result;
    }
}
