namespace PhoneShopErp.Platform.Tenancy;

/// <summary>
/// Ambient access to the immutable tenant context for the current async flow.
/// The context is established exactly once by the request pipeline and cleared
/// when the scope ends. Nothing in a module may set it directly.
/// </summary>
public static class TenantContextAccessor
{
    private static readonly AsyncLocal<Holder?> Ambient = new();

    public static TenantContext? Current => Ambient.Value?.Context;

    public static bool IsResolved => Ambient.Value is not null;

    /// <summary>
    /// Throws when no context has been resolved. Used by data access so a missing
    /// context can never degrade into an unscoped query.
    /// </summary>
    public static TenantContext Require() =>
        Ambient.Value?.Context
        ?? throw new InvalidOperationException(
            "Tenant context has not been resolved for this request.");

    /// <summary>
    /// Establishes context for the current async flow. Throws if a context is
    /// already present, which prevents accidental scope replacement.
    /// </summary>
    public static IDisposable Begin(TenantContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Ambient.Value is not null)
        {
            throw new InvalidOperationException(
                "Tenant context is already resolved and cannot be replaced in place. " +
                "Tenant switching must create a new application session.");
        }

        Ambient.Value = new Holder(context);
        return new Scope();
    }

    private sealed class Holder
    {
        public Holder(TenantContext context) => Context = context;

        public TenantContext Context { get; }
    }

    private sealed class Scope : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            Ambient.Value = null;
        }
    }
}
