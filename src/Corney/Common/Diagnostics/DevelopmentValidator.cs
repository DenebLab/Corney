using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Corney.Features.App;
using Microsoft.Extensions.Logging;

namespace Corney.Common.Diagnostics;

/// <summary>
/// Provides development-time validation and diagnostics for Corney configuration and environment.
/// This helps developers catch configuration issues early and understand system behavior.
/// </summary>
public class DevelopmentValidator
{
    private readonly ILogger<DevelopmentValidator> _logger;
    private readonly List<ValidationResult> _validationResults = new();

    public DevelopmentValidator(ILogger<DevelopmentValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all validation results from the last validation run.
    /// </summary>
    public IReadOnlyList<ValidationResult> ValidationResults => _validationResults.AsReadOnly();

    /// <summary>
    /// Performs comprehensive validation of the development environment.
    /// </summary>
    /// <param name="configPath">Path to the configuration file</param>
    /// <param name="config">The configuration object to validate</param>
    /// <returns>True if all validations pass, false otherwise</returns>
    public bool ValidateDevelopmentEnvironment(string configPath, CorneyConfig config)
    {
        using var correlationContext = CorrelationContext.StartOperation("DevelopmentValidation");
        
        _validationResults.Clear();
        _logger.LogInformation("Starting development environment validation...");

        // Validate configuration file
        ValidateConfigurationFile(configPath, config);
        
        // Validate crontab files
        ValidateCrontabFiles(config);
        
        // Validate development environment
        ValidateDevelopmentEnvironment();
        
        // Validate permissions
        ValidatePermissions(config);
        
        // Generate validation report
        GenerateValidationReport();

        var hasErrors = _validationResults.Any(r => r.Severity == ValidationSeverity.Error);
        var hasWarnings = _validationResults.Any(r => r.Severity == ValidationSeverity.Warning);

        if (hasErrors)
        {
            _logger.LogError("Development environment validation failed with {ErrorCount} errors", 
                _validationResults.Count(r => r.Severity == ValidationSeverity.Error));
        }
        else if (hasWarnings)
        {
            _logger.LogWarning("Development environment validation completed with {WarningCount} warnings", 
                _validationResults.Count(r => r.Severity == ValidationSeverity.Warning));
        }
        else
        {
            _logger.LogInformation("Development environment validation passed successfully");
        }

        return !hasErrors;
    }

    private void ValidateConfigurationFile(string configPath, CorneyConfig config)
    {
        AddValidationStep("Configuration File Validation");

        if (string.IsNullOrEmpty(configPath))
        {
            AddError("Configuration file path is not specified");
            return;
        }

        if (!File.Exists(configPath))
        {
            AddError($"Configuration file not found: {configPath}");
            return;
        }

        try
        {
            // Validate JSON syntax
            var json = File.ReadAllText(configPath);
            using var document = JsonDocument.Parse(json);
            AddSuccess("Configuration file is valid JSON");
        }
        catch (JsonException ex)
        {
            AddError($"Configuration file contains invalid JSON: {ex.Message}");
        }
        catch (Exception ex)
        {
            AddError($"Error reading configuration file: {ex.Message}");
        }

        // Validate configuration values
        if (config != null)
        {
            if (config.CheckIntervalSeconds < 1 || config.CheckIntervalSeconds > 3600)
            {
                AddWarning($"CheckIntervalSeconds ({config.CheckIntervalSeconds}) is outside recommended range (1-3600)");
            }

            if (config.FileMonitoringDebounceSeconds < 1 || config.FileMonitoringDebounceSeconds > 600)
            {
                AddWarning($"FileMonitoringDebounceSeconds ({config.FileMonitoringDebounceSeconds}) is outside recommended range (1-600)");
            }

            if (config.CheckIntervalSeconds < config.FileMonitoringDebounceSeconds)
            {
                AddWarning("CheckIntervalSeconds is less than FileMonitoringDebounceSeconds, which may cause excessive processing");
            }
        }
    }

    private void ValidateCrontabFiles(CorneyConfig config)
    {
        AddValidationStep("Crontab Files Validation");

        if (config?.CrontabFiles == null || config.CrontabFiles.Length == 0)
        {
            AddWarning("No crontab files specified in configuration");
            return;
        }

        var duplicates = config.CrontabFiles
            .GroupBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var duplicate in duplicates)
        {
            AddWarning($"Duplicate crontab file specified: {duplicate}");
        }

        foreach (var crontabFile in config.CrontabFiles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            ValidateCrontabFile(crontabFile);
        }
    }

