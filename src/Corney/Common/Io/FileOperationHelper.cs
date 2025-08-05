using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;

namespace Corney.Common.Io;

public interface IFileOperationHelper
{
    bool OpenFileInDefaultEditor(string filePath);
    bool FileExists(string filePath);
}

public class FileOperationHelper : IFileOperationHelper
{
    private readonly ILogger<FileOperationHelper> _logger;

    public FileOperationHelper(ILogger<FileOperationHelper> logger)
    {
        _logger = logger;
    }

    public bool OpenFileInDefaultEditor(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                _logger.LogWarning("Cannot open file: path is null or empty");
                return false;
            }

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Cannot open file: {FilePath} does not exist", filePath);
                return false;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            };

            using var process = Process.Start(startInfo);
            _logger.LogInformation("Successfully opened file: {FilePath}", filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open file: {FilePath}", filePath);
            return false;
        }
    }

    public bool FileExists(string filePath)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking file existence: {FilePath}", filePath);
            return false;
        }
    }
}