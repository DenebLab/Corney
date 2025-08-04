#Requires -Version 5.1
<#
.SYNOPSIS
    Corney Developer Environment Setup Script
    
.DESCRIPTION
    Automates the setup of a complete development environment for Corney.
    This script handles dependency verification, environment configuration,
    build setup, and development tool installation.

.PARAMETER SkipDependencies
    Skip dependency verification (for CI/CD environments)
    
.PARAMETER ConfigureVSCode
    Install and configure VS Code extensions for optimal development experience
    
.PARAMETER SetupDebugging
    Configure debugging environment with sample configurations
    
.PARAMETER Verbose
    Enable verbose output for troubleshooting

.EXAMPLE
    .\dev-setup.ps1 -ConfigureVSCode -SetupDebugging
    
.EXAMPLE
    .\dev-setup.ps1 -SkipDependencies -Verbose
#>

[CmdletBinding()]
param(
    [switch]$SkipDependencies,
    [switch]$ConfigureVSCode,
    [switch]$SetupDebugging,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$InformationPreference = "Continue"

# Configuration
$RequiredDotNetVersion = "8.0"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$DevConfigPath = Join-Path $ProjectRoot "dev"
$AppVSPath = Join-Path $DevConfigPath "app.vs"

# Color output functions
function Write-Success { 
    param($Message)
    Write-Host "✅ $Message" -ForegroundColor Green 
}

function Write-Warning { 
    param($Message)
    Write-Host "⚠️  $Message" -ForegroundColor Yellow 
}

function Write-Error { 
    param($Message)
    Write-Host "❌ $Message" -ForegroundColor Red 
}

function Write-Info { 
    param($Message)
    Write-Host "ℹ️  $Message" -ForegroundColor Cyan 
}

function Write-Step { 
    param($Message)
    Write-Host "`n🔧 $Message" -ForegroundColor Blue 
}

function Test-CommandExists {
    param($Command)
    $null -ne (Get-Command $Command -ErrorAction SilentlyContinue)
}

function Test-DotNetVersion {
    try {
        $version = dotnet --version
        $majorMinor = $version.Split('.')[0..1] -join '.'
        return [version]$majorMinor -ge [version]$RequiredDotNetVersion
    }
    catch {
        return $false
    }
}

function Install-VSCodeExtensions {
    $extensions = @(
        "ms-dotnettools.csharp",
        "ms-dotnettools.vscode-dotnet-runtime", 
        "formulahendry.dotnet-test-explorer",
        "jmrog.vscode-nuget-package-manager",
        "ms-vscode.powershell",
        "redhat.vscode-xml",
        "yzhang.markdown-all-in-one",
        "streetsidesoftware.code-spell-checker",
        "ms-vscode.vscode-json"
    )
    
    Write-Step "Installing VS Code extensions..."
    foreach ($extension in $extensions) {
        try {
            Write-Verbose "Installing extension: $extension"
            $result = code --install-extension $extension --force 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-Success "Installed: $extension"
            } else {
                Write-Warning "Failed to install: $extension - $result"
            }
        }
        catch {
            Write-Warning "Error installing $extension`: $($_.Exception.Message)"
        }
    }
}

function New-VSCodeWorkspace {
    $workspaceConfig = @{
        folders = @(
            @{ path = "." }
        )
        settings = @{
            "dotnet.defaultSolution" = "src/Corney.sln"
            "files.exclude" = @{
                "**/bin" = $true
                "**/obj" = $true
                "**/.vs" = $true
            }
            "csharp.semanticHighlighting.enabled" = $true
            "omnisharp.enableEditorConfigSupport" = $true
            "omnisharp.enableRoslynAnalyzers" = $true
        }
        extensions = @{
            recommendations = @(
                "ms-dotnettools.csharp",
                "ms-dotnettools.vscode-dotnet-runtime"
            )
        }
    }
    
    $workspacePath = Join-Path $ProjectRoot "Corney.code-workspace"
    $workspaceConfig | ConvertTo-Json -Depth 10 | Out-File -FilePath $workspacePath -Encoding UTF8
    Write-Success "Created VS Code workspace: $workspacePath"
}

