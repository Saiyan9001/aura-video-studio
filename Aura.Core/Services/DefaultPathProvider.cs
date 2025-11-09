using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Aura.Core.Services;

/// <summary>
/// Provides platform-specific default paths for the application
/// </summary>
public class DefaultPathProvider
{
    /// <summary>
    /// Gets the default save location for videos based on the platform
    /// </summary>
    /// <returns>Platform-appropriate default save location</returns>
    public static string GetDefaultSaveLocation()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(userProfile))
                {
                    return Path.Combine(userProfile, "Videos", "Aura");
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(home))
                {
                    return Path.Combine(home, "Movies", "Aura");
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(home))
                {
                    return Path.Combine(home, "Videos", "Aura");
                }
            }

            var fallback = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(fallback, "Aura", "Videos");
        }
        catch
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "output");
        }
    }

    /// <summary>
    /// Ensures the default save location directory exists
    /// </summary>
    /// <param name="path">Path to create</param>
    /// <returns>True if directory exists or was created successfully</returns>
    public static bool EnsureDirectoryExists(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            if (Directory.Exists(path))
            {
                return true;
            }

            Directory.CreateDirectory(path);
            return Directory.Exists(path);
        }
        catch
        {
            return false;
        }
    }
}