    private void ValidateCrontabFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            AddError("Empty crontab file path specified");
            return;
        }

        if (!File.Exists(filePath))
        {
            AddError($"Crontab file not found: {filePath}");
            return;
        }

        try
        {
            var lines = File.ReadAllLines(filePath);
            var validEntries = 0;
            var emptyLines = 0;
            var commentLines = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                
                if (string.IsNullOrEmpty(line))
                {
                    emptyLines++;
                    continue;
                }

                if (line.StartsWith("#"))
                {
                    commentLines++;
                    continue;
                }

                // Basic cron format validation (5 time fields + command)
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 6)
                {
                    AddWarning($"Crontab file {filePath} line {i + 1}: Possibly malformed cron entry (expected 5 time fields + command)");
                }
                else
                {
                    validEntries++;
                    
                    // Validate command exists (basic check)
                    var command = parts[5];
                    if (!File.Exists(command) && !IsSystemCommand(command))
                    {
                        AddInfo($"Crontab file {filePath} line {i + 1}: Command may not exist: {command}");
                    }
                }
            }

            if (validEntries == 0)
            {
                AddWarning($"Crontab file {filePath} contains no valid cron entries");
            }
            else
            {
                AddSuccess($"Crontab file {filePath}: {validEntries} valid entries, {commentLines} comments, {emptyLines} empty lines");
            }
        }
        catch (Exception ex)
        {
            AddError($"Error reading crontab file {filePath}: {ex.Message}");
        }
    }

    private bool IsSystemCommand(string command)
    {
        // Basic check for common system commands
        var systemCommands = new[] { "cmd", "powershell", "echo", "dir", "copy", "move", "del" };
        var commandName = Path.GetFileNameWithoutExtension(command).ToLowerInvariant();
        return systemCommands.Contains(commandName);
    }

    private void ValidateDevelopmentEnvironment()
    {
        AddValidationStep("Development Environment Validation");

        // Check .NET version
        try
        {
            var version = Environment.Version;
            if (version.Major >= 8)
            {
                AddSuccess($".NET version {version} is supported");
            }
            else
            {
                AddWarning($".NET version {version} may not be optimal (recommended: 8.0+)");
            }
        }
        catch (Exception ex)
        {
            AddWarning($"Could not determine .NET version: {ex.Message}");
        }

        // Check OS compatibility
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            AddSuccess($"Running on Windows {Environment.OSVersion.Version}");
        }
        else
        {
            AddWarning($"Running on {Environment.OSVersion.Platform} - some features may not work correctly");
        }

        // Check available memory
        try
        {
            var workingSet = Environment.WorkingSet;
            if (workingSet > 50 * 1024 * 1024) // 50MB
            {
                AddInfo($"Process memory usage: {workingSet / 1024 / 1024}MB");
            }
        }
        catch (Exception ex)
        {
            AddInfo($"Could not determine memory usage: {ex.Message}");
        }

        // Check environment variables
        var corneyEnv = Environment.GetEnvironmentVariable("CORNEY_ENV");
        if (!string.IsNullOrEmpty(corneyEnv))
        {
            AddInfo($"CORNEY_ENV environment variable set to: {corneyEnv}");
        }
    
        var aspNetEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (!string.IsNullOrEmpty(aspNetEnv))
        {
            AddInfo($"ASPNETCORE_ENVIRONMENT set to: {aspNetEnv}");
        }
    }

    private void ValidatePermissions(CorneyConfig config)
    {
        AddValidationStep("Permissions Validation");

        // Check write permissions for log directories
        var logDirectories = new[]
        {
            "dev/app.vs/log",
            "logs"
        };

        foreach (var logDir in logDirectories)
        {
            if (Directory.Exists(logDir))
            {
                try
                {
                    var testFile = Path.Combine(logDir, $"permission_test_{Guid.NewGuid():N}.tmp");
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                    AddSuccess($"Write permissions verified for: {logDir}");
                }
                catch (Exception ex)
                {
                    AddError($"No write permissions for log directory {logDir}: {ex.Message}");
                }
            }
        }

        // Check read permissions for crontab files
        if (config?.CrontabFiles != null)
        {
            foreach (var crontabFile in config.CrontabFiles)
            {
                if (File.Exists(crontabFile))
                {
                    try
                    {
                        File.ReadAllText(crontabFile);
                        AddSuccess($"Read permissions verified for: {crontabFile}");
                    }
                    catch (Exception ex)
                    {
                        AddError($"No read permissions for crontab file {crontabFile}: {ex.Message}");
                    }
                }
            }
        }
    }

    private void GenerateValidationReport()
    {
        AddValidationStep("Validation Summary");

        var errorCount = _validationResults.Count(r => r.Severity == ValidationSeverity.Error);
        var warningCount = _validationResults.Count(r => r.Severity == ValidationSeverity.Warning);
        var infoCount = _validationResults.Count(r => r.Severity == ValidationSeverity.Info);
        var successCount = _validationResults.Count(r => r.Severity == ValidationSeverity.Success);

        _logger.LogInformation("Validation Summary: {ErrorCount} errors, {WarningCount} warnings, {InfoCount} info, {SuccessCount} success",
            errorCount, warningCount, infoCount, successCount);

        // Log all validation results for debugging
        foreach (var result in _validationResults.Where(r => r.Severity != ValidationSeverity.Success))
        {
            var logLevel = result.Severity switch
            {
                ValidationSeverity.Error => LogLevel.Error,
                ValidationSeverity.Warning => LogLevel.Warning,
                ValidationSeverity.Info => LogLevel.Information,
                _ => LogLevel.Debug
            };

            _logger.Log(logLevel, "Validation: {Category} - {Message}", result.Category, result.Message);
        }
    }

    private void AddValidationStep(string stepName)
    {
        _logger.LogDebug("Validating: {StepName}", stepName);
    }

    private void AddError(string message, string category = "Configuration")
    {
        _validationResults.Add(new ValidationResult(ValidationSeverity.Error, category, message));
    }

    private void AddWarning(string message, string category = "Configuration")
    {
        _validationResults.Add(new ValidationResult(ValidationSeverity.Warning, category, message));
    }

    private void AddInfo(string message, string category = "Environment")
    {
        _validationResults.Add(new ValidationResult(ValidationSeverity.Info, category, message));
    }

    private void AddSuccess(string message, string category = "Validation")
    {
        _validationResults.Add(new ValidationResult(ValidationSeverity.Success, category, message));
    }
}

/// <summary>
/// Represents the result of a validation check.
/// </summary>
public record ValidationResult(ValidationSeverity Severity, string Category, string Message);

/// <summary>
/// Severity levels for validation results.
/// </summary>
public enum ValidationSeverity
{
    Success,
    Info,
    Warning,
    Error
}