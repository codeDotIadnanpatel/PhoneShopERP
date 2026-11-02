namespace PhoneShopErp.Platform.Errors;

/// <summary>
/// A deliberate, safe application failure. The message is safe to return to a
/// client: it must not contain a token, secret, SQL statement or stack detail.
/// </summary>
public sealed class PlatformException : Exception
{
    public PlatformException(string code, string message, int statusCode = 400)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public PlatformException(string code, string message, int statusCode, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }

    public int StatusCode { get; }
}
