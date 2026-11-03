using Npgsql;
using Xunit;

namespace PhoneShopErp.Database.Tests;

/// <summary>
/// Tenant isolation under the real runtime role with forced RLS. Every assertion
/// runs as <c>erp_app</c>; the superuser connection is used only to seed and to
/// verify that nothing changed.
/// </summary>
[Collection(PostgresDatabaseGroup.Name)]
public sealed class TenantIsolationTests
{
    private readonly DatabaseFixture fixture;

    public TenantIsolationTests(DatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    private TestDatabase Database => fixture.Database;

    [Fact]
    public async Task WithoutAnyIdentityContext_TheTenantRootReturnsNoRowsButStrictTablesRaise()
    {
        var visible = await ScalarAsync<long>(null, null, null, "SELECT count(*) FROM tenancy.tenant;");
        Assert.Equal(0, visible);

        var strict = await Assert.ThrowsAsync<PostgresException>(
            () => ScalarAsync<long>(null, null, null, "SELECT count(*) FROM platform.audit_event;"));
        Assert.Equal("42501", strict.SqlState);
    }

    [Fact]
    public async Task MissingMalformedOrNilTenantContext_IsRejected()
    {
        var missing = await Assert.ThrowsAsync<PostgresException>(
            () => ScalarAsync<long>(null, null, null, "SELECT count(*) FROM tenancy.branch;"));
        Assert.Equal("42501", missing.SqlState);

        var malformed = await Assert.ThrowsAsync<PostgresException>(
            () => ScalarAsync<long>("not-a-uuid", null, null, "SELECT count(*) FROM tenancy.branch;"));
        Assert.Equal("42501", malformed.SqlState);

        var nil = await Assert.ThrowsAsync<PostgresException>(
            () => ScalarAsync<long>(Guid.Empty.ToString(), null, null, "SELECT count(*) FROM tenancy.branch;"));
        Assert.Equal("42501", nil.SqlState);
    }

    [Fact]
    public async Task CrossTenantReads_AreInvisible()
    {
        var a = await fixture.CreateTenantAsync("rls-read-a");
        var b = await fixture.CreateTenantAsync("rls-read-b");

        var visibleTenants = await ScalarAsync<long>(
            a.TenantId.ToString(), a.UserId.ToString(), null,
            "SELECT count(*) FROM tenancy.tenant;");
        Assert.Equal(1, visibleTenants);

        var otherTenant = await ScalarAsync<long>(
            a.TenantId.ToString(), a.UserId.ToString(), null,
            $"SELECT count(*) FROM tenancy.tenant WHERE id = '{b.TenantId}';");
        Assert.Equal(0, otherTenant);

        var otherMembership = await ScalarAsync<long>(
            a.TenantId.ToString(), a.UserId.ToString(), null,
            $"SELECT count(*) FROM identity.membership WHERE tenant_id = '{b.TenantId}';");
        Assert.Equal(0, otherMembership);

        var otherSession = await ScalarAsync<long>(
            a.TenantId.ToString(), a.UserId.ToString(), null,
            $"SELECT count(*) FROM identity.session WHERE tenant_id = '{b.TenantId}';");
        Assert.Equal(0, otherSession);

        var otherAudit = await ScalarAsync<long>(
            a.TenantId.ToString(), a.UserId.ToString(), null,
            $"SELECT count(*) FROM platform.audit_event WHERE tenant_id = '{b.TenantId}';");
        Assert.Equal(0, otherAudit);
    }

    [Fact]
    public async Task CrossTenantInsert_IsRejectedByTheWithCheckPolicy()
    {
        var a = await fixture.CreateTenantAsync("rls-write-a");
        var b = await fixture.CreateTenantAsync("rls-write-b");

        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            a.TenantId.ToString(),
            a.UserId.ToString(),
            null,
            $"""
            INSERT INTO platform.audit_event (id, tenant_id, actor_type, action, outcome)
            VALUES ('{Guid.NewGuid()}', '{b.TenantId}', 'system', 'tenant.created', 'success');
            """));

