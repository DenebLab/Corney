using Corney.Common.Logging;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Corney.Common.Resilience;

/// <summary>
/// Resilient file reader with exponential backoff and circuit breaker
/// </summary>
public class ResilientFileReader
{
    private readonly RetryPolicy _retryPolicy;
    private readonly ILogger<ResilientFileReader> _logger;

    public ResilientFileReader(RetryPolicy retryPolicy, ILogger<ResilientFileReader> logger)
    {
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Read all lines from file with resilience
    /// </summary>
    public async Task<string[]> ReadAllLinesAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return await _retryPolicy.ExecuteAsync(
            async () =>
            {
                _logger.LogDebug("Reading file: {FilePath}", filePath);
                
                try
                {
                    var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
                    _logger.LogDebug("Successfully read {LineCount} lines from {FilePath}", lines.Length, filePath);
                    return lines;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read file: {FilePath}", filePath);
                    throw;
                }
            },
            $"ReadFile:{Path.GetFileName(filePath)}",
            IsRetryableFileException,
            cancellationToken);
    }

    /// <summary>
    /// Read all lines from file with resilience (synchronous)
    /// </summary>
    public string[] ReadAllLines(string filePath)
    {
        return _retryPolicy.Execute(
            () =>
            {
                _logger.LogDebug("Reading file: {FilePath}", filePath);
                
                try
                {
                    var lines = File.ReadAllLines(filePath);
                    _logger.LogDebug("Successfully read {LineCount} lines from {FilePath}", lines.Length, filePath);
                    return lines;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read file: {FilePath}", filePath);
                    throw;
                }
            },
            $"ReadFile:{Path.GetFileName(filePath)}",
            IsRetryableFileException);
    }

    /// <summary>
    /// Check if file exists with resilience
    /// </summary>
    public async Task<bool> ExistsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return await _retryPolicy.ExecuteAsync(
            async () =>
            {
                await Task.Yield(); // Make it async
                return File.Exists(filePath);
            },
            $"FileExists:{Path.GetFileName(filePath)}",
            IsRetryableFileException,
            cancellationToken);
    }

    /// <summary>
    /// Read all text from file with resilience
    /// </summary>
    public async Task<string> ReadAllTextAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return await _retryPolicy.ExecuteAsync(
            async () =>
            {
                _logger.LogDebug("Reading text from file: {FilePath}", filePath);
                
                try
                {
                    var text = await File.ReadAllTextAsync(filePath, cancellationToken);
                    _logger.LogDebug("Successfully read {ByteCount} bytes from {FilePath}", text.Length, filePath);
                    return text;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read text from file: {FilePath}", filePath);
                    throw;
                }
            },
            $"ReadText:{Path.GetFileName(filePath)}",
            IsRetryableFileException,
            cancellationToken);
    }

    /// <summary>
    /// Determine if file exception is retryable
    /// </summary>
    private static bool IsRetryableFileException(Exception ex)
    {
        return ex switch
        {
            FileNotFoundException => false,  // Don't retry if file doesn't exist
            DirectoryNotFoundException => false,  // Don't retry if directory doesn't exist
            UnauthorizedAccessException => false,  // Don't retry permission issues
            PathTooLongException => false,  // Don't retry path issues
            ArgumentException => false,  // Don't retry invalid arguments
            NotSupportedException => false,  // Don't retry unsupported operations
            IOException => true,  // Retry I/O issues (file locked, etc.)
            _ => true  // Retry other exceptions by default
        };
    }
}

/// <summary>
/// Factory for creating resilient file readers with different configurations
/// </summary>
public static class ResilientFileReaderFactory
{
    /// <summary>
    /// Create file reader with conservative retry policy
    /// </summary>
    public static ResilientFileReader CreateConservative(ILogger<ResilientFileReader> logger)
    {
        var options = new RetryPolicyOptions
        {
            MaxAttempts = 3,
            BaseDelay = TimeSpan.FromMilliseconds(100),
            BackoffMultiplier = 1.5,
            MaxDelay = TimeSpan.FromSeconds(5),
            UseJitter = true
        };

        var loggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
        var retryPolicy = new RetryPolicy(options, loggerFactory.CreateLogger<RetryPolicy>());
        
        return new ResilientFileReader(retryPolicy, logger);
    }

    /// <summary>
    /// Create file reader with aggressive retry policy for critical files
    /// </summary>
    public static ResilientFileReader CreateAggressive(ILogger<ResilientFileReader> logger)
    {
        var options = new RetryPolicyOptions
        {
            MaxAttempts = 10,
            BaseDelay = TimeSpan.FromMilliseconds(300),
            BackoffMultiplier = 2.0,
            MaxDelay = TimeSpan.FromSeconds(30),
            UseJitter = true
        };

        var loggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
        var retryPolicy = new RetryPolicy(options, loggerFactory.CreateLogger<RetryPolicy>());
        
        return new ResilientFileReader(retryPolicy, logger);
    }
}