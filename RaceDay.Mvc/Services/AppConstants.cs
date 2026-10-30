namespace RaceDay.Mvc.Services;

// Keys used to store values in the MVC session.
// Constants avoid typos like "UserID" vs "UserId".
public static class SessionKeys
{
    public const string UserId = "UserId";
    public const string Role = "Role";
    public const string DisplayName = "DisplayName";

    // The API's own session cookie (name=value), kept server-side
    // so we can send it back on every API call.
    public const string ApiCookie = "ApiSessionCookie";
}

// The two role names exactly as the API returns them.
public static class UserRoles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
}