        Assert.Equal("42501", exception.SqlState);
    }

    [Fact]
    public async Task CrossTenantUpdateAndDelete_AffectNoRows()
    {
        var a = await fixture.CreateTenantAsync("rls-mutate-a");
        var b = await fixture.CreateTenantAsync("rls-mutate-b");
        var displayNameBefore = await Database.ScalarAsync<string>(
            $"SELECT display_name FROM tenancy.tenant WHERE id = '{b.TenantId}';",
            "postgres");

        var updated = await ExecuteAsync(
            a.TenantId.ToString(),
            a.UserId.ToString(),
            null,
            $"UPDATE tenancy.tenant SET display_name = 'hijacked' WHERE id = '{b.TenantId}';");
        Assert.Equal(0, updated);

        var deleted = await ExecuteAsync(
            a.TenantId.ToString(),
            a.UserId.ToString(),
            null,
            $"DELETE FROM identity.role_permission WHERE tenant_id = '{b.TenantId}';");
        Assert.Equal(0, deleted);

        var displayNameAfter = await Database.ScalarAsync<string>(
            $"SELECT display_name FROM tenancy.tenant WHERE id = '{b.TenantId}';",
            "postgres");
        Assert.Equal(displayNameBefore, displayNameAfter);
    }

    [Fact]
    public async Task PreTenantTenantPicker_SeesOnlyTheCallersTenants()
    {
        var a = await fixture.CreateTenantAsync("picker-a");
        var b = await fixture.CreateTenantAsync("picker-b");

        var visible = await ScalarAsync<string>(
            null,
            a.UserId.ToString(),
            a.Subject,
            "SELECT string_agg(id::text, ',' ORDER BY id) FROM tenancy.tenant;");

        Assert.Equal(a.TenantId.ToString(), visible);
        Assert.DoesNotContain(b.TenantId.ToString(), visible!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreTenantMembershipRead_IsLimitedToTheCaller()
    {
        var a = await fixture.CreateTenantAsync("picker-member-a");
        var b = await fixture.CreateTenantAsync("picker-member-b");

        var rows = await ScalarAsync<long>(
            null,
            a.UserId.ToString(),
            a.Subject,
            "SELECT count(*) FROM identity.membership;");
        Assert.Equal(1, rows);

        var other = await ScalarAsync<long>(
            null,
            a.UserId.ToString(),
            a.Subject,
            $"SELECT count(*) FROM identity.membership WHERE user_id = '{b.UserId}';");
        Assert.Equal(0, other);
    }

    [Fact]
    public async Task RemovedMembership_RemovesTheTenantFromThePicker()
    {
        var tenant = await fixture.CreateTenantAsync("picker-removed");

        await Database.ExecuteAsync(
            $"UPDATE identity.membership SET status = 'removed' WHERE id = '{tenant.MembershipId}';",
            "postgres");

        var visible = await ScalarAsync<long>(
            null,
            tenant.UserId.ToString(),
            tenant.Subject,
            $"SELECT count(*) FROM tenancy.tenant WHERE id = '{tenant.TenantId}';");

        Assert.Equal(0, visible);
    }

    [Fact]
    public async Task ForcedRls_AppliesToTheTableOwner()
    {
        await fixture.CreateTenantAsync("owner-forced-rls");

        await using var connection = new NpgsqlConnection(Database.ConnectionStringFor("erp_migrator"));
        await connection.OpenAsync();

        await using (var who = new NpgsqlCommand("SELECT current_user;", connection))
        {
            Assert.Equal("erp_owner", (string)(await who.ExecuteScalarAsync())!);
        }

        await using var command = new NpgsqlCommand("SELECT count(*) FROM tenancy.tenant;", connection);
        var visible = (long)(await command.ExecuteScalarAsync())!;

        Assert.Equal(0, visible);
    }

    [Fact]
    public async Task SessionLocalContext_DisappearsWhenTheTransactionEnds()
    {
        var tenant = await fixture.CreateTenantAsync("context-lifetime");

        await using var connection = new NpgsqlConnection(Database.AppConnectionString);
        await connection.OpenAsync();

        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await SetAsync(connection, transaction, "app.tenant_id", tenant.TenantId.ToString());

            await using var inside = new NpgsqlCommand(
                "SELECT current_setting('app.tenant_id', true);",
                connection,
                transaction);
            Assert.Equal(tenant.TenantId.ToString(), (string)(await inside.ExecuteScalarAsync())!);

            await transaction.CommitAsync();
        }

        await using var after = new NpgsqlCommand(
            "SELECT current_setting('app.tenant_id', true);",
            connection);
        var value = await after.ExecuteScalarAsync();
        Assert.True(value is null or DBNull or "", $"Context leaked after commit: '{value}'");
    }

    [Fact]
    public async Task PooledConnections_DoNotCarryTenantContextBetweenRequests()
    {
        var tenant = await fixture.CreateTenantAsync("pool-leak");
        var pooled = new Npgsql.NpgsqlDataSourceBuilder(Database.PooledConnectionStringFor("erp_app", 1)).Build();

        await using (pooled)
        {
            await using (var first = await pooled.OpenConnectionAsync())
            {
                await using var transaction = await first.BeginTransactionAsync();
                await SetAsync(first, transaction, "app.tenant_id", tenant.TenantId.ToString());
                await SetAsync(first, transaction, "app.user_id", tenant.UserId.ToString());
                await transaction.CommitAsync();
            }

            // A pooled connection is reset with DISCARD ALL before reuse, so the
            // next logical request must start with no context.
            await using var second = await pooled.OpenConnectionAsync();
            await using var command = new NpgsqlCommand(
                "SELECT coalesce(current_setting('app.tenant_id', true), '') || '|' || coalesce(current_setting('app.user_id', true), '');",
                second);

            Assert.Equal("|", (string)(await command.ExecuteScalarAsync())!);

            await using var visible = new NpgsqlCommand("SELECT count(*) FROM platform.audit_event;", second);
            var exception = await Assert.ThrowsAsync<PostgresException>(
                () => visible.ExecuteScalarAsync());
            Assert.Equal("42501", exception.SqlState);
        }
    }

    private async Task<T?> ScalarAsync<T>(string? tenantId, string? userId, string? subject, string sql)
    {
        await using var connection = new NpgsqlConnection(Database.AppConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await SetAsync(connection, transaction, "app.tenant_id", tenantId);
        await SetAsync(connection, transaction, "app.user_id", userId);
        await SetAsync(connection, transaction, "app.subject", subject);

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        var value = await command.ExecuteScalarAsync();

        await transaction.RollbackAsync();
        return value is null or DBNull ? default : (T)value;
    }

    private async Task<int> ExecuteAsync(string? tenantId, string? userId, string? subject, string sql)
    {
        await using var connection = new NpgsqlConnection(Database.AppConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await SetAsync(connection, transaction, "app.tenant_id", tenantId);
        await SetAsync(connection, transaction, "app.user_id", userId);
        await SetAsync(connection, transaction, "app.subject", subject);

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        var affected = await command.ExecuteNonQueryAsync();

        await transaction.RollbackAsync();
        return affected;
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
}
