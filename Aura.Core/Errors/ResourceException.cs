using System;

namespace Aura.Core.Errors;

/// <summary>
/// Exception thrown when a resource-related error occurs (disk space, memory, file access, etc.)
/// </summary>
public class ResourceException : AuraException
{
    /// <summary>
    /// Type of resource that caused the error
    /// </summary>
    public ResourceType ResourceType { get; }

    /// <summary>
    /// Path to the resource if applicable (file path, directory path)
    /// </summary>
    public string? ResourcePath { get; }

    /// <summary>
    /// Amount of resource required (bytes for disk/memory)
    /// </summary>
    public long Required { get; }

    /// <summary>
    /// Amount of resource available (bytes for disk/memory)
    /// </summary>
    public long Available { get; }

    public ResourceException(
        ResourceType resourceType,
        string message,
        long required = 0,
        long available = 0,
        string? userMessage = null,
        string? resourcePath = null,
        string? correlationId = null,
        string[]? suggestedActions = null,
        Exception? innerException = null)
        : base(
            message,
            GenerateErrorCode(resourceType),
            userMessage ?? GenerateUserMessage(resourceType, message),
            correlationId,
            suggestedActions ?? GenerateDefaultSuggestedActions(resourceType),
            isTransient: false,
            innerException)
    {
        ResourceType = resourceType;
        ResourcePath = resourcePath;
        Required = required;
        Available = available;

        // Add resource context
        WithContext("resourceType", resourceType.ToString());
        if (!string.IsNullOrEmpty(resourcePath))
        {
            WithContext("resourcePath", resourcePath);
        }
        if (required > 0)
        {
            WithContext("requiredBytes", required);
            WithContext("requiredMB", required / (1024 * 1024));
        }
        if (available > 0)
        {
            WithContext("availableBytes", available);
            WithContext("availableMB", available / (1024 * 1024));
        }
    }

    private static string GenerateErrorCode(ResourceType resourceType)
    {
        return resourceType switch
        {
            ResourceType.Disk or ResourceType.DiskSpace => "E601",
            ResourceType.Memory => "E602",
            ResourceType.GPU => "E603",
            ResourceType.Network => "E604",
            ResourceType.FileAccess => "E605",
            ResourceType.DirectoryAccess => "E606",
            ResourceType.FileNotFound => "E607",
            ResourceType.FileLocked => "E608",
            ResourceType.FileCorrupted => "E609",
            _ => "E699"
        };
    }

    private static string GenerateUserMessage(ResourceType resourceType, string message)
    {
        return resourceType switch
        {
            ResourceType.Disk or ResourceType.DiskSpace => "Insufficient disk space to complete the operation.",
            ResourceType.Memory => "Insufficient memory to complete the operation.",
            ResourceType.GPU => "GPU resources unavailable or insufficient.",
            ResourceType.Network => "Network resources unavailable.",
            ResourceType.FileAccess => "Unable to access the required file.",
            ResourceType.DirectoryAccess => "Unable to access the required directory.",
            ResourceType.FileNotFound => "Required file not found.",
            ResourceType.FileLocked => "File is locked by another process.",
            ResourceType.FileCorrupted => "File appears to be corrupted or invalid.",
            _ => message
        };
    }

    private static string[] GenerateDefaultSuggestedActions(ResourceType resourceType)
    {
        return resourceType switch
        {
            ResourceType.Disk or ResourceType.DiskSpace => new[]
            {
                "Free up disk space and retry",
                "Choose a different output location with more space",
                "Delete temporary files or old projects"
            },
            ResourceType.Memory => new[]
            {
                "Close other applications to free up memory",
                "Reduce video quality or resolution",
                "Try with shorter content",
                "Restart the application"
            },
            ResourceType.GPU => new[]
            {
                "Ensure GPU drivers are up to date",
                "Close other GPU-intensive applications",
                "Try using CPU rendering instead",
                "Reduce video quality settings"
            },
            ResourceType.Network => new[]
            {
                "Check your internet connection",
                "Verify network settings",
                "Try again later",
                "Use offline providers if available"
            },
            ResourceType.FileAccess or ResourceType.DirectoryAccess => new[]
            {
                "Check file/directory permissions",
                "Ensure the path is accessible",
                "Run the application with appropriate privileges"
            },
            ResourceType.FileNotFound => new[]
            {
                "Verify the file exists at the expected location",
                "Check if the file was moved or deleted",
                "Regenerate the file if possible"
            },
            ResourceType.FileLocked => new[]
            {
                "Close any applications using the file",
                "Wait a moment and retry",
                "Restart the application if the issue persists"
            },
            ResourceType.FileCorrupted => new[]
            {
                "Regenerate the file",
                "Verify the source data is valid",
                "Check disk health if corruption occurs frequently"
            },
            _ => new[] { "Review the error details and retry" }
        };
    }

