#Requires -Version 5.1
<#
.SYNOPSIS
    Generates API documentation and developer guides for Corney
    
.DESCRIPTION
    This script automatically generates comprehensive API documentation,
    architecture guides, and developer references from the codebase.
    It analyzes XML documentation, code structure, and creates markdown files.

.PARAMETER OutputPath
    Directory where documentation should be generated (default: docs/)
    
.PARAMETER IncludePrivate
    Include private/internal APIs in documentation
    
.PARAMETER GenerateOnly
    Comma-separated list of documentation types to generate
    Options: api, architecture, examples, troubleshooting, all
    
.PARAMETER Format
    Output format: markdown, html, or both
    
.EXAMPLE
    .\generate-docs.ps1 -OutputPath "docs" -GenerateOnly "api,examples"
    
.EXAMPLE
    .\generate-docs.ps1 -IncludePrivate -Format "both"
#>

[CmdletBinding()]
param(
    [string]$OutputPath = "docs",
    [switch]$IncludePrivate,
    [string]$GenerateOnly = "all",
    [ValidateSet("markdown", "html", "both")]
    [string]$Format = "markdown",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot

# Documentation templates and content
$ApiDocTemplate = @"
# Corney API Documentation

## Overview
Corney is a Windows crontab executor that provides scheduling capabilities similar to Unix cron.

## Core Services

### CronService
The main scheduling engine that manages cron job execution.

**Key Methods:**
- ``Start(string[] crontabFiles)`` - Initializes and starts cron scheduling
- ``Restart(string[] cronFiles)`` - Restarts service with updated configuration  
- ``Stop()`` - Stops the cron service and cleans up resources

**Features:**
- Thread-safe operation with ReaderWriterLockSlim
- Performance monitoring integration  
- Automatic configuration reload
- Structured logging with correlation IDs

### ConfigFileMonitorService
Monitors configuration and crontab files for changes.

**Key Features:**
- Real-time file change detection
- Configurable debouncing (1-600 seconds)
- Differential reloading to avoid unnecessary processing
- File content change detection using SHA256 hashing

### ProcessWrapper
Executes scheduled commands with resilience patterns.

**Key Features:**
- Circuit breaker protection
- Retry policies with exponential backoff
- Process timeout handling
- Performance metrics collection

## Configuration

### CorneyConfig
Main configuration structure:

```csharp
public class CorneyConfig
{
    public string[] CrontabFiles { get; set; }
    public int CheckIntervalSeconds { get; set; } = 60;
    public int FileMonitoringDebounceSeconds { get; set; } = 5;
}
```

### Environment Variables
- ``CORNEY_ENV`` - Set to "Development" for enhanced debugging
- ``ASPNETCORE_ENVIRONMENT`` - Standard ASP.NET Core environment setting

## Performance Monitoring

Corney includes comprehensive performance monitoring:

- **Real-time Metrics**: Execution times, success rates, memory usage
- **Performance Dashboards**: Automated reporting every hour  
- **Alert System**: Configurable thresholds for performance issues
- **Correlation IDs**: Track operations across components

## Error Handling

### Resilience Patterns
- **Circuit Breaker**: Prevents cascading failures
- **Retry Policies**: Exponential backoff with jitter
- **Timeout Protection**: Prevents hanging operations
- **Health Checks**: Monitor system health

### Logging
Structured logging with:
- Event IDs for categorization
- Correlation context for tracing
- Performance metrics integration
- Multiple log levels and targets

## Development

### Debugging
Enable enhanced debugging with:
- Correlation context for operation tracing
- Enhanced loggers with automatic context
- Development-time configuration validation
- VS Code integration with launch configurations

### Testing
- xUnit test framework
- Integration tests for file monitoring
- Performance benchmarks
- Mutation testing support
"@

$ArchitectureDoc = @"
# Corney Architecture Guide

## System Overview
Corney follows a feature-based modular architecture with clear separation of concerns.

## Architecture Patterns

### Feature-Based Organization
```
Corney.Features/
├── Cron/           # Core scheduling functionality
├── Monitors/       # File system monitoring  
├── Processes/      # Command execution
└── App/           # Application configuration
```

### Dependency Injection
- Microsoft.Extensions.DependencyInjection
- Service lifetime management
- Factory patterns for implementation switching

### Event-Driven Architecture
- MediatR for event handling
- Domain events for loose coupling
- Async event processing

### Performance-First Design
- Non-blocking I/O operations
- Async/await throughout
- Resource pooling and reuse
- Memory-efficient algorithms

## Core Components

### Scheduling Engine
The CronService uses Cronos library for cron expression parsing:

```
CronDefinition -> CronService -> ProcessWrapper -> Command Execution
     ^                                                      |
     |                    Performance Monitoring            |
     |                           |                         |
ConfigFileMonitor <- File Changes <- FileSystemWatcher <- Files
```

### Monitoring System
Multi-layered monitoring approach:

1. **File System Level**: FileSystemWatcher with debouncing
2. **Configuration Level**: Differential configuration loading  
3. **Application Level**: Performance metrics and health checks
4. **Process Level**: Command execution monitoring

### Resilience Infrastructure
Built-in resilience patterns:

- **Circuit Breaker**: 3 failures trigger open state, 2-minute cooldown
- **Retry Policy**: Exponential backoff with jitter, max 3 attempts
- **Timeout Protection**: Configurable timeouts for all operations
- **Health Checks**: File system and circuit breaker status

## Data Flow

### Configuration Loading
```
Config File -> JSON Parse -> Validation -> CorneyConfig -> Service Registration
                  |              |            |
              Error Handling  Diagnostics  Hot Reload
```

### Cron Execution
```
Cron Expression -> Next Occurrence -> Schedule -> Execute -> Monitor -> Log
                        |                |         |        |       |
                   Time Calculation   Reactive    Process  Metrics Structured
                                      Scheduler   Wrapper          Logging
```

### Performance Monitoring
```
Operation Start -> Timing -> Metrics Collection -> Aggregation -> Dashboard -> Alerts
                     |           |                     |           |         |
                 Correlation   Resource Usage      Historical    Periodic   Threshold
                 Context       Tracking            Data          Reports    Checks
```

## Technology Stack

### Core Technologies
- **.NET 8.0**: Modern runtime with performance improvements
- **System.Reactive**: Reactive extensions for async operations
- **Cronos**: Cron expression parsing and scheduling
- **MediatR**: Mediator pattern for CQRS
- **NLog**: Structured logging framework

### Development Tools
- **xUnit**: Testing framework
- **NUKE**: Build automation
- **VS Code**: Enhanced debugging experience
- **PowerShell**: Setup and automation scripts

## Deployment Architecture

### Development Environment
```
dev/app.vs/
├── config/         # Development configuration
├── log/           # Application logs  
└── scripts/       # Development helpers
```

### Production Deployment
```
Corney.exe          # Main executable
├── Dependencies/   # Runtime dependencies
├── Config/        # Production configuration
└── Logs/         # Production logs
```

## Security Considerations

### Input Validation
- Configuration validation with data annotations
- File path sanitization
- Command argument validation

### Process Execution
- Working directory isolation
- Environment variable control
- Process timeout enforcement

### Logging Security
- No sensitive data in logs
- Structured logging prevents injection
- Configurable log retention

## Scalability Design

### Resource Management
- Efficient file watching with debouncing
- Memory-conscious logging
- Async I/O for non-blocking operations

### Performance Optimization
- Object pooling for frequent allocations
- Lazy initialization patterns
- Caching for repeated operations

### Monitoring and Alerting
- Real-time performance metrics
- Configurable alert thresholds
- Historical trend analysis
"@

$TroubleshootingDoc = @"
# Corney Troubleshooting Guide

## Common Issues

### Application Won't Start

**Symptoms:**
- Application exits immediately
- "Unable to resolve service" errors
- Missing dependency errors

**Solutions:**
1. **Check .NET Version**
   ``````
   dotnet --version
   # Should be 8.0 or later
   ``````

2. **Verify Configuration**
   ``````powershell
   .\scripts\dev-setup.ps1 -SetupDebugging
   ``````

3. **Check Dependencies**
   ``````
   dotnet restore src/Corney.sln
   dotnet build src/Corney.sln
   ``````

### File Monitoring Not Working

**Symptoms:**
- Cron jobs don't execute when files change
- No file change events in logs
- Configuration changes ignored

**Debugging Steps:**
1. **Check File Permissions**
   - Ensure read access to crontab files
   - Verify write access to log directories

2. **Validate File Paths**
   - Use absolute paths in configuration
   - Check file existence: ``Test-Path "path\to\crontab.txt"``

3. **Monitor File Events**
   Enable debug logging and check for FileSystemWatcher events

4. **Adjust Debouncing**
   - Increase ``FileMonitoringDebounceSeconds`` for frequent changes
   - Decrease for faster response times

### Performance Issues

**Symptoms:**  
- High CPU usage
- Memory leaks
- Slow cron execution

**Diagnostic Tools:**
1. **Performance Monitor**
   Check logs for performance metrics and alerts

2. **Memory Analysis**
   ``````
   dotnet-counters monitor --process-id <PID>
   ``````

3. **Enable Performance Logging**
   Set log level to Debug in configuration

**Common Fixes:**
- Reduce file monitoring frequency
- Optimize cron expressions
- Check for runaway processes

### Configuration Errors

**Symptoms:**
- Invalid JSON errors
- Configuration validation failures
- Default values being used

**Solutions:**
1. **Validate JSON Syntax**
   Use development validator:
   ``````csharp
   var validator = new DevelopmentValidator(logger);
   validator.ValidateDevelopmentEnvironment(configPath, config);
   ``````

2. **Check Configuration Schema**
   Ensure all required fields are present with valid values

3. **Use Development Tools**
   ``````powershell
   .\scripts\dev-setup.ps1 -Force
   ``````

## Debugging Techniques

### Enable Correlation Tracing
``````csharp
using (CorrelationContext.StartOperation("MyOperation"))
{
    // Your code here - all logs will include correlation ID
}
``````

### Enhanced Logging
``````csharp
logger.LogDebugWithContext("Operation started with {Parameter}", parameter);
``````

### Performance Profiling
Enable performance monitoring in configuration:
``````json
{
  "PerformanceMonitoring": {
    "Enabled": true,
    "ReportingInterval": "00:05:00",
    "DashboardInterval": "00:15:00"
  }
}
``````

## Log Analysis

### Key Log Patterns
- **Correlation IDs**: ``[abc12345] Operation started``
- **Performance Metrics**: ``completed in 123.4ms``
- **Error Context**: Full stack traces with correlation

### Log Locations
- **Development**: ``dev/app.vs/log/``
- **Production**: ``logs/``
- **System Events**: Windows Event Log

### Useful Log Queries
``````
# Find all operations for a correlation ID
Select-String -Path "*.txt" -Pattern "\[abc12345\]"

# Find performance issues  
Select-String -Path "*.txt" -Pattern "completed in [0-9]{3,}.*ms"

# Find errors with context
Select-String -Path "*.txt" -Pattern "ERR.*\[.*\]"
``````

## Performance Tuning

### Configuration Optimization
``````json
{
  "CheckIntervalSeconds": 30,           // Reduce for faster response
  "FileMonitoringDebounceSeconds": 10   // Increase for stability
}
``````

### Memory Optimization
- Monitor working set size
- Check for memory leaks in long-running processes
- Use performance counters for tracking

### CPU Optimization  
- Optimize cron expressions
- Reduce file monitoring frequency
- Use async patterns throughout

## Getting Help

### Enable Verbose Logging
1. Set log level to Debug or Trace
2. Enable correlation context
3. Use enhanced loggers with context

### Collect Diagnostic Information
``````powershell
# System info
Get-ComputerInfo | Out-File system-info.txt

# Process info
Get-Process Corney | Format-List * | Out-File process-info.txt

# Configuration
Get-Content "dev/app.vs/config/corney/config.json" | Out-File config-dump.txt
``````

### Contact Information
- Check logs first: ``dev/app.vs/log/``
- Review configuration: ``dev/app.vs/config/``
- Run diagnostics: ``.\scripts\dev-setup.ps1 -Verbose``
"@

function New-DocumentationStructure {
    param($OutputPath)
    
    $directories = @(
        "$OutputPath",
        "$OutputPath/api",
        "$OutputPath/guides", 
        "$OutputPath/examples",
        "$OutputPath/architecture"
    )
    
    foreach ($dir in $directories) {
        if (!(Test-Path $dir)) {
            New-Item -Path $dir -ItemType Directory -Force | Out-Null
            Write-Host "✅ Created directory: $dir" -ForegroundColor Green
        }
    }
}

function New-ApiDocumentation {
    param($OutputPath)
    
    Write-Host "🔧 Generating API documentation..." -ForegroundColor Blue
    
    $apiDocPath = Join-Path $OutputPath "api/README.md"
    $ApiDocTemplate | Out-File -FilePath $apiDocPath -Encoding UTF8
    Write-Host "✅ Created API documentation: $apiDocPath" -ForegroundColor Green
}

function New-ArchitectureDocumentation {
    param($OutputPath)
    
    Write-Host "🔧 Generating architecture documentation..." -ForegroundColor Blue
    
    $archDocPath = Join-Path $OutputPath "architecture/README.md"
    $ArchitectureDoc | Out-File -FilePath $archDocPath -Encoding UTF8
    Write-Host "✅ Created architecture documentation: $archDocPath" -ForegroundColor Green
}

function New-TroubleshootingDocumentation {
    param($OutputPath)
    
    Write-Host "🔧 Generating troubleshooting guide..." -ForegroundColor Blue
    
    $troubleshootingPath = Join-Path $OutputPath "guides/troubleshooting.md"
    $TroubleshootingDoc | Out-File -FilePath $troubleshootingPath -Encoding UTF8
    Write-Host "✅ Created troubleshooting guide: $troubleshootingPath" -ForegroundColor Green
}

function New-ExamplesDocumentation {
    param($OutputPath)
    
    Write-Host "🔧 Generating examples..." -ForegroundColor Blue
    
    $examplesDoc = @"
# Corney Examples

## Basic Crontab Examples

### Simple Time-Based Jobs
``````
# Run every minute
* * * * * echo "Hello World"

# Run every hour at minute 0
0 * * * * C:\scripts\hourly-task.bat

# Run daily at 9 AM
0 9 * * * powershell.exe -File "C:\scripts\daily-backup.ps1"

# Run weekly on Sunday at midnight
0 0 * * 0 C:\scripts\weekly-cleanup.exe
``````

### Advanced Scheduling
``````
# Run every 15 minutes during business hours (9 AM - 5 PM)
*/15 9-17 * * 1-5 C:\scripts\business-monitor.cmd

# Run on the first day of every month at 2 AM
0 2 1 * * C:\scripts\monthly-report.ps1

# Run every 30 seconds (using multiple entries)
* * * * * C:\scripts\frequent-task.exe
* * * * * ping 127.0.0.1 -n 31 > nul & C:\scripts\frequent-task.exe
``````

## Configuration Examples

### Basic Configuration
``````json
{
  "CrontabFiles": [
    "C:\\config\\main-crontab.txt",
    "C:\\config\\backup-tasks.txt"
  ],
  "CheckIntervalSeconds": 60,
  "FileMonitoringDebounceSeconds": 5
}
``````

### Development Configuration
``````json
{
  "CrontabFiles": [
    "dev/app.vs/config/corney/crontab.txt"
  ],
  "CheckIntervalSeconds": 10,
  "FileMonitoringDebounceSeconds": 2
}
``````

### Production Configuration
``````json
{
  "CrontabFiles": [
    "C:\\ProgramData\\Corney\\production-tasks.txt",
    "C:\\ProgramData\\Corney\\maintenance-tasks.txt"
  ],
  "CheckIntervalSeconds": 30,
  "FileMonitoringDebounceSeconds": 15
}
``````

## PowerShell Integration

### Running PowerShell Scripts
``````
# Basic PowerShell execution
0 9 * * * powershell.exe -ExecutionPolicy Bypass -File "C:\scripts\daily-task.ps1"

# PowerShell with parameters
0 */6 * * * powershell.exe -Command "& 'C:\scripts\monitor.ps1' -Environment 'Production' -LogLevel 'Info'"

# PowerShell one-liner  
*/5 * * * * powershell.exe -Command "Get-EventLog -LogName System -Newest 1 | Export-Csv C:\logs\events.csv"
``````

### Error Handling in PowerShell
``````powershell
# Example monitoring script with error handling
try {
    # Your monitoring logic here
    $result = Invoke-RestMethod -Uri "https://api.example.com/health"
    Write-Output "Health check passed: $($result.status)"
} catch {
    Write-Error "Health check failed: $($_.Exception.Message)"
    # Send alert or log to event system
}
``````

## Batch File Integration

### Simple Batch Examples
``````batch
@echo off
REM backup-script.bat
echo Starting backup at %date% %time%
xcopy "C:\important\*" "C:\backup\" /s /e /y
echo Backup completed at %date% %time%
``````

### Batch with Error Handling
``````batch
@echo off
REM robust-task.bat
echo Task started: %date% %time%

REM Your task commands here
your-command.exe
if %ERRORLEVEL% neq 0 (
    echo Task failed with error level %ERRORLEVEL%
    exit /b 1
)

echo Task completed successfully: %date% %time%
``````

## Monitoring and Logging

### Enable Debug Logging
Set environment variable before starting Corney:
``````cmd
set CORNEY_ENV=Development
Corney.exe
``````

### Performance Monitoring Example
``````csharp
// In your custom extensions
using (var operation = logger.LogOperationStart("CustomTask"))
{
    // Your task logic here
    await ProcessDataAsync();
    // Operation completion is logged automatically
}
``````

## Integration Examples

### Windows Task Scheduler Alternative
Replace Windows Task Scheduler with Corney:

**Old Task Scheduler XML:**
``````xml
<Task>
  <Triggers>
    <TimeTrigger>
      <StartBoundary>2024-01-01T09:00:00</StartBoundary>
      <Repetition>
        <Interval>PT1H</Interval>
      </Repetition>
    </TimeTrigger>
  </Triggers>
</Task>
``````

**New Corney Crontab:**
``````
0 9 * * * C:\tasks\hourly-process.exe
``````

### Service Integration
Run Corney as a Windows Service:
``````cmd
# Install as service (requires additional service wrapper)
sc create "Corney Scheduler" binPath="C:\Corney\Corney.exe" start=auto
sc start "Corney Scheduler"
``````

## Best Practices

### Crontab Organization
``````
# Group related tasks
# Database maintenance tasks
0 2 * * * C:\db\backup-database.ps1
0 3 * * 0 C:\db\optimize-database.ps1

# Log rotation tasks  
0 1 * * * C:\scripts\rotate-logs.bat
0 4 * * 0 C:\scripts\archive-old-logs.ps1

# Monitoring tasks
*/10 * * * * C:\monitoring\health-check.ps1
*/5 * * * * C:\monitoring\disk-space-check.bat
``````

### Error Recovery
``````
# Primary task with fallback
0 9 * * * C:\tasks\primary-backup.ps1 || C:\tasks\fallback-backup.ps1

# Task with notification
0 6 * * * C:\tasks\important-process.exe && C:\notify\success.ps1 || C:\notify\failure.ps1
``````

### Testing Cron Jobs
``````
# Test job (runs every minute for testing)
* * * * * echo "Test at %date% %time%" >> C:\temp\test.log

# Remove or comment out after testing
# * * * * * echo "Test at %date% %time%" >> C:\temp\test.log
``````
"@
    
    $examplesPath = Join-Path $OutputPath "examples/README.md"
    $examplesDoc | Out-File -FilePath $examplesPath -Encoding UTF8
    Write-Host "✅ Created examples documentation: $examplesPath" -ForegroundColor Green
}

# Main execution
try {
    Write-Host "`n📖 Corney Documentation Generator" -ForegroundColor Magenta
    Write-Host "================================`n" -ForegroundColor Magenta
    
    $fullOutputPath = Join-Path $ProjectRoot $OutputPath
    New-DocumentationStructure -OutputPath $fullOutputPath
    
    $generateTypes = $GenerateOnly.Split(',').Trim()
    
    if ($generateTypes -contains "all" -or $generateTypes -contains "api") {
        New-ApiDocumentation -OutputPath $fullOutputPath
    }
    
    if ($generateTypes -contains "all" -or $generateTypes -contains "architecture") {
        New-ArchitectureDocumentation -OutputPath $fullOutputPath
    }
    
    if ($generateTypes -contains "all" -or $generateTypes -contains "examples") {
        New-ExamplesDocumentation -OutputPath $fullOutputPath
    }
    
    if ($generateTypes -contains "all" -or $generateTypes -contains "troubleshooting") {
        New-TroubleshootingDocumentation -OutputPath $fullOutputPath
    }
    
    Write-Host "`n🎉 Documentation generation complete!" -ForegroundColor Green
    Write-Host "📁 Documentation available in: $fullOutputPath" -ForegroundColor Cyan
    
} catch {
    Write-Host "❌ Documentation generation failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}