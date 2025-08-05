# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Corney is a crontab file executor for Windows. It provides Windows-based scheduling capabilities similar to Unix cron by parsing crontab files and executing scheduled tasks. The application uses a Windows Forms UI with system tray integration and monitors configuration files for changes.

## Development Commands

### Build System
The project uses NUKE build system with cross-platform support:
- **Windows**: `build.cmd` or `build.ps1`  
- **Linux/macOS**: `build.sh`
- **Direct build**: `dotnet build src/build/_build.csproj`

Main build targets:
- `PublishLocal` (default): Complete build with packaging and publishing
- `PublishRobeNova`: Complete build on build server
- `GithubRelease`: Build for GitHub releases with single-file executable
- `Test`: Run test suite with graceful failure handling
- `Clean`: Clean build artifacts 
- `Restore`: Restore NuGet packages
- `PublishLocalStandalone`: Build and copy to dev/app.standalone/

### Testing
- **Run tests**: Use NUKE `./build.cmd Test` or `dotnet test` directly
- **Test projects**: Located in `src/Corney.Tests/`
- **Test framework**: xUnit 2.9.3
- **Platform support**: Tests run on Windows only (due to Windows Forms dependencies)
- **CI integration**: Tests run automatically in GitHub Actions build pipeline
- **Test output**: TRX format with results in `.nuke/temp/w/Corney/test-result/`

### Development Environment
- **Local standalone**: `dev/app.standalone/Corney.exe` (built executable)
- **VS debugging**: `dev/app.vs/` contains config and logs for development
- **Config files**: Located in `dev/app.vs/config/corney/`

## CI/CD Pipeline

### GitHub Actions Workflows
The project uses GitHub Actions for automated CI/CD:

#### Build Workflow (`.github/workflows/build.yml`)
- **Triggers**: Push to `main` or `develop` branches
- **Platform**: Windows Server 2022
- **Steps**:
  1. Checkout with full git history and tags
  2. Setup .NET 8.0 SDK
  3. Cache NuGet packages
  4. Run NUKE `GithubRelease` target (includes tests)
  5. Publish test results from TRX files
  6. Upload single-file executable artifact

#### Release Workflow (`.github/workflows/release.yml`)
- **Triggers**: Push to `production` branch
- **Features**:
  1. Uses AbcVersion tool for semantic versioning
  2. Creates and pushes git tags automatically
  3. Builds single-file executable via NUKE
  4. Packages release assets
  5. Creates GitHub Release with artifacts
  6. Generates detailed release notes

#### Dependencies Workflow (`.github/workflows/dependencies.yml`)
- **Automated dependency updates and security scanning**

### Version Management
- **AbcVersion**: Semantic versioning based on git history and tags
- **Build metadata**: Includes branch, commit SHA, build timestamp
- **Repository root**: Configured for proper git repository detection in CI

## Architecture

### Project Structure
- **Corney** (main executable): Windows Forms application with system tray integration
- **Corney.Core** (library): Core business logic and features  
- **Corney.Tests** (tests): xUnit test suite
- **build** (NUKE): Build automation and packaging

### Core Components

#### Feature-Based Architecture
The codebase follows feature-based organization in `Corney.Core/Features/`:

- **Cron**: Core scheduling functionality
  - `CronService`: Main scheduling engine using Cronos library
  - `CrontabFileParser`: Parses crontab file format
  - `CronDefinition`: Models cron job definitions
  - `ExecuteItem`: Represents individual execution instances

- **Monitors**: File system monitoring
  - `ConfigFileMonitor`: Watches crontab files for changes using FileSystemWatcher
  - `FileWatchHelpers`: Utilities for file monitoring

- **Processes**: Command execution
  - `ProcessWrapper`: Manages external process execution
  - `Pharse`: Command parsing and tokenization
  - `StringHelper`: String manipulation utilities

#### Application Bootstrap
Uses a sophisticated bootstrap pattern in `Common/Bootstrap/`:
- `Boot`: Main bootstrapper with assembly management
- `AppEnvironment`/`AppEnvironmentBuilder`: Environment configuration
- `AppVersion`/`AppVersionBuilder`: Version management with build metadata

