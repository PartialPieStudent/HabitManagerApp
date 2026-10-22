using System.Net;

namespace Habits.Mobile.Services.Api;

public sealed class ApiException : Exception
{
    public ApiException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}

public sealed class ApiUnavailableException : Exception
{
    public ApiUnavailableException(Exception innerException)
        : base("Serveris nav sasniedzams. Pārbaudiet, vai API ir palaists, un mēģiniet vēlreiz.", innerException)
    {
    }
}
