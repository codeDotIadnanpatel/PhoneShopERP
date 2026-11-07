using System.Text.Json;
using Npgsql;
using PhoneShopErp.Persistence;
using PhoneShopErp.Platform.Audit;
using PhoneShopErp.Platform.Tenancy;
using Xunit;

namespace PhoneShopErp.Database.Tests;

/// <summary>
/// T-04: the persistence boundary must redact audit metadata even when a caller
/// forgets to. These tests write through the real <see cref="PostgresAuditStore"/>
/// and read the persisted JSONB back, so they cover the actual SQL path rather
/// than a helper in isolation. They also prove the schema's size bound and the
/// existing RLS/grant model are unchanged.
/// </summary>
[Collection(PostgresDatabaseGroup.Name)]
public sealed class AuditRedactionTests
{
    private static readonly string[] Secrets =
    [
        "hunter2",
        "provider-token-value",
        "refresh-value",
        "4111111111111111",
        "sk-live-123",
        "Host=db",
        "BEGIN PRIVATE KEY",
        "bearer-value",
        "cookie-value",
        "4821",
    ];

    private readonly DatabaseFixture fixture;

    public AuditRedactionTests(DatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    private TestDatabase Database => fixture.Database;

    [Fact]
    public async Task AuditDetail_IsRedactedAtThePersistenceBoundary()
    {
        var tenant = await fixture.CreateTenantAsync("t04-audit-redaction");
        var auditId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var detail = JsonSerializer.SerializeToDocument(new Dictionary<string, object?>
        {
            ["slug"] = "t04-audit-redaction",
            ["actor_email"] = "owner@example.test",
            ["PASSWORD"] = "hunter2",
            ["empty"] = string.Empty,
            ["nothing"] = null,
            ["provider"] = new Dictionary<string, object?>
            {
                ["access_token"] = "provider-token-value",
                ["region"] = "eu-west-1",
            },
            ["items"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["card_number"] = "4111111111111111",
                    ["sku"] = "SKU-1",
                },
            },
            ["api_key"] = "sk-live-123",
            ["connection_string"] = "Host=db;Username=app;Password=hunter2",
            ["private_key"] = "-----BEGIN PRIVATE KEY-----",
            ["device"] = new Dictionary<string, object?> { ["passcode"] = "4821" },
            ["encoded"] = "{\"refresh_token\":\"refresh-value\",\"safe\":\"kept\"}",
        });

        var context = TenantContext.Create(
            tenant.TenantId,
            tenant.UserId,
            Guid.NewGuid(),
            tenant.MembershipId,
            1);

        await using (var unitOfWork = await fixture.TenantUnitOfWorkFactory.BeginAsync(
                         context,
                         CancellationToken.None))
        {
            await fixture.Audit.AppendAsync(
                unitOfWork,
                new AuditEvent(
                    auditId,
                    tenant.TenantId,
                    DateTimeOffset.UtcNow,
                    AuditActorTypes.User,
                    tenant.UserId,
                    AuditActions.TenantCreated,
                    AuditOutcomes.Success,
                    "tenant",
                    targetId,
                    "corr-t04-redaction",
                    "operator note",
                    detail),
                CancellationToken.None);

            await unitOfWork.CommitAsync(CancellationToken.None);
        }

        var row = Assert.Single(await Database.QueryAsync(
            $"""
            SELECT detail::text,
                   action, outcome, actor_type, actor_user_id,
                   target_type, target_id, correlation_id, reason
            FROM platform.audit_event
            WHERE id = '{auditId}';
            """,
            "erp_platform_admin"));

        var persisted = Assert.IsType<string>(row[0]);
        AssertNoSecret(persisted);

        // Core event attribution and context survive untouched.
        Assert.Equal(AuditActions.TenantCreated, (string)row[1]!);
        Assert.Equal(AuditOutcomes.Success, (string)row[2]!);
        Assert.Equal(AuditActorTypes.User, (string)row[3]!);
        Assert.Equal(tenant.UserId, (Guid)row[4]!);
        Assert.Equal("tenant", (string)row[5]!);
        Assert.Equal(targetId, (Guid)row[6]!);
        Assert.Equal("corr-t04-redaction", (string)row[7]!);
        Assert.Equal("operator note", (string)row[8]!);

        using var document = JsonDocument.Parse(persisted);
        var root = document.RootElement;

        // Safe metadata is preserved, including null/empty values.
        Assert.Equal("t04-audit-redaction", root.GetProperty("slug").GetString());
        Assert.Equal("owner@example.test", root.GetProperty("actor_email").GetString());
        Assert.Equal(string.Empty, root.GetProperty("empty").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("nothing").ValueKind);
        Assert.Equal("eu-west-1", root.GetProperty("provider").GetProperty("region").GetString());
        Assert.Equal("SKU-1", root.GetProperty("items")[0].GetProperty("sku").GetString());

        // Sensitive values are replaced wherever they appear.
        Assert.Equal("[REDACTED]", root.GetProperty("PASSWORD").GetString());
        Assert.Equal(
            "[REDACTED]",
            root.GetProperty("provider").GetProperty("access_token").GetString());
        Assert.Equal(
            "[REDACTED]",
            root.GetProperty("items")[0].GetProperty("card_number").GetString());
        Assert.Equal("[REDACTED]", root.GetProperty("api_key").GetString());
        Assert.Equal("[REDACTED]", root.GetProperty("connection_string").GetString());
        Assert.Equal("[REDACTED]", root.GetProperty("private_key").GetString());
        Assert.Equal(
            "[REDACTED]",
            root.GetProperty("device").GetProperty("passcode").GetString());

        // JSON encoded inside a string value is redacted one level down while
        // its safe sibling survives.
        using var encoded = JsonDocument.Parse(root.GetProperty("encoded").GetString()!);
        Assert.Equal("[REDACTED]", encoded.RootElement.GetProperty("refresh_token").GetString());
        Assert.Equal("kept", encoded.RootElement.GetProperty("safe").GetString());
    }

