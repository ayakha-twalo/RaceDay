namespace RaceDay.Mvc.Services;

// The outcome of one API call. Controllers read this instead of
// dealing with raw HTTP responses, so users never see raw errors.
public class ApiResult
{
    public bool Success { get; init; }

    // HTTP status code (0 = the API could not be reached at all).
    public int StatusCode { get; init; }

    // A friendly, user-safe message when Success is false.
    public string? Error { get; init; }

    // The API's session cookie, if this response set one (login only).
    public string? SessionCookie { get; init; }

    public bool IsUnauthorized => StatusCode == 401;
}

// Same as ApiResult, plus the deserialised response body.
public class ApiResult<T> : ApiResult
{
    public T? Data { get; init; }
}