    /// <summary>
    /// Creates a ResourceException for disk space errors
    /// </summary>
    public static ResourceException InsufficientDiskSpace(string? path = null, long? requiredBytes = null, long? availableBytes = null, string? correlationId = null)
    {
        var message = requiredBytes.HasValue
            ? $"Insufficient disk space. Required: {requiredBytes / (1024 * 1024)} MB"
            : "Insufficient disk space to complete the operation.";

        return new ResourceException(
            ResourceType.DiskSpace,
            message,
            requiredBytes ?? 0,
            availableBytes ?? 0,
            resourcePath: path,
            correlationId: correlationId);
    }

    /// <summary>
    /// Creates a ResourceException for memory errors
    /// </summary>
    public static ResourceException InsufficientMemory(long? requiredBytes = null, long? availableBytes = null, string? correlationId = null)
    {
        var message = requiredBytes.HasValue
            ? $"Insufficient memory. Required: {requiredBytes / (1024 * 1024)} MB"
            : "Insufficient memory to complete the operation.";

        return new ResourceException(
            ResourceType.Memory,
            message,
            requiredBytes ?? 0,
            availableBytes ?? 0,
            correlationId: correlationId);
    }

    /// <summary>
    /// Creates a ResourceException for file access errors
    /// </summary>
    public static ResourceException FileAccessDenied(string path, string? correlationId = null, Exception? innerException = null)
    {
        return new ResourceException(
            ResourceType.FileAccess,
            $"Access denied to file: {path}",
            resourcePath: path,
            correlationId: correlationId,
            innerException: innerException);
    }

    /// <summary>
    /// Creates a ResourceException for directory access errors
    /// </summary>
    public static ResourceException DirectoryAccessDenied(string path, string? correlationId = null, Exception? innerException = null)
    {
        return new ResourceException(
            ResourceType.DirectoryAccess,
            $"Access denied to directory: {path}",
            resourcePath: path,
            correlationId: correlationId,
            innerException: innerException);
    }

    /// <summary>
    /// Creates a ResourceException for file not found errors
    /// </summary>
    public static ResourceException FileNotFound(string path, string? correlationId = null)
    {
        return new ResourceException(
            ResourceType.FileNotFound,
            $"File not found: {path}",
            resourcePath: path,
            correlationId: correlationId);
    }

    /// <summary>
    /// Creates a ResourceException for file locked errors
    /// </summary>
    public static ResourceException FileLocked(string path, string? correlationId = null)
    {
        return new ResourceException(
            ResourceType.FileLocked,
            $"File is locked by another process: {path}",
            resourcePath: path,
            correlationId: correlationId);
    }

    public override Dictionary<string, object> ToErrorResponse()
    {
        var response = base.ToErrorResponse();
        response["resourceType"] = ResourceType.ToString();
        if (!string.IsNullOrEmpty(ResourcePath))
        {
            response["resourcePath"] = ResourcePath;
        }
        if (Required > 0)
        {
            response["required"] = new { bytes = Required, mb = Required / (1024 * 1024) };
        }
        if (Available > 0)
        {
            response["available"] = new { bytes = Available, mb = Available / (1024 * 1024) };
        }
        return response;
    }
}

/// <summary>
/// Types of resources that can cause errors
/// </summary>
public enum ResourceType
{
    Disk,
    DiskSpace,
    Memory,
    GPU,
    Network,
    FileAccess,
    DirectoryAccess,
    FileNotFound,
    FileLocked,
    FileCorrupted
}
