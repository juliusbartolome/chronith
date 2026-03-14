using System.Net;

namespace Chronith.Client.Errors;

/// <summary>
/// Thrown when the Chronith API returns a non-success status code.
/// </summary>
public sealed class ChronithApiException : Exception
{
    /// <summary>The HTTP status code returned by the API.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>The raw response body from the API.</summary>
    public string ResponseBody { get; }

    public ChronithApiException(HttpStatusCode statusCode, string responseBody)
        : base($"Chronith API error {(int)statusCode}: {responseBody}")
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