function New-DevelopmentConfig {
    Write-Step "Setting up development configuration..."
    
    # Ensure dev directories exist
    @($DevConfigPath, $AppVSPath, 
      (Join-Path $AppVSPath "config"), 
      (Join-Path $AppVSPath "config" "corney"),
      (Join-Path $AppVSPath "log")) | ForEach-Object {
        if (!(Test-Path $_)) {
            New-Item -Path $_ -ItemType Directory -Force | Out-Null
            Write-Success "Created directory: $_"
        }
    }
    
    # Create sample crontab file
    $sampleCrontab = @"
# Sample crontab file for Corney development
# Format: [minute] [hour] [day] [month] [day_of_week] [command]

# Run a simple command every minute (for testing)
# */1 * * * * echo "Hello from Corney!" > C:\temp\corney-test.txt

# Run a PowerShell script every 5 minutes
# */5 * * * * powershell.exe -ExecutionPolicy Bypass -File "C:\scripts\monitor.ps1"

# Run a batch file daily at 9 AM
# 0 9 * * * C:\scripts\daily-backup.bat

# Examples of different time formats:
# 0 * * * *     - Every hour
# 0 0 * * *     - Every day at midnight
# 0 0 * * 0     - Every Sunday at midnight
# 0 0 1 * *     - First day of every month
"@
    
    $crontabPath = Join-Path $AppVSPath "config" "corney" "crontab.txt"
    if (!(Test-Path $crontabPath) -or $Force) {
        $sampleCrontab | Out-File -FilePath $crontabPath -Encoding UTF8
        Write-Success "Created sample crontab: $crontabPath"
    }
    
    # Create development configuration
    $devConfig = @{
        CrontabFiles = @($crontabPath)
        CheckIntervalSeconds = 10
        FileMonitoringDebounceSeconds = 2
    }
    
    $configPath = Join-Path $AppVSPath "config" "corney" "config.json"
    if (!(Test-Path $configPath) -or $Force) {
        $devConfig | ConvertTo-Json -Depth 10 | Out-File -FilePath $configPath -Encoding UTF8
        Write-Success "Created development config: $configPath"
    }
}

function New-DebugConfiguration {
    Write-Step "Setting up debugging configuration..."
    
    # Create launch.json for VS Code debugging
    $vscodeDir = Join-Path $ProjectRoot ".vscode"
    if (!(Test-Path $vscodeDir)) {
        New-Item -Path $vscodeDir -ItemType Directory -Force | Out-Null
    }
    
    $launchConfig = @{
        version = "0.2.0"
        configurations = @(
            @{
                name = "Launch Corney (Debug)"
                type = "coreclr"
                request = "launch"
                program = "`${workspaceFolder}/src/Corney/bin/Debug/net8.0-windows/Corney.exe"
                args = @()
                cwd = "`${workspaceFolder}"
                console = "internalConsole"
                stopAtEntry = $false
                env = @{
                    ASPNETCORE_ENVIRONMENT = "Development"
                    CORNEY_ENV = "Development"
                }
            },
            @{
                name = "Attach to Corney Process"
                type = "coreclr"
                request = "attach"
                processName = "Corney.exe"
            }
        )
    }
    
    $launchPath = Join-Path $vscodeDir "launch.json"
    if (!(Test-Path $launchPath) -or $Force) {
        $launchConfig | ConvertTo-Json -Depth 10 | Out-File -FilePath $launchPath -Encoding UTF8
        Write-Success "Created VS Code launch configuration: $launchPath"
    }
    
    # Create tasks.json for build tasks
    $tasksConfig = @{
        version = "2.0.0"
        tasks = @(
            @{
                label = "build"
                command = "dotnet"
                type = "process"
                args = @("build", "`${workspaceFolder}/src/Corney/Corney.csproj")
                group = @{
                    kind = "build"
                    isDefault = $true
                }
                presentation = @{
                    echo = $true
                    reveal = "silent"
                    focus = $false
                    panel = "shared"
                }
                problemMatcher = "`$msCompile"
            },
            @{
                label = "test"
                command = "dotnet"
                type = "process"
                args = @("test", "`${workspaceFolder}/src/Corney.Tests/Corney.Tests.csproj")
                group = "test"
                presentation = @{
                    echo = $true
                    reveal = "always"
                    focus = $false
                    panel = "shared"
                }
            },
            @{
                label = "clean"
                command = "dotnet"
                type = "process"
                args = @("clean", "`${workspaceFolder}/src/Corney.sln")
                group = "build"
            }
        )
    }
    
    $tasksPath = Join-Path $vscodeDir "tasks.json"
    if (!(Test-Path $tasksPath) -or $Force) {
        $tasksConfig | ConvertTo-Json -Depth 10 | Out-File -FilePath $tasksPath -Encoding UTF8
        Write-Success "Created VS Code tasks configuration: $tasksPath"
    }
}

function Test-BuildEnvironment {
    Write-Step "Testing build environment..."
    
    try {
        Push-Location (Join-Path $ProjectRoot "src")
        
        Write-Info "Restoring NuGet packages..."
        dotnet restore Corney.sln
        if ($LASTEXITCODE -ne 0) { throw "Package restore failed" }
        
        Write-Info "Building solution..."
        dotnet build Corney.sln --no-restore
        if ($LASTEXITCODE -ne 0) { throw "Build failed" }
        
        Write-Info "Running tests..."
        dotnet test Corney.Tests/Corney.Tests.csproj --no-build --verbosity minimal
        if ($LASTEXITCODE -ne 0) { throw "Tests failed" }
        
        Write-Success "Build environment is working correctly!"
        
    }
    catch {
        Write-Error "Build environment test failed: $($_.Exception.Message)"
        throw
    }
    finally {
        Pop-Location
    }
}

