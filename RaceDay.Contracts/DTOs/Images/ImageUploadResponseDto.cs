namespace RaceDay.Contracts.DTOs.Images
{
    // This DTO returns the image URL after a successful upload.
    public class ImageUploadResponseDto
    {
        public string ImageUrl { get; set; } = string.Empty;
    }
}
