using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Corney.Features.App;

/// <summary>
/// Provides validation capabilities for configuration objects
/// </summary>
public static class ConfigurationValidator
{
    /// <summary>
    /// Validates configuration using data annotations and custom business rules
    /// </summary>
    public static ConfigurationValidationResult ValidateConfiguration(
        CorneyConfig config, 
        ILogger logger)
    {
        var result = new ConfigurationValidationResult();
        var validationContext = new ValidationContext(config);
        var validationResults = new List<ValidationResult>();

        // Validate using data annotations
        var isValid = Validator.TryValidateObject(config, validationContext, validationResults, true);
        
        if (!isValid)
        {
            foreach (var validationResult in validationResults)
            {
                var error = new ConfigurationError
                {
                    PropertyName = validationResult.MemberNames.FirstOrDefault() ?? "Unknown",
                    ErrorMessage = validationResult.ErrorMessage ?? "Validation failed",
                    ErrorType = ConfigurationErrorType.ValidationAttribute
                };
                result.Errors.Add(error);
            }
        }

        // Custom business rule validations
        ValidateCrontabFiles(config, result, logger);
        ValidateIntervalSettings(config, result);

        result.IsValid = result.Errors.Count == 0;
        return result;
    }

    private static void ValidateCrontabFiles(CorneyConfig config, ConfigurationValidationResult result, ILogger logger)
    {
        if (config.CrontabFiles == null || config.CrontabFiles.Length == 0)
        {
            return; // Already caught by Required attribute
        }

        for (int i = 0; i < config.CrontabFiles.Length; i++)
        {
            var filePath = config.CrontabFiles[i];
            
            // Check for null or empty paths
            if (string.IsNullOrWhiteSpace(filePath))
            {
                result.Errors.Add(new ConfigurationError
                {
                    PropertyName = $"CrontabFiles[{i}]",
                    ErrorMessage = "Crontab file path cannot be null or empty",
                    ErrorType = ConfigurationErrorType.BusinessRule
                });
                continue;
            }

            // Validate file path format
            try
            {
                var fullPath = Path.GetFullPath(filePath);
                config.CrontabFiles[i] = fullPath; // Normalize the path
            }
            catch (Exception ex)
            {
                result.Errors.Add(new ConfigurationError
                {
                    PropertyName = $"CrontabFiles[{i}]",
                    ErrorMessage = $"Invalid file path format: {ex.Message}",
                    ErrorType = ConfigurationErrorType.BusinessRule
                });
                continue;
            }

            // Check if parent directory exists (file itself doesn't need to exist yet)
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                result.Warnings.Add(new ConfigurationWarning
                {
                    PropertyName = $"CrontabFiles[{i}]",
                    WarningMessage = $"Directory does not exist: {directory}. It will be created if needed.",
                    WarningType = ConfigurationWarningType.MissingDirectory
                });
            }

            // Check for duplicate file paths
            for (int j = i + 1; j < config.CrontabFiles.Length; j++)
            {
                if (string.Equals(config.CrontabFiles[i], config.CrontabFiles[j], StringComparison.OrdinalIgnoreCase))
                {
                    result.Errors.Add(new ConfigurationError
                    {
                        PropertyName = $"CrontabFiles[{j}]",
                        ErrorMessage = $"Duplicate crontab file path: {filePath}",
                        ErrorType = ConfigurationErrorType.BusinessRule
                    });
                }
            }
        }
    }

    private static void ValidateIntervalSettings(CorneyConfig config, ConfigurationValidationResult result)
    {
        // Check if debounce is not too close to check interval
        if (config.FileMonitoringDebounceSeconds >= config.CheckIntervalSeconds)
        {
            result.Warnings.Add(new ConfigurationWarning
            {
                PropertyName = nameof(config.FileMonitoringDebounceSeconds),
                WarningMessage = "File monitoring debounce should be less than check interval for optimal performance",
                WarningType = ConfigurationWarningType.PerformanceImpact
            });
        }

        // Validate reasonable ranges
        if (config.CheckIntervalSeconds < 5)
        {
            result.Warnings.Add(new ConfigurationWarning
            {
                PropertyName = nameof(config.CheckIntervalSeconds),
                WarningMessage = "Very short check intervals may impact system performance",
                WarningType = ConfigurationWarningType.PerformanceImpact
            });
        }
    }
}

/// <summary>
/// Result of configuration validation
/// </summary>
public class ConfigurationValidationResult
{
    public bool IsValid { get; set; } = true;
    public List<ConfigurationError> Errors { get; set; } = new();
    public List<ConfigurationWarning> Warnings { get; set; } = new();

    public string GetFormattedErrorMessage()
    {
        if (IsValid)
            return string.Empty;

        var messages = new List<string>();
        
        if (Errors.Count > 0)
        {
            messages.Add("Configuration Errors:");
            foreach (var error in Errors)
            {
                messages.Add($"  • {error.PropertyName}: {error.ErrorMessage}");
            }
        }

        if (Warnings.Count > 0)
        {
            messages.Add("Configuration Warnings:");
            foreach (var warning in Warnings)
            {
                messages.Add($"  • {warning.PropertyName}: {warning.WarningMessage}");
            }
        }

        return string.Join(Environment.NewLine, messages);
    }
}

/// <summary>
/// Configuration validation error
/// </summary>
public class ConfigurationError
{
    public string PropertyName { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public ConfigurationErrorType ErrorType { get; set; }
}

/// <summary>
/// Configuration validation warning
/// </summary>
public class ConfigurationWarning
{
    public string PropertyName { get; set; } = string.Empty;
    public string WarningMessage { get; set; } = string.Empty;
    public ConfigurationWarningType WarningType { get; set; }
}

/// <summary>
/// Types of configuration errors
/// </summary>
public enum ConfigurationErrorType
{
    ValidationAttribute,
    BusinessRule,
    FileSystem,
    Security
}

/// <summary>
/// Types of configuration warnings
/// </summary>
public enum ConfigurationWarningType
{
    MissingDirectory,
    PerformanceImpact,
    Recommendation
}