function New-DeveloperGuide {
    $guide = @"
# Corney Developer Setup Guide

This guide will help you set up a complete development environment for Corney.

## Quick Start

1. **Run the setup script:**
   ``````powershell
   .\scripts\dev-setup.ps1 -ConfigureVSCode -SetupDebugging
   ``````

2. **Open the project:**
   - VS Code: Open `Corney.code-workspace`
   - Visual Studio: Open `src\Corney.sln`

3. **Start debugging:**
   - Press F5 in VS Code or Visual Studio
   - Or run: `dotnet run --project src\Corney\Corney.csproj`

## Development Workflow

### Building
``````bash
# Build the solution
dotnet build src\Corney.sln

# Build and run
dotnet run --project src\Corney\Corney.csproj
``````

### Testing
``````bash
# Run all tests
dotnet test src\Corney.Tests\

# Run tests with coverage
dotnet test src\Corney.Tests\ --collect:"XPlat Code Coverage"
``````

### Configuration

Development configuration is located in `dev\app.vs\config\corney\`:
- `config.json` - Main application configuration
- `crontab.txt` - Sample crontab file for testing

### Debugging

The setup script creates VS Code debug configurations:
- **Launch Corney (Debug)** - Start with debugger attached
- **Attach to Corney Process** - Attach to running process

### Logs

Development logs are written to `dev\app.vs\log\`:
- `corney.lowlevel.txt` - Low-level system logs
- `corney.YYYY-MM-DD.000.txt` - Daily application logs

## Architecture Overview

Corney follows a feature-based architecture:

- `Features\Cron\` - Core scheduling functionality
- `Features\Monitors\` - File system monitoring
- `Features\Processes\` - Process execution
- `Common\` - Shared infrastructure

## Contributing

1. Create a feature branch from `develop`
2. Make your changes with appropriate tests
3. Ensure all tests pass: `dotnet test`
4. Create a pull request to `develop`

## Troubleshooting

### Common Issues

**Build Errors:**
- Ensure .NET 8.0 SDK is installed
- Run `dotnet restore` to restore packages
- Clean and rebuild: `dotnet clean && dotnet build`

**File Monitoring Not Working:**
- Check that crontab files exist and are accessible
- Verify file paths in configuration are correct
- Check logs for file system errors

**Performance Issues:**
- Enable performance monitoring in configuration
- Check memory usage with Performance Monitor
- Review logs for slow operations

### Getting Help

- Check the logs in `dev\app.vs\log\`
- Review configuration in `dev\app.vs\config\`
- Enable verbose logging for more details
"@
    
    $guidePath = Join-Path $ProjectRoot "DEVELOPER.md"
    if (!(Test-Path $guidePath) -or $Force) {
        $guide | Out-File -FilePath $guidePath -Encoding UTF8
        Write-Success "Created developer guide: $guidePath"
    }
}

# Main execution
try {
    Write-Host "`n🚀 Corney Developer Environment Setup" -ForegroundColor Magenta
    Write-Host "====================================`n" -ForegroundColor Magenta
    
    # Dependency checks
    if (!$SkipDependencies) {
        Write-Step "Verifying dependencies..."
        
        if (!(Test-CommandExists "dotnet")) {
            Write-Error ".NET SDK not found. Please install .NET 8.0 SDK from https://dotnet.microsoft.com/download"
            exit 1
        }
        
        if (!(Test-DotNetVersion)) {
            Write-Error ".NET 8.0 or later required. Current version: $(dotnet --version)"
            exit 1
        }
        
        Write-Success ".NET SDK $(dotnet --version) is installed"
        
        if (!(Test-CommandExists "git")) {
            Write-Warning "Git not found. Some features may not work properly."
        } else {
            Write-Success "Git is available"
        }
    }
    
    # Setup development environment
    New-DevelopmentConfig
    New-DeveloperGuide
    
    if ($SetupDebugging) {
        New-DebugConfiguration
    }
    
    if ($ConfigureVSCode) {
        if (Test-CommandExists "code") {
            Install-VSCodeExtensions
            New-VSCodeWorkspace
            Write-Success "VS Code configured successfully"
        } else {
            Write-Warning "VS Code not found. Skipping VS Code configuration."
        }
    }
    
    # Test the build environment
    Test-BuildEnvironment
    
    Write-Host "`n🎉 Development environment setup complete!" -ForegroundColor Green
    Write-Host "📖 See DEVELOPER.md for detailed development workflow" -ForegroundColor Cyan
    Write-Host "🚀 Run 'dotnet run --project src\Corney\Corney.csproj' to start" -ForegroundColor Cyan
    
}
catch {
    Write-Error "Setup failed: $($_.Exception.Message)"
    Write-Host "`nFor help, check the logs or run with -Verbose flag" -ForegroundColor Yellow
    exit 1
}