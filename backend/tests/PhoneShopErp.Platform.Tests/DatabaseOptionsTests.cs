using PhoneShopErp.Persistence;
using Xunit;

namespace PhoneShopErp.Platform.Tests;

public sealed class DatabaseOptionsTests
{
    private static DatabaseOptions Options(string connectionString) =>
        new() { ConnectionString = connectionString };

    [Fact]
    public void Validate_AcceptsExplicitErpAppCredential()
    {
        var options = Options("Host=localhost;Database=phoneshoperp;Username=erp_app;Password=secret");

        options.Validate();
    }

    [Theory]
    [InlineData("erp_owner")]
    [InlineData("erp_migrator")]
    [InlineData("erp_platform_admin")]
    [InlineData("postgres")]
    [InlineData("ERP_APP")]
    public void Validate_RejectsEveryOtherCredential(string username)
    {
        var options = Options($"Host=localhost;Database=phoneshoperp;Username={username};Password=secret");

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);
        Assert.Contains("erp_app", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsAUsernameThatOnlyComesFromTheEnvironment()
    {
        var options = Options("Host=localhost;Database=phoneshoperp");

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);
        Assert.Contains("PGUSER", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsAnEmptyConnectionString()
    {
        Assert.Throws<InvalidOperationException>(Options(string.Empty).Validate);
        Assert.Throws<InvalidOperationException>(Options("   ").Validate);
    }

    [Fact]
    public void Validate_RejectsAMalformedConnectionString()
    {
        Assert.Throws<InvalidOperationException>(Options("this is not a connection string").Validate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public void Validate_BoundsThePoolSize(int poolSize)
    {
        var options = Options("Host=localhost;Database=phoneshoperp;Username=erp_app;Password=secret");
        options.MaximumPoolSize = poolSize;

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(601)]
    public void Validate_BoundsTheCommandTimeout(int timeoutSeconds)
    {
        var options = Options("Host=localhost;Database=phoneshoperp;Username=erp_app;Password=secret");
        options.CommandTimeoutSeconds = timeoutSeconds;

        Assert.Throws<InvalidOperationException>(options.Validate);
    }
}
