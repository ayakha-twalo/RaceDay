namespace RaceDay.Contracts.DTOs.Profile
{
    public class ProfileResponseDto
    {
        public int UserId { get; set; }

    public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }

        public string Role { get; set; } = string.Empty;

        // Azure URL of the user's profile picture.
        // This will remain null until a picture is uploaded.
        public string? ProfileImageUrl { get; set; }
    }

}
