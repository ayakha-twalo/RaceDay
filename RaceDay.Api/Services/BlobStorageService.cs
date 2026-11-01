using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;

namespace RaceDay.Api.Services
{
    public class BlobStorageService
    {
        // Maximum upload size is 5 MB.
        private const long MaxFileSizeBytes = 5 * 1024 * 1024;

        // Image extensions accepted by the application.
        private static readonly string[] AllowedExtensions =
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp"
        };

        // Image content types accepted by the application.
        private static readonly string[] AllowedContentTypes =
        {
            "image/jpeg",
            "image/png",
            "image/gif",
            "image/webp"
        };

        private readonly IConfiguration _configuration;
        private readonly ILogger<BlobStorageService> _logger;

        public BlobStorageService(
            IConfiguration configuration,
            ILogger<BlobStorageService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        // Checks the uploaded image before it is sent to Azure.
        // Returns null when the image is valid.
        public string? Validate(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return "Please choose an image file to upload.";
            }

            if (file.Length > MaxFileSizeBytes)
            {
                return "The image must be 5 MB or smaller.";
            }

            string extension =
                Path.GetExtension(file.FileName).ToLowerInvariant();

            string contentType =
                (file.ContentType ?? string.Empty).ToLowerInvariant();

            if (!AllowedExtensions.Contains(extension) ||
                !AllowedContentTypes.Contains(contentType))
            {
                return "Only JPG, PNG, GIF or WEBP images are allowed.";
            }

            return null;
        }

        // Uploads the image to the specified existing Azure container.
        public async Task<string> UploadAsync(
            IFormFile file,
            string containerName)
        {
            try
            {
                // Read the connection string from User Secrets.
                string connectionString =
                    _configuration["AzureBlobStorage:ConnectionString"]
                    ?? throw new InvalidOperationException(
                        "AzureBlobStorage:ConnectionString is not configured.");

                // Connect to the Azure storage account.
                var blobServiceClient =
                    new BlobServiceClient(connectionString);

                // Select the container.
                var containerClient =
                    blobServiceClient.GetBlobContainerClient(containerName);

                // Check that the container exists.
                var exists = await containerClient.ExistsAsync();

                if (!exists.Value)
                {
                    throw new InvalidOperationException(
                        $"The Azure container '{containerName}' does not exist.");
                }

                // Generate a unique name for the image.
                string extension =
                    Path.GetExtension(file.FileName).ToLowerInvariant();

                string blobName = $"{Guid.NewGuid()}{extension}";

                // Create the blob reference.
                BlobClient blobClient =
                    containerClient.GetBlobClient(blobName);

                // Upload the file and preserve its content type.
                await using Stream stream = file.OpenReadStream();

                await blobClient.UploadAsync(
                    stream,
                    new BlobUploadOptions
                    {
                        HttpHeaders = new BlobHttpHeaders
                        {
                            ContentType = file.ContentType
                        }
                    });

                _logger.LogInformation(
                    "Image uploaded successfully to {ContainerName}.",
                    containerName);

                // Return the public image URL.
                return blobClient.Uri.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Azure Blob Storage image upload failed.");

                throw;
            }
        }
    }
}