using PhoneShopErp.Platform.Security;
using Xunit;

namespace PhoneShopErp.Platform.Tests;

public sealed class SensitiveDataRedactorTests
{
    [Theory]
    [InlineData("password")]
    [InlineData("Password")]
    [InlineData("card_number")]
    [InlineData("CardNumber")]
    [InlineData("cvv")]
    [InlineData("cvc")]
    [InlineData("id_token")]
    [InlineData("access_token")]
    [InlineData("refresh_token")]
    [InlineData("session_token")]
    [InlineData("passcode")]
    [InlineData("authorization")]
    [InlineData("secret")]
    [InlineData("otp")]
    public void IsSensitive_RecognisesBaselineCredentialFields(string name)
    {
        Assert.True(SensitiveDataRedactor.IsSensitive(name));
    }

    [Theory]
    [InlineData("customer_name")]
    [InlineData("imei")]
    [InlineData("tenant_slug")]
    [InlineData("id")]
    public void IsSensitive_LeavesOrdinaryFieldsAlone(string name)
    {
        Assert.False(SensitiveDataRedactor.IsSensitive(name));
    }

    [Fact]
    public void Redact_ReplacesSensitiveValuesAndKeepsTheRest()
    {
        var redacted = SensitiveDataRedactor.Redact(
            new Dictionary<string, object?>
            {
                ["password"] = "hunter2",
                ["email"] = "owner@example.test",
                ["nested"] = new Dictionary<string, object?> { ["cvv"] = "123" },
            });

        Assert.Equal(SensitiveDataRedactor.Redacted, redacted["password"]);
        Assert.Equal("owner@example.test", redacted["email"]);

        var nested = Assert.IsType<Dictionary<string, object?>>(redacted["nested"]);
        Assert.Equal(SensitiveDataRedactor.Redacted, nested["cvv"]);
    }

    [Fact]
    public void RedactJson_RemovesNestedSensitiveProperties()
    {
        const string json = """
            {
              "email": "a@b.test",
              "provider": { "access_token": "abc", "keep": 1 },
              "items": [ { "card_number": "4111111111111111" } ]
            }
            """;

        var redacted = SensitiveDataRedactor.RedactJson(json);

        Assert.DoesNotContain("abc", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("4111111111111111", redacted, StringComparison.Ordinal);
        Assert.Contains("a@b.test", redacted, StringComparison.Ordinal);
        Assert.Contains("\"keep\":1", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void RedactJson_EscapesTheRedactionInsideStrings()
    {
        var redacted = SensitiveDataRedactor.RedactJson("""{"cvv":"123"}""");

        Assert.Equal("""{"cvv":"[REDACTED]"}""", redacted);
    }

    [Fact]
    public void RedactValue_LeavesNonJsonStringsAndScalarsUntouched()
    {
        var redacted = SensitiveDataRedactor.Redact(
            new Dictionary<string, object?>
            {
                ["message"] = "not json",
                ["count"] = 3,
                ["nothing"] = null,
            });

        Assert.Equal("not json", redacted["message"]);
        Assert.Equal(3, redacted["count"]);
        Assert.Null(redacted["nothing"]);
    }

    [Fact]
    public void RedactValue_LeavesMalformedJsonAloneRatherThanThrowing()
    {
        var redacted = SensitiveDataRedactor.Redact(
            new Dictionary<string, object?> { ["detail"] = "{ not json" });

        Assert.Equal("{ not json", redacted["detail"]);
    }
}
