namespace Kairos.Api.Common.Exceptions;

public class UnauthenticatedException: Exception
{
    public UnauthenticatedException()
        : base("You are not unauthenticated.")
    {
    }

    public UnauthenticatedException(string message)
        : base(message)
    {
    }

    public UnauthenticatedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}