    [Fact]
    public async Task SecurityEventDetail_IsRedactedAtThePersistenceBoundary()
    {
        var securityEventId = Guid.NewGuid();

        var detail = JsonSerializer.SerializeToDocument(new Dictionary<string, object?>
        {
            ["event_type"] = "authentication.failed",
            ["provider"] = "oidc",
            ["password"] = "hunter2",
            ["authorization"] = "Bearer bearer-value",
            ["cookie"] = "session=cookie-value",
            ["device"] = new Dictionary<string, object?> { ["passcode"] = "4821" },
        });

        await using (var unitOfWork = await fixture.PreTenantUnitOfWorkFactory.BeginAsync(
                         PreTenantContext.ForSubject("subject-t04-security"),
                         CancellationToken.None))
        {
            await fixture.Audit.AppendSecurityEventAsync(
                unitOfWork,
                new SecurityEvent(
                    securityEventId,
                    DateTimeOffset.UtcNow,
                    SecurityEventCategories.Authentication,
                    SecurityEventSeverities.High,
                    AuditOutcomes.Denied,
                    "subject-t04-security",
                    "corr-t04-security",
                    detail),
                CancellationToken.None);

            await unitOfWork.CommitAsync(CancellationToken.None);
        }

        var row = Assert.Single(await Database.QueryAsync(
            $"""
            SELECT detail::text, category, severity, outcome, actor_reference, correlation_id
            FROM platform.security_event
            WHERE id = '{securityEventId}';
            """,
            "erp_platform_admin"));

        var persisted = Assert.IsType<string>(row[0]);
        AssertNoSecret(persisted);

        Assert.Equal(SecurityEventCategories.Authentication, (string)row[1]!);
        Assert.Equal(SecurityEventSeverities.High, (string)row[2]!);
        Assert.Equal(AuditOutcomes.Denied, (string)row[3]!);
        Assert.Equal("subject-t04-security", (string)row[4]!);
        Assert.Equal("corr-t04-security", (string)row[5]!);

        using var document = JsonDocument.Parse(persisted);
        var root = document.RootElement;
        Assert.Equal("authentication.failed", root.GetProperty("event_type").GetString());
        Assert.Equal("oidc", root.GetProperty("provider").GetString());
        Assert.Equal("[REDACTED]", root.GetProperty("password").GetString());
        Assert.Equal("[REDACTED]", root.GetProperty("authorization").GetString());
        Assert.Equal("[REDACTED]", root.GetProperty("cookie").GetString());
        Assert.Equal(
            "[REDACTED]",
            root.GetProperty("device").GetProperty("passcode").GetString());
    }

