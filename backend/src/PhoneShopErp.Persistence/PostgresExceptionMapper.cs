using Npgsql;
using PhoneShopErp.Platform.Errors;

namespace PhoneShopErp.Persistence;

/// <summary>
/// Converts PostgreSQL failures into safe application failures. No SQL text,
/// constraint definition or database message is returned to a client.
/// </summary>
public static class PostgresExceptionMapper
{
    public static PlatformException ToPlatformException(PostgresException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.SqlState switch
        {
            // insufficient_privilege: RLS, append-only trigger or a revoked grant.
            PostgresErrorCodes.InsufficientPrivilege => new PlatformException(
                ErrorCodes.Forbidden,
                "The operation is not permitted.",
                403,
                exception),

            // unique_violation
            PostgresErrorCodes.UniqueViolation => new PlatformException(
                ErrorCodes.Conflict,
                "The request conflicts with an existing record.",
                409,
                exception),

            // foreign_key_violation
            PostgresErrorCodes.ForeignKeyViolation => new PlatformException(
                ErrorCodes.ValidationFailed,
                "A referenced record does not exist in this tenant.",
                400,
                exception),

            // check_violation
            PostgresErrorCodes.CheckViolation => new PlatformException(
                ErrorCodes.ValidationFailed,
                "The request violates a data constraint.",
                400,
                exception),

            // invalid_text_representation / datetime_field_overflow
            PostgresErrorCodes.InvalidTextRepresentation => new PlatformException(
                ErrorCodes.ValidationFailed,
                "A value has an invalid format.",
                400,
                exception),

            PostgresErrorCodes.DatetimeFieldOverflow => new PlatformException(
                ErrorCodes.ValidationFailed,
                "A timestamp is outside the supported range.",
                400,
                exception),

            // serialization_failure / deadlock_detected
            PostgresErrorCodes.SerializationFailure => new PlatformException(
                ErrorCodes.ConcurrencyConflict,
                "The record changed concurrently. Retry with the current version.",
                409,
                exception),

            PostgresErrorCodes.DeadlockDetected => new PlatformException(
                ErrorCodes.ConcurrencyConflict,
                "The request could not be completed because of a concurrent update. Retry.",
                409,
                exception),

            // lock_not_available / query_canceled / admin_shutdown / cannot_connect_now
            PostgresErrorCodes.LockNotAvailable => new PlatformException(
                ErrorCodes.ConcurrencyConflict,
                "The record is locked by another transaction. Retry.",
                409,
                exception),

            PostgresErrorCodes.QueryCanceled => new PlatformException(
                ErrorCodes.DependencyUnavailable,
                "The database did not complete the request in time.",
                503,
                exception),

            PostgresErrorCodes.AdminShutdown => new PlatformException(
                ErrorCodes.DependencyUnavailable,
                "The database is unavailable.",
                503,
                exception),

            PostgresErrorCodes.CannotConnectNow => new PlatformException(
                ErrorCodes.DependencyUnavailable,
                "The database is unavailable.",
                503,
                exception),

            _ => new PlatformException(
                ErrorCodes.InternalError,
                "The request could not be completed.",
                500,
                exception),
        };
    }

    public static PlatformException ToPlatformException(NpgsqlException exception) =>
        new(
            ErrorCodes.DependencyUnavailable,
            "The database is unavailable.",
            503,
            exception);
}
