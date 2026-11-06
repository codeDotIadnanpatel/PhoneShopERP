using Npgsql;
using Xunit;

namespace PhoneShopErp.Database.Tests;

/// <summary>
/// Tenant-isolation verification against the real runtime role (<c>erp_app</c>)
/// with forced RLS. Every assertion that exercises tenancy runs as
/// <c>erp_app</c>; the superuser connection is used only to seed fixtures and to
/// prove that a denied mutation left the other tenant's row unchanged.
///
/// The three PostgreSQL outcomes are deliberately kept distinct:
///
///   * <b>Rejected</b> - SQLSTATE 42501. Either the RLS WITH CHECK expression
///     rejected a write ("row-level security policy") or the runtime role has no
///     table privilege at all ("permission denied").
///   * <b>Filtered</b> - a read returns zero rows because the USING expression
///     does not match; there is no error.
///   * <b>Affected zero rows</b> - an UPDATE/DELETE succeeds but matches no row.
///
/// The suite does not claim complete isolation for any path that is not listed in
/// <see cref="TenantOwnedTables"/>; <see cref="EveryTenantTable_IsInTheReviewedCoverageList"/>
/// makes an unreviewed tenant table fail the build.
/// </summary>
[Collection(PostgresDatabaseGroup.Name)]
public sealed class RlsVerificationTests
{
    /// <summary>
    /// Every table that carries a <c>tenant_id</c> column in the migrated schema.
    /// This is the reviewed coverage list, not a guess: the catalog test below
    /// fails when a migration adds a tenant table without adding it here.
    /// </summary>
    private static readonly string[] TenantOwnedTables =
    [
        "identity.device",
        "identity.membership",
        "identity.role",
        "identity.role_assignment",
        "identity.role_permission",
        "identity.session",
        "platform.audit_event",
        "platform.idempotency_record",
        "platform.outbox_message",
        "saas.billing_attempt",
        "saas.entitlement",
        "saas.subscription",
        "saas.usage_record",
        "tenancy.branch",
        "tenancy.legal_entity",
        "tenancy.register",
        "tenancy.stock_location",
        "tenancy.tenant",
    ];

    /// <summary>
    /// Every forced-RLS table in the managed schemas, including the four global
    /// identity tables that are identity-scoped rather than tenant-scoped.
    /// </summary>
    private static readonly string[] ForcedRlsTables =
    [
        "identity.device",
        "identity.membership",
        "identity.platform_grant",
        "identity.role",
        "identity.role_assignment",
        "identity.role_permission",
        "identity.session",
        "identity.user",
        "platform.audit_event",
        "platform.idempotency_record",
        "platform.onboarding_request",
        "platform.outbox_message",
        "platform.security_event",
        "saas.billing_attempt",
        "saas.entitlement",
        "saas.subscription",
        "saas.usage_record",
        "tenancy.branch",
        "tenancy.legal_entity",
        "tenancy.register",
        "tenancy.stock_location",
        "tenancy.tenant",
    ];

    /// <summary>
    /// Tenant tables whose read policy calls the throwing
    /// <c>platform.current_tenant_id()</c>. A missing context is an error, not an
    /// empty result.
    /// </summary>
    private static readonly string[] StrictContextTables =
    [
        "identity.device",
        "identity.role",
        "identity.role_assignment",
        "identity.role_permission",
        "platform.audit_event",
        "platform.idempotency_record",
        "platform.outbox_message",
        "saas.billing_attempt",
        "saas.entitlement",
        "saas.subscription",
        "saas.usage_record",
        "tenancy.branch",
        "tenancy.legal_entity",
        "tenancy.register",
        "tenancy.stock_location",
    ];

    /// <summary>
    /// Approved pre-tenant read paths use the non-throwing helpers, so a request
    /// without context sees no rows rather than raising from the policy.
    /// </summary>
    private static readonly string[] NonThrowingContextTables =
    [
        "identity.membership",
        "identity.session",
        "tenancy.tenant",
    ];

    /// <summary>
    /// Tables where <c>erp_app</c> holds UPDATE. The remaining tenant tables are
    /// protected at the privilege layer as well, which is asserted separately.
    /// </summary>
    private static readonly string[] UpdateGrantedTables =
    [
        "identity.device",
        "identity.membership",
        "identity.role",
        "identity.role_assignment",
        "identity.session",
        "platform.idempotency_record",
        "platform.outbox_message",
        "saas.billing_attempt",
        "saas.entitlement",
        "saas.subscription",
        "tenancy.branch",
        "tenancy.legal_entity",
        "tenancy.register",
        "tenancy.stock_location",
        "tenancy.tenant",
    ];

    /// <summary>Tables where <c>erp_app</c> holds DELETE.</summary>
    private static readonly string[] DeleteGrantedTables =
    [
        "identity.role_permission",
        "platform.idempotency_record",
    ];

    private readonly DatabaseFixture fixture;

