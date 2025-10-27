namespace FoMed.Api.Settings;

public class FileStorageSettings
{
    public string Provider { get; set; } = "AzureBlob"; // "AzureBlob", "AWS", "Local"
    public string ConnectionString { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "uploads";
    public string BaseUrl { get; set; } = string.Empty; // URL công khai 
    public int MaxFileSizeMB { get; set; } = 5;
    public string[] AllowedExtensions { get; set; } = new[]
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp"
    };
    public string[] AllowedMimeTypes { get; set; } = new[]
    {
        "image/jpeg", "image/png", "image/gif", "image/webp"
    };
}