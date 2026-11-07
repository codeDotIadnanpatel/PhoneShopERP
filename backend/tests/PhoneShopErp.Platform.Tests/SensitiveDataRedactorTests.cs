using System.Text.Json;
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
    [InlineData("api_key")]
    [InlineData("X-Api-Key")]
    [InlineData("apiKey")]
    [InlineData("connection_string")]
    [InlineData("DatabaseConnectionString")]
    [InlineData("private_key")]
    [InlineData("PrivateKey")]
    [InlineData("Set-Cookie")]
    [InlineData("pwd")]
    [InlineData("pin")]
    public void IsSensitive_RecognisesBaselineCredentialFields(string name)
    {
        Assert.True(SensitiveDataRedactor.IsSensitive(name));
    }

    [Theory]
    [InlineData("customer_name")]
    [InlineData("imei")]
    [InlineData("tenant_slug")]
    [InlineData("id")]
    [InlineData("api_version")]
    [InlineData("connection_id")]
    [InlineData("private_note")]
    [InlineData("shipping_address")]
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
    public void RedactJson_RedactsCredentialFieldsWhateverTheirCasingOrDepth()
    {
        const string json = """
            {
              "PASSWORD": "hunter2",
              "provider": {
                "Access_Token": "provider-token",
                "region": "eu-west-1",
                "api_client": { "apiKey": "sk-live-123" }
              },
              "purchases": [ { "Card-Number": "4111111111111111", "last_four": "1111" } ],
              "connection_string": "Host=db;Username=app;Password=hunter2",
              "private_key": "-----BEGIN PRIVATE KEY-----",
              "device": { "passcode": "4821" }
            }
            """;

        var redacted = SensitiveDataRedactor.RedactJson(json);

        foreach (var secret in new[]
                 {
                     "hunter2", "provider-token", "sk-live-123", "4111111111111111",
                     "Host=db", "BEGIN PRIVATE KEY", "4821",
                 })
        {
            Assert.DoesNotContain(secret, redacted, StringComparison.Ordinal);
        }

        using var document = JsonDocument.Parse(redacted);
        var root = document.RootElement;
        Assert.Equal("[REDACTED]", root.GetProperty("PASSWORD").GetString());
        Assert.Equal("[REDACTED]", root.GetProperty("provider").GetProperty("Access_Token").GetString());
        Assert.Equal("eu-west-1", root.GetProperty("provider").GetProperty("region").GetString());
        Assert.Equal(
            "[REDACTED]",
            root.GetProperty("provider").GetProperty("api_client").GetProperty("apiKey").GetString());
        Assert.Equal(
            "[REDACTED]",
            root.GetProperty("purchases")[0].GetProperty("Card-Number").GetString());
        Assert.Equal("1111", root.GetProperty("purchases")[0].GetProperty("last_four").GetString());
        Assert.Equal("[REDACTED]", root.GetProperty("connection_string").GetString());
        Assert.Equal("[REDACTED]", root.GetProperty("private_key").GetString());
        Assert.Equal(
            "[REDACTED]",
            root.GetProperty("device").GetProperty("passcode").GetString());
    }

    [Fact]
    public void RedactJson_RedactsJsonEncodedStringsAtAnyDepth()
    {
        const string json = """
            {
              "slug": "acme",
              "provider_response": "{\"refresh_token\":\"refresh-value\",\"safe\":\"kept\"}",
              "items": [ "{\"card_number\":\"4111111111111111\",\"sku\":\"SKU-1\"}" ]
            }
            """;

        var redacted = SensitiveDataRedactor.RedactJson(json);

        Assert.DoesNotContain("refresh-value", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("4111111111111111", redacted, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(redacted);
        var root = document.RootElement;
        Assert.Equal("acme", root.GetProperty("slug").GetString());

        using var provider = JsonDocument.Parse(root.GetProperty("provider_response").GetString()!);
        Assert.Equal("[REDACTED]", provider.RootElement.GetProperty("refresh_token").GetString());
        Assert.Equal("kept", provider.RootElement.GetProperty("safe").GetString());

        using var item = JsonDocument.Parse(root.GetProperty("items")[0].GetString()!);
        Assert.Equal("[REDACTED]", item.RootElement.GetProperty("card_number").GetString());
        Assert.Equal("SKU-1", item.RootElement.GetProperty("sku").GetString());
    }

    [Fact]
    public void RedactJson_LeavesMalformedEmbeddedJsonUnchanged()
    {
        const string json = """{ "note": "{ not json", "password": "hunter2" }""";

        var redacted = SensitiveDataRedactor.RedactJson(json);

        using var document = JsonDocument.Parse(redacted);
        Assert.Equal("{ not json", document.RootElement.GetProperty("note").GetString());
        Assert.Equal("[REDACTED]", document.RootElement.GetProperty("password").GetString());
        Assert.DoesNotContain("hunter2", redacted, StringComparison.Ordinal);
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
