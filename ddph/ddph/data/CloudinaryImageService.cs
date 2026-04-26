using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ddph.Data
{
    public sealed class CloudinaryImageService
    {
        private const string CloudName = "dgi195c7t";
        private const string UploadPreset = "dreamdough_products";
        private const string UploadFolder = "products";

        private static readonly HttpClient HttpClient = new();
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static async Task<string> UploadProductImageAsync(string imagePath)
        {
            await using var imageStream = File.OpenRead(imagePath);
            using var content = new MultipartFormDataContent();
            content.Add(CreateFormField("upload_preset", UploadPreset));
            content.Add(CreateFormField("folder", UploadFolder));
            content.Add(CreateFileField(imageStream, imagePath));

            using var response = await HttpClient
                .PostAsync($"https://api.cloudinary.com/v1_1/{CloudName}/image/upload", content)
                .ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Cloudinary upload failed with preset '{UploadPreset}' and status {(int)response.StatusCode}: {body}");
            }

            var uploadResponse = JsonSerializer.Deserialize<CloudinaryUploadResponse>(body, JsonOptions);
            if (string.IsNullOrWhiteSpace(uploadResponse?.SecureUrl))
            {
                throw new InvalidOperationException("Cloudinary did not return an image URL.");
            }

            return uploadResponse.SecureUrl;
        }

        private static HttpContent CreateFormField(string name, string value)
        {
            var content = new StringContent(value);
            content.Headers.ContentDisposition = null;
            content.Headers.TryAddWithoutValidation(
                "Content-Disposition",
                $"form-data; name=\"{name}\"");
            return content;
        }

        private static HttpContent CreateFileField(Stream imageStream, string imagePath)
        {
            var content = new StreamContent(imageStream);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(GetMimeType(imagePath));
            content.Headers.TryAddWithoutValidation(
                "Content-Disposition",
                $"form-data; name=\"file\"; filename=\"{Path.GetFileName(imagePath)}\"");
            return content;
        }

        private static string GetMimeType(string imagePath)
        {
            return Path.GetExtension(imagePath).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
        }

        private sealed class CloudinaryUploadResponse
        {
            [JsonPropertyName("secure_url")]
            public string? SecureUrl { get; set; }
        }
    }
}
