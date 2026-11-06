using Npgsql;
using PhoneShopErp.Modules.IdentityAccess.Application;
using Xunit;

namespace PhoneShopErp.Database.Tests;

/// <summary>
/// Stage 5 T-01: the runtime write boundary on <c>identity.user</c> (Stage 4
/// finding D-01).
///
/// <c>erp_app</c> may read its own row and provision a new user, but it must never
/// update an identity record. Every denial below runs as the real <c>erp_app</c>
/// role inside a transaction that sets the trusted <c>app.subject</c> context;
/// the superuser connection is used only to seed state and to prove that nothing
/// changed. <c>postgres</c> is never used as a substitute for the runtime role.
/// </summary>
[Collection(PostgresDatabaseGroup.Name)]
public sealed class IdentityUserWriteBoundaryTests
{
    private readonly DatabaseFixture fixture;

    public IdentityUserWriteBoundaryTests(DatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    private TestDatabase Database => fixture.Database;

    [Fact]
    public async Task GrantAndPolicySurface_IsLeastPrivilege()
    {
        var privileges = (await Database.QueryAsync(
            """
            SELECT
                has_table_privilege('erp_app', 'identity.user', 'SELECT'),
                has_table_privilege('erp_app', 'identity.user', 'INSERT'),
                has_table_privilege('erp_app', 'identity.user', 'UPDATE'),
                has_table_privilege('erp_app', 'identity.user', 'DELETE'),
                has_table_privilege('erp_app', 'identity.user', 'TRUNCATE'),
                has_any_column_privilege('erp_app', 'identity.user', 'UPDATE'),
                has_column_privilege('erp_app', 'identity.user', 'status', 'UPDATE'),
                has_column_privilege('erp_app', 'identity.user', 'subject', 'UPDATE'),
                has_column_privilege('erp_app', 'identity.user', 'email', 'UPDATE'),
                has_column_privilege('erp_app', 'identity.user', 'display_name', 'UPDATE'),
                has_column_privilege('erp_app', 'identity.user', 'row_version', 'UPDATE'),
                has_column_privilege('erp_app', 'identity.user', 'id', 'UPDATE');
            """,
            "postgres"))[0];

        Assert.True((bool)privileges[0]!, "erp_app must keep SELECT for the self-read path.");
        Assert.True((bool)privileges[1]!, "erp_app must keep INSERT for just-in-time provisioning.");

        for (var index = 2; index < privileges.Length; index++)
        {
            Assert.False((bool)privileges[index]!, $"erp_app must not hold UPDATE privilege (index {index}).");
        }

        var updatePolicies = await Database.ScalarAsync<long>(
            """
            SELECT count(*)
            FROM pg_policies
            WHERE schemaname = 'identity' AND tablename = 'user' AND cmd = 'UPDATE';
            """,
            "postgres");
        Assert.Equal(0, updatePolicies);
    }

    [Fact]
    public async Task RuntimeRole_CannotChangeItsOwnStatus()
    {
        var tenant = await fixture.CreateTenantAsync("t01-own-status");

        await AssertPrivilegeRejectedAsync(
            "own status update",
            () => ExecuteAsSubjectAsync(
                tenant.Subject,
                $"UPDATE identity.user SET status = 'deactivated' WHERE id = '{tenant.UserId}';"));

        Assert.Equal("active", await UserStatusAsync(tenant.UserId));
    }

    [Fact]
    public async Task DeactivatedUser_CannotReactivateThroughSqlOrProvisioning()
    {
        var tenant = await fixture.CreateTenantAsync("t01-reactivation");
        await Database.ExecuteAsync(
            $"UPDATE identity.user SET status = 'deactivated' WHERE id = '{tenant.UserId}';",
            "postgres");

        await AssertPrivilegeRejectedAsync(
            "direct reactivation",
            () => ExecuteAsSubjectAsync(
                tenant.Subject,
                $"UPDATE identity.user SET status = 'active' WHERE id = '{tenant.UserId}';"));

        // The provisioning path is INSERT ... ON CONFLICT DO NOTHING. It must not
        // turn the existing deactivated row back into an active one.
        var resolved = await fixture.Identity.EnsureUserAsync(
            tenant.Principal("t01-reactivation-attempt"),
            CancellationToken.None);

        Assert.Equal(tenant.UserId, resolved);
        Assert.Equal("deactivated", await UserStatusAsync(tenant.UserId));
    }

    [Fact]
    public async Task RuntimeRole_CannotChangeItsOwnProviderSubject()
    {
        var tenant = await fixture.CreateTenantAsync("t01-subject");

        await AssertPrivilegeRejectedAsync(
            "own subject update",
            () => ExecuteAsSubjectAsync(
                tenant.Subject,
                $"UPDATE identity.user SET subject = 'attacker-subject' WHERE id = '{tenant.UserId}';"));

        Assert.Equal(
            tenant.Subject,
            await Database.ScalarAsync<string>(
                $"SELECT subject FROM identity.user WHERE id = '{tenant.UserId}';",
                "postgres"));
    }

    [Fact]
    public async Task RuntimeRole_CannotUpdateAnotherUsersRecord()
    {
        var attacker = await fixture.CreateTenantAsync("t01-attacker");
        var victim = await fixture.CreateTenantAsync("t01-victim");

        var before = await Database.ScalarAsync<string>(
            $"SELECT display_name FROM identity.user WHERE id = '{victim.UserId}';",
            "postgres");

        await AssertPrivilegeRejectedAsync(
            "another user's display name",
            () => ExecuteAsSubjectAsync(
                attacker.Subject,
                $"UPDATE identity.user SET display_name = 'hijacked' WHERE id = '{victim.UserId}';"));

        Assert.Equal(
            before,
            await Database.ScalarAsync<string>(
                $"SELECT display_name FROM identity.user WHERE id = '{victim.UserId}';",
                "postgres"));
    }

    [Fact]
    public async Task RuntimeRole_HasNoApprovedProfileUpdatePath()
    {
        var tenant = await fixture.CreateTenantAsync("t01-profile");

        await AssertPrivilegeRejectedAsync(
            "provider-managed email",
            () => ExecuteAsSubjectAsync(
                tenant.Subject,
                $"UPDATE identity.user SET email = 'attacker@example.test' WHERE id = '{tenant.UserId}';"));

        await AssertPrivilegeRejectedAsync(
            "provider-managed display name",
            () => ExecuteAsSubjectAsync(
                tenant.Subject,
                $"UPDATE identity.user SET display_name = 'attacker' WHERE id = '{tenant.UserId}';"));

        var row = (await Database.QueryAsync(
            $"SELECT display_name, email FROM identity.user WHERE id = '{tenant.UserId}';",
            "postgres"))[0];

        Assert.Equal("Owner t01-profile", (string)row[0]!);
        Assert.Equal(tenant.Subject + "@example.test", (string)row[1]!);
    }

    [Fact]
    public async Task Provisioning_KeepsTheDocumentedProviderSnapshot()
    {
        var subject = "subject-t01-provision";
        var first = await fixture.Identity.EnsureUserAsync(
            new AuthenticatedPrincipal(subject, "sid-1", "Original Name", "original@example.test"),
            CancellationToken.None);

        var second = await fixture.Identity.EnsureUserAsync(
            new AuthenticatedPrincipal(subject, "sid-2", "Changed Name", "changed@example.test"),
            CancellationToken.None);

        Assert.Equal(first, second);

        var rows = await Database.QueryAsync(
            $"SELECT display_name, email, status, row_version FROM identity.user WHERE id = '{first}';",
            "postgres");

        Assert.Single(rows);
        Assert.Equal("Original Name", (string)rows[0][0]!);
        Assert.Equal("original@example.test", (string)rows[0][1]!);
        Assert.Equal("active", (string)rows[0][2]!);
        Assert.Equal(1, (int)rows[0][3]!);
    }

    [Fact]
    public async Task RowVersionAndUpdatedAt_StillBumpForAuthorizedUpdates()
    {
        var tenant = await fixture.CreateTenantAsync("t01-row-version");

        var before = (await Database.QueryAsync(
            $"SELECT row_version, updated_at FROM identity.user WHERE id = '{tenant.UserId}';",
            "postgres"))[0];

        await Database.ExecuteAsync(
            $"UPDATE identity.user SET display_name = 'Authorised Rename' WHERE id = '{tenant.UserId}';",
            "postgres");

        var after = (await Database.QueryAsync(
            $"SELECT row_version, updated_at FROM identity.user WHERE id = '{tenant.UserId}';",
            "postgres"))[0];

        Assert.Equal((int)before[0]! + 1, (int)after[0]!);
        Assert.NotEqual(before[1], after[1]);
    }

    [Fact]
    public async Task DeniedIdentityWrites_DoNotChangeTheAuditLedger()
    {
        var tenant = await fixture.CreateTenantAsync("t01-audit");

        var before = await Database.ScalarAsync<long>(
            $"SELECT count(*) FROM platform.audit_event WHERE tenant_id = '{tenant.TenantId}';",
            "postgres");
        Assert.True(before >= 1, "Onboarding must have written an audit event.");

        await AssertPrivilegeRejectedAsync(
            "status",
            () => ExecuteAsSubjectAsync(
                tenant.Subject,
                $"UPDATE identity.user SET status = 'deactivated' WHERE id = '{tenant.UserId}';"));

        await AssertPrivilegeRejectedAsync(
            "subject",
            () => ExecuteAsSubjectAsync(
                tenant.Subject,
                $"UPDATE identity.user SET subject = 'attacker-subject' WHERE id = '{tenant.UserId}';"));

        Assert.Equal(
            before,
            await Database.ScalarAsync<long>(
                $"SELECT count(*) FROM platform.audit_event WHERE tenant_id = '{tenant.TenantId}';",
                "postgres"));
    }

    private async Task<string?> UserStatusAsync(Guid userId) =>
        await Database.ScalarAsync<string>(
            $"SELECT status FROM identity.user WHERE id = '{userId}';",
            "postgres");

    private async Task ExecuteAsSubjectAsync(string subject, string sql)
    {
        await using var connection = new NpgsqlConnection(Database.AppConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using (var setContext = new NpgsqlCommand(
            "SELECT set_config('app.subject', @subject, true);",
            connection,
            transaction))
        {
            setContext.Parameters.AddWithValue("subject", subject);
            _ = await setContext.ExecuteScalarAsync();
        }

        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            _ = await command.ExecuteNonQueryAsync();
        }

        await transaction.RollbackAsync();
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

        Assert.Fail($"{label}: the statement was accepted but erp_app must not have UPDATE privilege.");
    }
}
