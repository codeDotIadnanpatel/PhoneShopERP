using Npgsql;
using Xunit;

namespace PhoneShopErp.Database.Tests;

public static class PostgresAssert
{
    /// <summary>
    /// Runs a statement as the given role and asserts PostgreSQL rejected it with
    /// one of the expected SQLSTATE codes. 42501 is insufficient privilege
    /// (including an RLS violation), 23514 a check constraint.
    /// </summary>
    public static async Task<PostgresException> RejectsAsync(
        TestDatabase database,
        string sql,
        string role,
        params string[] expectedStates)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => database.ExecuteAsync(sql, role));

        Assert.Contains(exception.SqlState, expectedStates);
        return exception;
    }

    public static async Task<PostgresException> RejectsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string sql,
        params string[] expectedStates)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => command.ExecuteNonQueryAsync());

        Assert.Contains(exception.SqlState, expectedStates);
        return exception;
    }
}