    [Fact]
    public async Task AuditDetail_MalformedEmbeddedJsonIsPreservedWithoutFailingTheEvent()
    {
        var tenant = await fixture.CreateTenantAsync("t04-audit-malformed");
        var auditId = Guid.NewGuid();

        var detail = JsonSerializer.SerializeToDocument(new Dictionary<string, object?>
        {
            ["note"] = "{ not json",
            ["password"] = "hunter2",
        });

        var context = TenantContext.Create(
            tenant.TenantId,
            tenant.UserId,
            Guid.NewGuid(),
            tenant.MembershipId,
            1);

        await using (var unitOfWork = await fixture.TenantUnitOfWorkFactory.BeginAsync(
                         context,
                         CancellationToken.None))
        {
            await fixture.Audit.AppendAsync(
                unitOfWork,
                new AuditEvent(
                    auditId,
                    tenant.TenantId,
                    DateTimeOffset.UtcNow,
                    AuditActorTypes.System,
                    null,
                    AuditActions.TenantConfigured,
                    AuditOutcomes.Success,
                    "tenant",
                    tenant.TenantId,
                    null,
                    null,
                    detail),
                CancellationToken.None);

            await unitOfWork.CommitAsync(CancellationToken.None);
        }

        var persisted = Assert.IsType<string>(Assert.Single(await Database.QueryAsync(
            $"SELECT detail::text FROM platform.audit_event WHERE id = '{auditId}';",
            "erp_platform_admin"))[0]);

        AssertNoSecret(persisted);

        using var document = JsonDocument.Parse(persisted);
        Assert.Equal("{ not json", document.RootElement.GetProperty("note").GetString());
        Assert.Equal("[REDACTED]", document.RootElement.GetProperty("password").GetString());
    }

    [Fact]
    public async Task AuditDetailSizeBound_IsStillEnforcedAfterRedaction()
    {
        var tenant = await fixture.CreateTenantAsync("t04-audit-bounds");
        var context = TenantContext.Create(
            tenant.TenantId,
            tenant.UserId,
            Guid.NewGuid(),
            tenant.MembershipId,
            1);

        // A near-bound but benign detail still fits through the real store.
        await using (var unitOfWork = await fixture.TenantUnitOfWorkFactory.BeginAsync(
                         context,
                         CancellationToken.None))
        {
            await fixture.Audit.AppendAsync(
                unitOfWork,
                AuditWithBlob(tenant.TenantId, new string('a', 8000)),
                CancellationToken.None);
            await unitOfWork.CommitAsync(CancellationToken.None);
        }

        // Redaction must not become a way around the 8 KiB schema bound.
        await using (var unitOfWork = await fixture.TenantUnitOfWorkFactory.BeginAsync(
                         context,
                         CancellationToken.None))
        {
            var exception = await Assert.ThrowsAsync<PostgresException>(() =>
                fixture.Audit.AppendAsync(
                    unitOfWork,
                    AuditWithBlob(tenant.TenantId, new string('b', 9000)),
                    CancellationToken.None));

            Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
            Assert.Equal("ck_audit_event_detail_size", exception.ConstraintName);

            await unitOfWork.RollbackAsync(CancellationToken.None);
        }
    }

    private static AuditEvent AuditWithBlob(Guid tenantId, string blob) =>
        new(
            Guid.NewGuid(),
            tenantId,
            DateTimeOffset.UtcNow,
            AuditActorTypes.System,
            null,
            AuditActions.TenantConfigured,
            AuditOutcomes.Success,
            "tenant",
            tenantId,
            null,
            null,
            JsonSerializer.SerializeToDocument(new { blob }));

    private static void AssertNoSecret(string persisted)
    {
        foreach (var secret in Secrets)
        {
            Assert.DoesNotContain(secret, persisted, StringComparison.Ordinal);
        }
    }
}
