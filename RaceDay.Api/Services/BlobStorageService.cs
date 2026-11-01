using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;

namespace RaceDay.Api.Services
{
    public class BlobStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ILogger<BlobStorageService> _logger;

        // Maximum image size: 5 MB.
        private const long MaxFileSize = 5 * 1024 * 1024;

        // Only these image extensions are accepted.
        private readonly string[] allowedExtensions =
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".gif",
            ".webp"
        };

        // Only these image types are accepted.
        private readonly string[] allowedContentTypes =
        {
            "image/jpeg",
            "image/png",
            "image/gif",
            "image/webp"
        };

        // Constructor: connects the service to Azure Blob Storage.
        public BlobStorageService(
            IConfiguration configuration,
            ILogger<BlobStorageService> logger)
        {
            _logger = logger;

            // Read the Azure connection string from User Secrets.
            string? connectionString =
                configuration["AzureBlobStorage:ConnectionString"];

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Azure Blob Storage connection string is missing. " +
                    "Check your User Secrets settings.");
            }

            _blobServiceClient = new BlobServiceClient(connectionString);
        }

        // Checks whether an uploaded image is valid.
        public string? ValidateImage(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return "Please select an image to upload.";
            }

            if (file.Length > MaxFileSize)
            {
                return "The image must not be larger than 5 MB.";
            }

            string extension =
                Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                return "Only JPG, JPEG, PNG, GIF and WEBP images are allowed.";
            }

            if (!allowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                return "The uploaded file must be a valid image type.";
            }

            return null;
        }

        // Uploads an image to the selected Azure container.
        public async Task<string> UploadImageAsync(
            IFormFile file,
            string containerName)
        {
            // Check the image before uploading.
            string? validationError = ValidateImage(file);

            if (validationError != null)
            {
                throw new ArgumentException(validationError);
            }

            // Get the Azure container.
            BlobContainerClient containerClient =
                _blobServiceClient.GetBlobContainerClient(containerName);

            // Check that the container already exists.
            var containerExists = await containerClient.ExistsAsync();

            if (!containerExists.Value)
            {
                throw new InvalidOperationException(
                    $"The Azure container '{containerName}' does not exist.");
            }

            // Give the image a unique name to avoid overwriting other images.
            string extension =
                Path.GetExtension(file.FileName).ToLowerInvariant();

            string blobName = $"{Guid.NewGuid()}{extension}";

            // Create a reference to the image in Azure.
            BlobClient blobClient =
                containerClient.GetBlobClient(blobName);

            // Upload the image.
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
                "Image successfully uploaded to Azure container {ContainerName}.",
                containerName);

            // Return the image's public URL.
            return blobClient.Uri.ToString();
        }
    }
}