#### Dependency Injection
- **Container**: Autofac for dependency injection
- **Mediator**: MediatR for CQRS pattern with events like `AppStartingEvent`, `AppStartedEvent`
- **Modules**: Feature-based Autofac modules in each feature's `Config/` folder

### Key Dependencies
- **Autofac 4.6.2**: Dependency injection container
- **MediatR 4.0.1**: Mediator pattern for CQRS
- **Cronos 0.6.3**: Cron expression parsing and scheduling
- **NLog 4.7.15**: Logging framework
- **Newtonsoft.Json 13.0.1**: JSON serialization
- **System.Reactive**: Reactive extensions for async operations

### Application Flow
1. **Bootstrap**: Single instance check, logging setup, assembly registration
2. **Container Build**: Register Autofac modules from all features
3. **App Events**: Publish `AppStartingEvent` → `AppStartedEvent` → `StartCorneyReq`
4. **Main Loop**: Windows Forms message pump with system tray integration

## Installation and Deployment

### PowerShell Installer (`scripts/install.ps1`)
Enterprise-grade installer with the following features:
- **GitHub API integration**: Downloads from releases automatically
- **Version management**: Side-by-side installations in `%LOCALAPPDATA%\Deneblab\Corney\`
- **System integration**: Desktop shortcuts and Windows startup registration
- **Safe updates**: Process management with retry logic for file operations
- **Organized structure**: Separate directories for app, config, logs, and cache

#### Installer Parameters
| Parameter | Description | Example |
|-----------|-------------|---------|
| `-Version` | Install specific version | `-Version "2.0.23"` |
| `-ForceUpdate` | Force reinstall if version exists | `-ForceUpdate` |
| `-SkipShortcut` | Don't create desktop shortcut | `-SkipShortcut` |
| `-SkipStartup` | Don't add to Windows startup | `-SkipStartup` |
| `-Silent` | Install without launching app | `-Silent` |

#### Installation Directory Structure
```
%LOCALAPPDATA%\Deneblab\Corney\
├── app\Corney.{version}\     # Versioned installations
├── config\                   # Configuration files
├── log\                     # Application logs
├── .syrup\                  # Cache and metadata
└── Corney.lnk               # Main shortcut
```

### Deployment Artifacts
- **Single-file executable**: Self-contained Windows executable
- **Build artifacts**: Complete build output with dependencies
- **Release packages**: ZIP archives via GitHub Releases
- **Version metadata**: Embedded version information via AbcVersion

## Configuration

### Environment Configuration
- **Config directory**: Determined by `AppEnvironment` (typically user profile)
- **App config**: `{ConfigDir}/corney/config.json`
- **Crontab files**: Configurable paths in `CorneyConfig`

### Development Configuration
- **NLog config**: `src/Corney/NLog.config`
- **App settings**: `src/Corney/App.config`
- **Developer config**: JSON-based configuration in `dev/app.vs/config/`

## File Monitoring and Execution

The application continuously monitors crontab files and executes scheduled commands:
- Uses `FileSystemWatcher` for real-time file change detection
- Parses crontab format with support for standard cron expressions
- Executes commands using `ProcessWrapper` with proper error handling and logging
- Maintains execution history and provides status through system tray interface

## Build and Packaging

The NUKE build system provides:
- **Assembly patching**: Version metadata injection during build via AbcVersion
- **Single-file packaging**: .NET 8.0 single-file executable generation
- **Cross-platform builds**: Windows, Linux, macOS support for build system
- **GitHub Actions integration**: Automated CI/CD pipeline support
- **Artifact management**: ZIP archives and executable generation
- **Test integration**: Automated test execution with graceful failure handling
- **Version management**: Git-based semantic versioning with build metadata

### Build Outputs
- **Development**: `dev/app.vs/` for Visual Studio debugging
- **Local builds**: Via `PublishLocal` target
- **GitHub releases**: Single-file executable via `GithubRelease` target
- **Test results**: TRX files in `.nuke/temp/w/Corney/test-result/`

### Important Build Notes
- **Windows-only tests**: Test suite requires Windows due to Windows Forms dependencies
- **Platform detection**: Build system automatically detects Windows vs Linux/macOS
- **Git repository**: Requires proper git repository for version calculation
- **Line endings**: Automatic CRLF to LF conversion for cross-platform compatibility