    public RlsVerificationTests(DatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    private TestDatabase Database => fixture.Database;

    [Fact]
    public async Task EveryTenantTable_IsInTheReviewedCoverageList()
    {
        var tenantTables = await Database.QueryAsync(
            """
            SELECT n.nspname || '.' || c.relname
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname IN ('platform', 'tenancy', 'identity', 'saas')
              AND c.relkind = 'r'
              AND EXISTS (
                  SELECT 1
                  FROM pg_attribute a
                  WHERE a.attrelid = c.oid
                    AND a.attname = 'tenant_id'
                    AND a.attnum > 0
                    AND NOT a.attisdropped)
            ORDER BY 1;
            """,
            "postgres");

        Assert.Equal(
            Sorted(TenantOwnedTables),
            Sorted(Names(tenantTables)));

        var forcedTables = await Database.QueryAsync(
            """
            SELECT n.nspname || '.' || c.relname
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname IN ('platform', 'tenancy', 'identity', 'saas')
              AND c.relkind = 'r'
              AND c.relrowsecurity
              AND c.relforcerowsecurity
            ORDER BY 1;
            """,
            "postgres");

        Assert.Equal(
            Sorted(ForcedRlsTables),
            Sorted(Names(forcedTables)));

        // A tenant table could be added with RLS enabled but not forced, or with
        // a policy that ignores the trusted tenant context. Both defeat the
        // coverage claim even though the table would appear in the list above.
        foreach (var table in TenantOwnedTables)
        {
            Assert.Contains(table, Names(forcedTables), StringComparer.Ordinal);
        }

        var policies = await Database.QueryAsync(
            """
            SELECT n.nspname || '.' || c.relname
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname IN ('platform', 'tenancy', 'identity', 'saas')
              AND c.relkind = 'r'
              AND EXISTS (
                  SELECT 1
                  FROM pg_policy p
                  WHERE p.polrelid = c.oid
                    AND (
                        coalesce(pg_get_expr(p.polqual, p.polrelid), '') LIKE '%current_tenant_id%'
                        OR coalesce(pg_get_expr(p.polwithcheck, p.polrelid), '') LIKE '%current_tenant_id%'))
            ORDER BY 1;
            """,
            "postgres");

        Assert.Equal(
            Sorted(TenantOwnedTables),
            Sorted(Names(policies)));
    }

    [Fact]
    public async Task CrossTenantRead_IsFilteredToZeroRowsOnEveryTenantTable()
    {
        var a = await SeedTenantAsync("read-a");
        var b = await SeedTenantAsync("read-b");

        foreach (var table in TenantOwnedTables)
        {
            var own = await ScalarAsAsync<long>(
                a.TenantId.ToString(),
                a.UserId.ToString(),
                subject: null,
                sessionReference: null,
                $"SELECT count(*) FROM {table} WHERE tenant_id = '{a.TenantId}';");

            Assert.True(
                own >= 1,
                $"{table}: tenant A cannot see its own seeded row, so the cross-tenant assertion would be vacuous.");

            var other = await ScalarAsAsync<long>(
                a.TenantId.ToString(),
                a.UserId.ToString(),
                subject: null,
                sessionReference: null,
                $"SELECT count(*) FROM {table} WHERE tenant_id = '{b.TenantId}';");

            Assert.Equal(0, other);
        }
    }

    [Fact]
    public async Task CrossTenantInsert_IsRejectedByTheWithCheckPolicyOnEveryTenantTable()
    {
        var a = await SeedTenantAsync("insert-a");
        var b = await SeedTenantAsync("insert-b");

        foreach (var table in TenantOwnedTables)
        {
            var sql = CrossTenantInsertSql(table, a, b);

            await AssertRlsRejectedAsync(
                $"INSERT INTO {table} under tenant B while tenant A is in context",
                () => ExecuteAsAsync(
                    a.TenantId.ToString(),
                    a.UserId.ToString(),
                    subject: null,
                    sessionReference: null,
                    sql));
        }
    }

    [Fact]
    public async Task CrossTenantUpdate_AffectsZeroRowsWhereUpdateIsGranted()
    {
        var a = await SeedTenantAsync("update-a");
        var b = await SeedTenantAsync("update-b");

        foreach (var table in UpdateGrantedTables)
        {
            var before = await Database.ScalarAsync<int>(
                $"SELECT row_version FROM {table} WHERE tenant_id = '{b.TenantId}';",
                "postgres");

            var affected = await ExecuteAsAsync(
                a.TenantId.ToString(),
                a.UserId.ToString(),
                subject: null,
                sessionReference: null,
                CrossTenantUpdateSql(table, b.TenantId));

            Assert.Equal(0, affected);

            var after = await Database.ScalarAsync<int>(
                $"SELECT row_version FROM {table} WHERE tenant_id = '{b.TenantId}';",
                "postgres");

            Assert.Equal(before, after);
        }
    }

    [Fact]
    public async Task CrossTenantUpdate_IsRejectedAtThePrivilegeLayerWhereNoGrantExists()
    {
        var a = await SeedTenantAsync("update-denied-a");
        var b = await SeedTenantAsync("update-denied-b");

        var withoutUpdateGrant = TenantOwnedTables
            .Except(UpdateGrantedTables, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            Sorted(["identity.role_permission", "platform.audit_event", "saas.usage_record"]),
            Sorted(withoutUpdateGrant));

        foreach (var table in withoutUpdateGrant)
        {
            var before = await CountAsync(table, b.TenantId);

            await AssertPrivilegeRejectedAsync(
                $"UPDATE {table} without an erp_app grant",
                () => ExecuteAsAsync(
                    a.TenantId.ToString(),
                    a.UserId.ToString(),
                    subject: null,
                    sessionReference: null,
                    CrossTenantUpdateSql(table, b.TenantId)));

            Assert.Equal(before, await CountAsync(table, b.TenantId));
        }
    }

    [Fact]
    public async Task CrossTenantDelete_AffectsZeroRowsWhereDeleteIsGranted()
    {
        var a = await SeedTenantAsync("delete-a");
        var b = await SeedTenantAsync("delete-b");

        foreach (var table in DeleteGrantedTables)
        {
            var before = await CountAsync(table, b.TenantId);
            Assert.True(before >= 1, $"{table}: tenant B was not seeded.");

            var affected = await ExecuteAsAsync(
                a.TenantId.ToString(),
                a.UserId.ToString(),
                subject: null,
                sessionReference: null,
                $"DELETE FROM {table} WHERE tenant_id = '{b.TenantId}';");

            Assert.Equal(0, affected);
            Assert.Equal(before, await CountAsync(table, b.TenantId));
        }
    }

    [Fact]
    public async Task CrossTenantDelete_IsRejectedAtThePrivilegeLayerWhereNoGrantExists()
    {
        var a = await SeedTenantAsync("delete-denied-a");
        var b = await SeedTenantAsync("delete-denied-b");

        var withoutDeleteGrant = TenantOwnedTables
            .Except(DeleteGrantedTables, StringComparer.Ordinal)
            .ToArray();

        foreach (var table in withoutDeleteGrant)
        {
            var before = await CountAsync(table, b.TenantId);

            await AssertPrivilegeRejectedAsync(
                $"DELETE FROM {table} without an erp_app grant",
                () => ExecuteAsAsync(
                    a.TenantId.ToString(),
                    a.UserId.ToString(),
                    subject: null,
                    sessionReference: null,
                    $"DELETE FROM {table} WHERE tenant_id = '{b.TenantId}';"));

            Assert.Equal(before, await CountAsync(table, b.TenantId));
        }
    }

    /// <summary>
    /// The cross-tenant UPDATE/DELETE assertions above run as <c>erp_app</c>, so
    /// on the three tables where that role holds no UPDATE grant the statement
    /// is rejected by the privilege check before the policy is ever evaluated.
    /// A missing or wrong policy could therefore go unnoticed.
    ///
    /// <c>erp_migrator</c> connects with <c>ALTER ROLE ... SET role = erp_owner</c>.
    /// <c>erp_owner</c> owns every managed table but is NOBYPASSRLS and every
    /// table is FORCE RLS, so running the same statements as the owner exercises
    /// the policy itself for all 18 tenant tables without adding a single grant.
    /// </summary>
    [Fact]
    public async Task CrossTenantMutation_IsFilteredByRlsEvenWhereTheRuntimeRoleHasNoGrant()
    {
        var a = await SeedTenantAsync("owner-a");
        var b = await SeedTenantAsync("owner-b");

        await using var connection = new NpgsqlConnection(Database.ConnectionStringFor("erp_migrator"));
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using (var who = new NpgsqlCommand(
            "SELECT current_user, (SELECT rolbypassrls FROM pg_roles WHERE rolname = current_user);",
            connection,
            transaction))
        await using (var reader = await who.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync());
            Assert.Equal("erp_owner", reader.GetString(0));
            Assert.False(reader.GetBoolean(1), "The mutation probe must run under forced RLS.");
        }

        await ApplyContextAsync(
            connection,
            transaction,
            a.TenantId.ToString(),
            a.UserId.ToString(),
            subject: null,
            sessionReference: null);

        try
        {
            foreach (var table in TenantOwnedTables)
            {
                var before = await CountAsync(table, b.TenantId);
                Assert.True(before >= 1, $"{table}: tenant B was not seeded.");

                await using (var update = new NpgsqlCommand(
                    CrossTenantUpdateSql(table, b.TenantId),
                    connection,
                    transaction))
                {
                    Assert.Equal(0, await update.ExecuteNonQueryAsync());
                }

                Assert.Equal(before, await CountAsync(table, b.TenantId));

                await using (var delete = new NpgsqlCommand(
                    $"DELETE FROM {table} WHERE tenant_id = '{b.TenantId}';",
                    connection,
                    transaction))
                {
                    Assert.Equal(0, await delete.ExecuteNonQueryAsync());
                }

                Assert.Equal(before, await CountAsync(table, b.TenantId));
            }
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }

    /// <summary>
    /// The audit ledger documents "structured, bounded fields". These assertions
    /// prove the bounds are enforced by the schema and not only by application
    /// code, and that a value at the documented limit is still accepted.
    /// </summary>
    [Fact]
    public async Task AuditAndSecurityEventFields_RejectOversizedValues()
    {
        var tenant = await SeedTenantAsync("bounds");

        await Database.ExecuteAsync(
            $"""
            INSERT INTO platform.security_event (
                id, category, severity, outcome, actor_reference, detail)
            VALUES (
                '{Guid.NewGuid()}', 'rls_bounds', 'info', 'success', repeat('a', 200),
                jsonb_build_object('blob', repeat('b', 8000)));
            """,
            "erp_app");

        await Database.ExecuteAsync(
            $"""
            INSERT INTO platform.audit_event (
                id, tenant_id, actor_type, action, outcome, reason, detail)
            VALUES (
                '{Guid.NewGuid()}', '{tenant.TenantId}', 'system', 'rls.bounds', 'success',
                repeat('c', 1000), jsonb_build_object('blob', repeat('d', 8000)));
            """,
            "postgres");

        await AssertCheckRejectedAsync(
            "platform.security_event: 201-character actor_reference",
            () => Database.ExecuteAsync(
                $"""
                INSERT INTO platform.security_event (
                    id, category, severity, outcome, actor_reference)
                VALUES (
                    '{Guid.NewGuid()}', 'rls_bounds', 'info', 'success', repeat('a', 201));
                """,
                "erp_app"));

        await AssertCheckRejectedAsync(
            "platform.security_event: detail larger than 8 KiB",
            () => Database.ExecuteAsync(
                $"""
                INSERT INTO platform.security_event (
                    id, category, severity, outcome, detail)
                VALUES (
                    '{Guid.NewGuid()}', 'rls_bounds', 'info', 'success',
                    jsonb_build_object('blob', repeat('b', 9000)));
                """,
                "erp_app"));

        await AssertCheckRejectedAsync(
            "platform.audit_event: 1001-character reason",
            () => ExecuteAsAsync(
                tenant.TenantId.ToString(),
                tenant.UserId.ToString(),
                subject: null,
                sessionReference: null,
                $"""
                INSERT INTO platform.audit_event (
                    id, tenant_id, actor_type, action, outcome, reason)
                VALUES (
                    '{Guid.NewGuid()}', '{tenant.TenantId}', 'system', 'rls.bounds',
                    'success', repeat('c', 1001));
                """));

        await AssertCheckRejectedAsync(
            "platform.audit_event: detail larger than 8 KiB",
            () => ExecuteAsAsync(
                tenant.TenantId.ToString(),
                tenant.UserId.ToString(),
                subject: null,
                sessionReference: null,
                $"""
                INSERT INTO platform.audit_event (
                    id, tenant_id, actor_type, action, outcome, detail)
                VALUES (
                    '{Guid.NewGuid()}', '{tenant.TenantId}', 'system', 'rls.bounds',
                    'success', jsonb_build_object('blob', repeat('d', 9000)));
                """));
    }

    [Fact]
    public async Task MissingMalformedOrNilTenantContext_FailsClosed()
    {
        var tenant = await SeedTenantAsync("context");

        foreach (var table in StrictContextTables)
        {
            await AssertContextRejectedAsync(
                $"{table}: SELECT without tenant context",
                () => ScalarAsAsync<long>(
                    tenantId: null,
                    userId: null,
                    subject: null,
                    sessionReference: null,
                    $"SELECT count(*) FROM {table};"),
                "Tenant context is not set");
        }

        foreach (var table in NonThrowingContextTables)
        {
            var visible = await ScalarAsAsync<long>(
                tenantId: null,
                userId: null,
                subject: null,
                sessionReference: null,
                $"SELECT count(*) FROM {table};");

            Assert.Equal(0, visible);
        }

        await AssertContextRejectedAsync(
            "platform.audit_event: INSERT without tenant context",
            () => ExecuteAsAsync(
                tenantId: null,
                userId: null,
                subject: null,
                sessionReference: null,
                $"""
                INSERT INTO platform.audit_event (id, tenant_id, actor_type, action, outcome)
                VALUES ('{Guid.NewGuid()}', '{tenant.TenantId}', 'system', 'rls.probe', 'success');
                """),
            "Tenant context is not set");

        await AssertContextRejectedAsync(
            "tenancy.branch: INSERT without tenant context",
            () => ExecuteAsAsync(
                tenantId: null,
                userId: null,
                subject: null,
                sessionReference: null,
                $"""
                INSERT INTO tenancy.branch (
                    id, tenant_id, legal_entity_id, code, display_name, timezone, locale, status)
                VALUES (
                    '{Guid.NewGuid()}', '{tenant.TenantId}', '{tenant.LegalEntityId}',
                    'CTX', 'Context probe', 'Europe/London', 'en-GB', 'active');
                """),
            "Tenant context is not set");

        await AssertContextRejectedAsync(
            "tenancy.branch: SELECT with a malformed tenant context",
            () => ScalarAsAsync<long>(
                "not-a-uuid",
                tenant.UserId.ToString(),
                subject: null,
                sessionReference: null,
                "SELECT count(*) FROM tenancy.branch;"),
            "Tenant context is not a valid UUID");

        await AssertContextRejectedAsync(
            "tenancy.branch: SELECT with the nil tenant context",
            () => ScalarAsAsync<long>(
                Guid.Empty.ToString(),
                tenant.UserId.ToString(),
                subject: null,
                sessionReference: null,
                "SELECT count(*) FROM tenancy.branch;"),
            "Tenant context must not be the nil UUID");
    }

    [Fact]
    public async Task PooledConnectionReuse_DoesNotLeakTenantOrUserContext()
    {
        var tenant = await SeedTenantAsync("pool");

        await using var pool = new NpgsqlDataSourceBuilder(
            Database.PooledConnectionStringFor("erp_app", maxPoolSize: 1)).Build();

        await using (var first = await pool.OpenConnectionAsync())
        {
            await using var transaction = await first.BeginTransactionAsync();

            await SetAsync(first, transaction, "app.tenant_id", tenant.TenantId.ToString());
            await SetAsync(first, transaction, "app.user_id", tenant.UserId.ToString());

            await using var visible = new NpgsqlCommand(
                "SELECT count(*) FROM platform.audit_event;",
                first,
                transaction);
            Assert.True((long)(await visible.ExecuteScalarAsync())! >= 1);

            // Abort the transaction with a cross-tenant write. The context must
            // not survive the rollback when the connection is returned to the pool.
            await using var attack = new NpgsqlCommand(
                $"""
                INSERT INTO platform.audit_event (id, tenant_id, actor_type, action, outcome)
                VALUES ('{Guid.NewGuid()}', '{Guid.NewGuid()}', 'system', 'rls.probe', 'success');
                """,
                first,
                transaction);

            var rejection = await Assert.ThrowsAsync<PostgresException>(
                () => attack.ExecuteNonQueryAsync());
            Assert.Equal("42501", rejection.SqlState);
            Assert.Contains(
                "row-level security policy",
                rejection.MessageText,
                StringComparison.Ordinal);

            await transaction.RollbackAsync();
        }

        await using (var second = await pool.OpenConnectionAsync())
        {
            await using var context = new NpgsqlCommand(
                """
                SELECT coalesce(current_setting('app.tenant_id', true), '')
                    || '|' || coalesce(current_setting('app.user_id', true), '');
                """,
                second);
            Assert.Equal("|", (string)(await context.ExecuteScalarAsync())!);

            await AssertContextRejectedAsync(
                "pooled reuse: audit read",
                () => ScalarOnConnectionAsync<long>(second, "SELECT count(*) FROM platform.audit_event;"),
                "Tenant context is not set");

            var pickerRows = await ScalarOnConnectionAsync<long>(
                second,
                "SELECT count(*) FROM tenancy.tenant;");
            Assert.Equal(0, pickerRows);
        }

        // Even a caller that wrongly uses a session-level set_config cannot leak
        // context into the next logical request: the pool resets the session.
        await using (var third = await pool.OpenConnectionAsync())
        {
            await using var sessionSet = new NpgsqlCommand(
                $"SELECT set_config('app.tenant_id', '{tenant.TenantId}', false);",
                third);
            _ = await sessionSet.ExecuteScalarAsync();
        }

        await using (var fourth = await pool.OpenConnectionAsync())
        {
            await using var leaked = new NpgsqlCommand(
                "SELECT coalesce(current_setting('app.tenant_id', true), '');",
                fourth);
            Assert.Equal(string.Empty, (string)(await leaked.ExecuteScalarAsync())!);
        }
    }

    [Fact]
    public async Task PreTenantIdentityPaths_AreRestrictedToTheCallersOwnRows()
    {
        var a = await SeedTenantAsync("pre-a");
        var b = await SeedTenantAsync("pre-b");

        // Global user bootstrap read: by validated subject, or by the internal
        // user id already bound to the request.
        Assert.Equal(1, await ScalarAsAsync<long>(
            null, null, a.Subject, null,
            $"SELECT count(*) FROM identity.user WHERE id = '{a.UserId}';"));
        Assert.Equal(0, await ScalarAsAsync<long>(
            null, null, a.Subject, null,
            $"SELECT count(*) FROM identity.user WHERE id = '{b.UserId}';"));
        Assert.Equal(1, await ScalarAsAsync<long>(
            null, a.UserId.ToString(), null, null,
            $"SELECT count(*) FROM identity.user WHERE id = '{a.UserId}';"));
        Assert.Equal(0, await ScalarAsAsync<long>(
            null, a.UserId.ToString(), null, null,
            $"SELECT count(*) FROM identity.user WHERE id = '{b.UserId}';"));
        Assert.Equal(0, await ScalarAsAsync<long>(
            null, null, null, null,
            "SELECT count(*) FROM identity.user;"));

        // Pre-tenant membership picker: the caller's own memberships only.
        Assert.Equal(1, await ScalarAsAsync<long>(
            null, a.UserId.ToString(), a.Subject, null,
            "SELECT count(*) FROM identity.membership;"));
        Assert.Equal(0, await ScalarAsAsync<long>(
            null, a.UserId.ToString(), a.Subject, null,
            $"SELECT count(*) FROM identity.membership WHERE user_id = '{b.UserId}';"));

        // Pre-tenant session lookup requires both the provider reference and the
        // resolved user id to match the same row.
        Assert.Equal(1, await ScalarAsAsync<long>(
            null, a.UserId.ToString(), null, a.SessionReference,
            "SELECT count(*) FROM identity.session;"));
        Assert.Equal(0, await ScalarAsAsync<long>(
            null, a.UserId.ToString(), null, b.SessionReference,
            $"SELECT count(*) FROM identity.session WHERE tenant_id = '{b.TenantId}';"));
        Assert.Equal(0, await ScalarAsAsync<long>(
            null, a.UserId.ToString(), null, null,
            "SELECT count(*) FROM identity.session;"));
        Assert.Equal(0, await ScalarAsAsync<long>(
            null, null, null, a.SessionReference,
            "SELECT count(*) FROM identity.session;"));

        // A user row may only be created for the authenticated subject. The
        // runtime role holds no UPDATE privilege at all (Stage 5 T-01), so an
        // update attempt is rejected at the privilege layer before RLS runs.
        var otherUserName = await Database.ScalarAsync<string>(
            $"SELECT display_name FROM identity.user WHERE id = '{b.UserId}';",
            "postgres");

        await AssertPrivilegeRejectedAsync(
            "identity.user: UPDATE",
            () => ExecuteAsAsync(
                null, null, a.Subject, null,
                $"UPDATE identity.user SET display_name = 'hijacked' WHERE id = '{b.UserId}';"));
        Assert.Equal(
            otherUserName,
            await Database.ScalarAsync<string>(
                $"SELECT display_name FROM identity.user WHERE id = '{b.UserId}';",
                "postgres"));

        await AssertRlsRejectedAsync(
            "identity.user: INSERT under another subject",
            () => ExecuteAsAsync(
                null, null, a.Subject, null,
                $"""
                INSERT INTO identity.user (id, subject, display_name, status)
                VALUES ('{Guid.NewGuid()}', '{b.Subject}', 'RLS probe', 'active');
                """));

        // Platform grants are self-read only.
        Assert.Equal(1, await ScalarAsAsync<long>(
            null, a.UserId.ToString(), null, null,
            "SELECT count(*) FROM identity.platform_grant;"));
        Assert.Equal(0, await ScalarAsAsync<long>(
            null, a.UserId.ToString(), null, null,
            $"SELECT count(*) FROM identity.platform_grant WHERE user_id = '{b.UserId}';"));
        Assert.Equal(0, await ScalarAsAsync<long>(
            null, null, null, null,
            "SELECT count(*) FROM identity.platform_grant;"));

        // Onboarding idempotency is owner-scoped for reads and writes.
        Assert.Equal(1, await ScalarAsAsync<long>(
            null, a.UserId.ToString(), null, null,
            "SELECT count(*) FROM platform.onboarding_request;"));
        Assert.Equal(0, await ScalarAsAsync<long>(
            null, a.UserId.ToString(), null, null,
            $"SELECT count(*) FROM platform.onboarding_request WHERE requested_by_user_id = '{b.UserId}';"));
        Assert.Equal(0, await ScalarAsAsync<long>(
            null, null, null, null,
            "SELECT count(*) FROM platform.onboarding_request;"));

        var onboardingUpdated = await ExecuteAsAsync(
            null, a.UserId.ToString(), null, null,
            $"UPDATE platform.onboarding_request SET status = 'completed' WHERE requested_by_user_id = '{b.UserId}';");
        Assert.Equal(0, onboardingUpdated);

        var onboardingDeleted = await ExecuteAsAsync(
            null, a.UserId.ToString(), null, null,
            $"DELETE FROM platform.onboarding_request WHERE requested_by_user_id = '{b.UserId}';");
        Assert.Equal(0, onboardingDeleted);

        Assert.Equal(1, await Database.ScalarAsync<long>(
            $"SELECT count(*) FROM platform.onboarding_request WHERE requested_by_user_id = '{b.UserId}';",
            "postgres"));

        // The pre-tenant tenant picker is bounded by non-removed membership.
        var picker = await ScalarAsAsync<string>(
            null, a.UserId.ToString(), a.Subject, null,
            "SELECT string_agg(id::text, ',' ORDER BY id) FROM tenancy.tenant;");
        Assert.Equal(a.TenantId.ToString(), picker);
        Assert.DoesNotContain(b.TenantId.ToString(), picker!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SecurityEvents_AreAppendOnlyAndUnreadableByTheRuntimeRole()
    {
        var id = Guid.NewGuid();

        await Database.ExecuteAsync(
            $"""
            INSERT INTO platform.security_event (id, category, severity, outcome)
            VALUES ('{id}', 'rls_probe', 'info', 'success');
            """,
            "erp_app");

        await AssertPrivilegeRejectedAsync(
            "SELECT platform.security_event",
            () => Database.ScalarAsync<long>(
                "SELECT count(*) FROM platform.security_event;",
                "erp_app"));

        await AssertPrivilegeRejectedAsync(
            "UPDATE platform.security_event",
            () => Database.ExecuteAsync(
                $"UPDATE platform.security_event SET severity = 'high' WHERE id = '{id}';",
                "erp_app"));

        await AssertPrivilegeRejectedAsync(
            "DELETE FROM platform.security_event",
            () => Database.ExecuteAsync(
                $"DELETE FROM platform.security_event WHERE id = '{id}';",
                "erp_app"));

        var severity = await Database.ScalarAsync<string>(
            $"SELECT severity FROM platform.security_event WHERE id = '{id}';",
            "postgres");

        Assert.Equal("info", severity);
    }

    private async Task<RlsTenant> SeedTenantAsync(string label)
    {
        var subject = $"subject-rls-{label}-{Guid.NewGuid():N}";
        var created = await fixture.CreateTenantAsync("rls-" + label, subject);

        var roleId = await Database.ScalarAsync<Guid>(
            $"SELECT id FROM identity.role WHERE tenant_id = '{created.TenantId}' AND code = 'owner';",
            "postgres");

        var ids = new RlsTenant(
            created.TenantId,
            created.UserId,
            created.MembershipId,
            roleId,
            SessionId: Guid.NewGuid(),
            DeviceId: Guid.NewGuid(),
            IdempotencyId: Guid.NewGuid(),
            OutboxId: Guid.NewGuid(),
            SubscriptionId: Guid.NewGuid(),
            EntitlementId: Guid.NewGuid(),
            UsageId: Guid.NewGuid(),
            BillingId: Guid.NewGuid(),
            LegalEntityId: Guid.NewGuid(),
            BranchId: Guid.NewGuid(),
            StockLocationId: Guid.NewGuid(),
            RegisterId: Guid.NewGuid(),
            Subject: subject,
            SessionReference: $"ref-{label}-{Guid.NewGuid():N}",
            IdempotencyKey: $"rls-key-{Guid.NewGuid():N}");

        const string hash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        await Database.ExecuteAsync(
            $"""
            INSERT INTO saas.plan (code, display_name)
            VALUES ('rls_verify_plan', 'RLS verification plan')
            ON CONFLICT (code) DO NOTHING;

            INSERT INTO identity.session (
                id, tenant_id, user_id, membership_id, permission_version,
                provider_session_reference, fingerprint_hash, issued_at, last_seen_at,
                idle_expires_at, absolute_expires_at)
            VALUES (
                '{ids.SessionId}', '{ids.TenantId}', '{ids.UserId}', '{ids.MembershipId}', 1,
                '{ids.SessionReference}', '{hash}',
                now() - interval '1 minute', now() - interval '1 minute',
                now() + interval '30 minutes', now() + interval '1 hour');

            INSERT INTO identity.device (
                id, tenant_id, user_id, display_name, platform, status)
            VALUES (
                '{ids.DeviceId}', '{ids.TenantId}', '{ids.UserId}',
                'RLS device', 'windows', 'active');

            INSERT INTO platform.idempotency_record (
                id, tenant_id, scope, idempotency_key, request_hash, status, expires_at)
            VALUES (
                '{ids.IdempotencyId}', '{ids.TenantId}', 'rls.verify',
                '{ids.IdempotencyKey}', '{hash}', 'in_progress', now() + interval '1 hour');

            INSERT INTO platform.outbox_message (id, tenant_id, event_type, payload)
            VALUES (
                '{ids.OutboxId}', '{ids.TenantId}', 'rls.verify',
                jsonb_build_object('probe', true));

            INSERT INTO saas.subscription (id, tenant_id, plan_code, status)
            VALUES ('{ids.SubscriptionId}', '{ids.TenantId}', 'rls_verify_plan', 'active');

            INSERT INTO saas.entitlement (
                id, tenant_id, feature_code, is_enabled, source_subscription_id)
            VALUES (
                '{ids.EntitlementId}', '{ids.TenantId}', 'rls_feature', true,
                '{ids.SubscriptionId}');

            INSERT INTO saas.usage_record (id, tenant_id, feature_code, quantity)
            VALUES ('{ids.UsageId}', '{ids.TenantId}', 'rls_feature', 1);

            INSERT INTO saas.billing_attempt (id, tenant_id, status)
            VALUES ('{ids.BillingId}', '{ids.TenantId}', 'pending');

            INSERT INTO tenancy.legal_entity (
                id, tenant_id, code, display_name, country_code, status)
            VALUES (
                '{ids.LegalEntityId}', '{ids.TenantId}', 'RLS',
                'RLS legal entity', 'GB', 'active');

            INSERT INTO tenancy.branch (
                id, tenant_id, legal_entity_id, code, display_name, timezone, locale, status)
            VALUES (
                '{ids.BranchId}', '{ids.TenantId}', '{ids.LegalEntityId}', 'RLS',
                'RLS branch', 'Europe/London', 'en-GB', 'active');

            INSERT INTO tenancy.stock_location (
                id, tenant_id, branch_id, code, display_name, location_type, status)
            VALUES (
                '{ids.StockLocationId}', '{ids.TenantId}', '{ids.BranchId}', 'RLS',
                'RLS location', 'store', 'active');

            INSERT INTO tenancy.register (
                id, tenant_id, branch_id, code, display_name, status)
            VALUES (
                '{ids.RegisterId}', '{ids.TenantId}', '{ids.BranchId}', 'RLS',
                'RLS register', 'active');

            INSERT INTO identity.platform_grant (
                id, user_id, permission_code, is_active, permission_version)
            VALUES (
                '{Guid.NewGuid()}', '{ids.UserId}', 'security_event.view', true, 1);
            """,
            "postgres");

        return ids;
    }

    private async Task<long> CountAsync(string table, Guid tenantId) =>
        await Database.ScalarAsync<long>(
            $"SELECT count(*) FROM {table} WHERE tenant_id = '{tenantId}';",
            "postgres");

    private static string CrossTenantInsertSql(string table, RlsTenant a, RlsTenant b)
    {
        var otherTenant = Guid.NewGuid();

        return table switch
        {
            "identity.device" =>
                $"""
                INSERT INTO identity.device (id, tenant_id, user_id, display_name, platform, status)
                VALUES ('{Guid.NewGuid()}', '{b.TenantId}', '{b.UserId}', 'RLS probe', 'windows', 'active');
                """,
            "identity.membership" =>
                $"""
                INSERT INTO identity.membership (id, tenant_id, user_id, status)
                VALUES ('{Guid.NewGuid()}', '{b.TenantId}', '{a.UserId}', 'active');
                """,
            "identity.role" =>
                $"""
                INSERT INTO identity.role (id, tenant_id, code, display_name)
                VALUES ('{Guid.NewGuid()}', '{b.TenantId}', 'rlsx', 'RLS probe role');
                """,
            "identity.role_assignment" =>
                $"""
                INSERT INTO identity.role_assignment (
                    id, tenant_id, membership_id, role_id, scope_type, scope_id)
                VALUES (
                    '{Guid.NewGuid()}', '{b.TenantId}', '{b.MembershipId}', '{b.RoleId}',
                    'branch', '{b.BranchId}');
                """,
            "identity.role_permission" =>
                $"""
                INSERT INTO identity.role_permission (tenant_id, role_id, permission_code)
                VALUES ('{b.TenantId}', '{b.RoleId}', 'tenant.create');
                """,
            "identity.session" =>
                $"""
                INSERT INTO identity.session (
                    id, tenant_id, user_id, membership_id, permission_version,
                    provider_session_reference, fingerprint_hash, issued_at, last_seen_at,
                    idle_expires_at, absolute_expires_at)
                VALUES (
                    '{Guid.NewGuid()}', '{b.TenantId}', '{b.UserId}', '{b.MembershipId}', 1,
                    'ref-rls-probe-{Guid.NewGuid():N}',
                    'cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc',
                    now() - interval '1 minute', now() - interval '1 minute',
                    now() + interval '30 minutes', now() + interval '1 hour');
                """,
            "platform.audit_event" =>
                $"""
                INSERT INTO platform.audit_event (id, tenant_id, actor_type, action, outcome)
                VALUES ('{Guid.NewGuid()}', '{b.TenantId}', 'system', 'rls.probe', 'success');
                """,
            "platform.idempotency_record" =>
                $"""
                INSERT INTO platform.idempotency_record (
                    id, tenant_id, scope, idempotency_key, request_hash, status, expires_at)
                VALUES (
                    '{Guid.NewGuid()}', '{b.TenantId}', 'rls.probe',
                    'rls-probe-{Guid.NewGuid():N}',
                    'dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd',
                    'in_progress', now() + interval '1 hour');
                """,
            "platform.outbox_message" =>
                $"""
                INSERT INTO platform.outbox_message (id, tenant_id, event_type, payload)
                VALUES (
                    '{Guid.NewGuid()}', '{b.TenantId}', 'rls.probe',
                    jsonb_build_object('probe', true));
                """,
            "saas.billing_attempt" =>
                $"""
                INSERT INTO saas.billing_attempt (id, tenant_id, status)
                VALUES ('{Guid.NewGuid()}', '{b.TenantId}', 'pending');
                """,
            "saas.entitlement" =>
                $"""
                INSERT INTO saas.entitlement (id, tenant_id, feature_code, is_enabled)
                VALUES ('{Guid.NewGuid()}', '{b.TenantId}', 'rls_probe_feature', true);
                """,
            "saas.subscription" =>
                $"""
                INSERT INTO saas.subscription (id, tenant_id, plan_code, status)
                VALUES ('{Guid.NewGuid()}', '{b.TenantId}', 'rls_verify_plan', 'cancelled');
                """,
            "saas.usage_record" =>
                $"""
                INSERT INTO saas.usage_record (id, tenant_id, feature_code, quantity)
                VALUES ('{Guid.NewGuid()}', '{b.TenantId}', 'rls_probe', 1);
                """,
            "tenancy.branch" =>
                $"""
                INSERT INTO tenancy.branch (
                    id, tenant_id, legal_entity_id, code, display_name, timezone, locale, status)
                VALUES (
                    '{Guid.NewGuid()}', '{b.TenantId}', '{b.LegalEntityId}', 'RLSX',
                    'RLS probe branch', 'Europe/London', 'en-GB', 'active');
                """,
            "tenancy.legal_entity" =>
                $"""
                INSERT INTO tenancy.legal_entity (
                    id, tenant_id, code, display_name, country_code, status)
                VALUES (
                    '{Guid.NewGuid()}', '{b.TenantId}', 'RLSX',
                    'RLS probe entity', 'GB', 'active');
                """,
            "tenancy.register" =>
                $"""
                INSERT INTO tenancy.register (
                    id, tenant_id, branch_id, code, display_name, status)
                VALUES (
                    '{Guid.NewGuid()}', '{b.TenantId}', '{b.BranchId}', 'RLSX',
                    'RLS probe register', 'active');
                """,
            "tenancy.stock_location" =>
                $"""
                INSERT INTO tenancy.stock_location (
                    id, tenant_id, branch_id, code, display_name, location_type, status)
                VALUES (
                    '{Guid.NewGuid()}', '{b.TenantId}', '{b.BranchId}', 'RLSX',
                    'RLS probe location', 'store', 'active');
                """,
            // The tenant root ties id to tenant_id. A duplicate of tenant B would
            // be rejected by the primary key even if RLS failed, so use a fresh
            // third tenant id to isolate the WITH CHECK behaviour.
            "tenancy.tenant" =>
                $"""
                INSERT INTO tenancy.tenant (
                    id, tenant_id, slug, display_name, status, default_locale, default_timezone)
                VALUES (
                    '{otherTenant}', '{otherTenant}', 'rls-probe',
                    'RLS probe tenant', 'active', 'en-GB', 'Europe/London');
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(table), table, "Not a reviewed tenant table."),
        };
    }

    private static string CrossTenantUpdateSql(string table, Guid tenantId) =>
        table switch
        {
            "identity.device" =>
                $"UPDATE identity.device SET display_name = 'rls-hijack' WHERE tenant_id = '{tenantId}';",
            "identity.membership" =>
                $"UPDATE identity.membership SET permission_version = permission_version + 1 WHERE tenant_id = '{tenantId}';",
            "identity.role" =>
                $"UPDATE identity.role SET display_name = 'rls-hijack' WHERE tenant_id = '{tenantId}';",
            "identity.role_assignment" =>
                $"UPDATE identity.role_assignment SET revoked_at = now(), revoke_reason = 'rls-probe' WHERE tenant_id = '{tenantId}';",
            "identity.role_permission" =>
                $"UPDATE identity.role_permission SET permission_code = 'tenant.view' WHERE tenant_id = '{tenantId}';",
            "identity.session" =>
                $"UPDATE identity.session SET last_seen_at = now() WHERE tenant_id = '{tenantId}';",
            "platform.audit_event" =>
                $"UPDATE platform.audit_event SET outcome = 'denied' WHERE tenant_id = '{tenantId}';",
            "platform.idempotency_record" =>
                $"UPDATE platform.idempotency_record SET status = 'completed', response_status = 200 WHERE tenant_id = '{tenantId}';",
            "platform.outbox_message" =>
                $"UPDATE platform.outbox_message SET attempt_count = attempt_count + 1 WHERE tenant_id = '{tenantId}';",
            "saas.billing_attempt" =>
                $"UPDATE saas.billing_attempt SET status = 'failed' WHERE tenant_id = '{tenantId}';",
            "saas.entitlement" =>
                $"UPDATE saas.entitlement SET is_enabled = false WHERE tenant_id = '{tenantId}';",
            "saas.subscription" =>
                $"UPDATE saas.subscription SET status = 'suspended' WHERE tenant_id = '{tenantId}';",
            "saas.usage_record" =>
                $"UPDATE saas.usage_record SET quantity = 2 WHERE tenant_id = '{tenantId}';",
            "tenancy.branch" =>
                $"UPDATE tenancy.branch SET display_name = 'rls-hijack' WHERE tenant_id = '{tenantId}';",
            "tenancy.legal_entity" =>
                $"UPDATE tenancy.legal_entity SET display_name = 'rls-hijack' WHERE tenant_id = '{tenantId}';",
            "tenancy.register" =>
                $"UPDATE tenancy.register SET display_name = 'rls-hijack' WHERE tenant_id = '{tenantId}';",
            "tenancy.stock_location" =>
                $"UPDATE tenancy.stock_location SET display_name = 'rls-hijack' WHERE tenant_id = '{tenantId}';",
            "tenancy.tenant" =>
                $"UPDATE tenancy.tenant SET display_name = 'rls-hijack' WHERE tenant_id = '{tenantId}';",
            _ => throw new ArgumentOutOfRangeException(nameof(table), table, "Not a reviewed tenant table."),
        };

    private async Task<T?> ScalarAsAsync<T>(
        string? tenantId,
        string? userId,
        string? subject,
        string? sessionReference,
        string sql)
    {
        await using var connection = new NpgsqlConnection(Database.AppConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await ApplyContextAsync(connection, transaction, tenantId, userId, subject, sessionReference);

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        var value = await command.ExecuteScalarAsync();

        await transaction.RollbackAsync();
        return value is null or DBNull ? default : (T)value;
    }

    private async Task<int> ExecuteAsAsync(
        string? tenantId,
        string? userId,
        string? subject,
        string? sessionReference,
        string sql)
    {
        await using var connection = new NpgsqlConnection(Database.AppConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await ApplyContextAsync(connection, transaction, tenantId, userId, subject, sessionReference);

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        var affected = await command.ExecuteNonQueryAsync();

        await transaction.RollbackAsync();
        return affected;
    }

    private static async Task<long> ScalarOnConnectionAsync<T>(
        NpgsqlConnection connection,
        string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? 0 : Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task ApplyContextAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string? tenantId,
        string? userId,
        string? subject,
        string? sessionReference)
    {
        await SetAsync(connection, transaction, "app.tenant_id", tenantId);
        await SetAsync(connection, transaction, "app.user_id", userId);
        await SetAsync(connection, transaction, "app.subject", subject);
        await SetAsync(connection, transaction, "app.session_reference", sessionReference);
    }

    private static async Task SetAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string name,
        string? value)
    {
        await using var command = new NpgsqlCommand(
            "SELECT set_config(@name, @value, true);",
            connection,
            transaction);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("value", value ?? string.Empty);
        _ = await command.ExecuteScalarAsync();
    }

    private static async Task AssertRlsRejectedAsync(string label, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (PostgresException exception)
        {
            Assert.True(
                exception.SqlState == "42501",
                $"{label}: expected SQLSTATE 42501, got {exception.SqlState}: {exception.MessageText}");
            Assert.True(
                exception.MessageText.Contains("row-level security policy", StringComparison.Ordinal),
                $"{label}: expected an RLS rejection, got: {exception.MessageText}");
            return;
        }

        Assert.Fail($"{label}: the statement was accepted but should have been rejected.");
    }

    private static async Task AssertPrivilegeRejectedAsync(string label, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (PostgresException exception)
        {
            Assert.True(
                exception.SqlState == "42501",
                $"{label}: expected SQLSTATE 42501, got {exception.SqlState}: {exception.MessageText}");
            Assert.True(
                exception.MessageText.Contains("permission denied", StringComparison.Ordinal),
                $"{label}: expected a privilege rejection, got: {exception.MessageText}");
            return;
        }

        Assert.Fail($"{label}: the statement was accepted but the runtime role has no grant.");
    }

    private static async Task AssertCheckRejectedAsync(string label, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (PostgresException exception)
        {
            Assert.True(
                exception.SqlState == "23514",
                $"{label}: expected SQLSTATE 23514, got {exception.SqlState}: {exception.MessageText}");
            Assert.True(
                exception.MessageText.Contains("violates check constraint", StringComparison.Ordinal),
                $"{label}: expected a check-constraint rejection, got: {exception.MessageText}");
            return;
        }

        Assert.Fail($"{label}: the statement was accepted but the value is out of bounds.");
    }

    private static async Task AssertContextRejectedAsync(
        string label,
        Func<Task> action,
        string expectedMessage)
    {
        try
        {
            await action();
        }
        catch (PostgresException exception)
        {
            Assert.True(
                exception.SqlState == "42501",
                $"{label}: expected SQLSTATE 42501, got {exception.SqlState}: {exception.MessageText}");
            Assert.True(
                exception.MessageText.Contains(expectedMessage, StringComparison.Ordinal),
                $"{label}: expected '{expectedMessage}', got: {exception.MessageText}");
            return;
        }

        Assert.Fail($"{label}: the statement succeeded but should have failed closed.");
    }

    private static string[] Names(List<object?[]> rows) =>
        rows.Select(row => (string)row[0]!).ToArray();

    private static string[] Sorted(IEnumerable<string> values) =>
        values.OrderBy(value => value, StringComparer.Ordinal).ToArray();

    private sealed record RlsTenant(
        Guid TenantId,
        Guid UserId,
        Guid MembershipId,
        Guid RoleId,
        Guid SessionId,
        Guid DeviceId,
        Guid IdempotencyId,
        Guid OutboxId,
        Guid SubscriptionId,
        Guid EntitlementId,
        Guid UsageId,
        Guid BillingId,
        Guid LegalEntityId,
        Guid BranchId,
        Guid StockLocationId,
        Guid RegisterId,
        string Subject,
        string SessionReference,
        string IdempotencyKey);
}
