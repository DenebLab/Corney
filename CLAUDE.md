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
- `PublishRobeNova` : Complete build on build server
- `Clean`: Clean build artifacts 
- `Restore`: Restore NuGet packages
- `PublishLocalStandalone`: Build and copy to dev/app.standalone/

### Testing
- **Run tests**: Use xUnit test runner with `dotnet test` or Visual Studio
- **Test projects**: Located in `src/Corney.Tests/`
- **Test framework**: xUnit 2.4.1

### Development Environment
- **Local standalone**: `dev/app.standalone/Corney.exe` (built executable)
- **VS debugging**: `dev/app.vs/` contains config and logs for development
- **Config files**: Located in `dev/app.vs/config/corney/`

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
- **Assembly patching**: Version metadata injection during build
- **Dependency merging**: LibZ for single-executable packaging
- **NuGet packaging**: Automated package creation
- **Azure DevOps integration**: Automated CI/CD pipeline support
- **Artifact management**: ZIP and NuGet package generation