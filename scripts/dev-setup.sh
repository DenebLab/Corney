#!/bin/bash
set -euo pipefail

# Corney Developer Environment Setup Script (Linux/macOS)
# 
# This script automates the setup of a complete development environment for Corney.
# Handles dependency verification, environment configuration, and development tool setup.

# Configuration
REQUIRED_DOTNET_VERSION="8.0"
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEV_CONFIG_PATH="$PROJECT_ROOT/dev"
APP_VS_PATH="$DEV_CONFIG_PATH/app.vs"

# Color output functions
print_success() { echo -e "\033[0;32m✅ $1\033[0m"; }
print_warning() { echo -e "\033[0;33m⚠️  $1\033[0m"; }
print_error() { echo -e "\033[0;31m❌ $1\033[0m"; }
print_info() { echo -e "\033[0;36mℹ️  $1\033[0m"; }
print_step() { echo -e "\n\033[0;34m🔧 $1\033[0m"; }

# Command line options
SKIP_DEPENDENCIES=false
CONFIGURE_VSCODE=false
SETUP_DEBUGGING=false
FORCE=false
VERBOSE=false

show_help() {
    cat << EOF
Corney Developer Environment Setup Script

Usage: $0 [OPTIONS]

Options:
    -s, --skip-dependencies     Skip dependency verification
    -v, --vscode               Configure VS Code extensions and workspace
    -d, --debug                Setup debugging configuration
    -f, --force                Force overwrite existing configurations
    -V, --verbose              Enable verbose output
    -h, --help                 Show this help message

Examples:
    $0 --vscode --debug        Setup with VS Code and debugging
    $0 --skip-dependencies     Skip dependency checks (for CI/CD)
EOF
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -s|--skip-dependencies)
            SKIP_DEPENDENCIES=true
            shift
            ;;
        -v|--vscode)
            CONFIGURE_VSCODE=true
            shift
            ;;
        -d|--debug)
            SETUP_DEBUGGING=true
            shift
            ;;
        -f|--force)
            FORCE=true
            shift
            ;;
        -V|--verbose)
            VERBOSE=true
            set -x
            shift
            ;;
        -h|--help)
            show_help
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            show_help
            exit 1
            ;;
    esac
done

command_exists() {
    command -v "$1" >/dev/null 2>&1
}

check_dotnet_version() {
    if ! command_exists dotnet; then
        return 1
    fi
    
    local version
    version=$(dotnet --version)
    local major_minor
    major_minor=$(echo "$version" | cut -d. -f1-2)
    
    if [[ $(echo "$major_minor >= $REQUIRED_DOTNET_VERSION" | bc -l) -eq 1 ]]; then
        return 0
    else
        return 1
    fi
}

install_vscode_extensions() {
    local extensions=(
        "ms-dotnettools.csharp"
        "ms-dotnettools.vscode-dotnet-runtime"
        "formulahendry.dotnet-test-explorer"
        "jmrog.vscode-nuget-package-manager"
        "ms-vscode.powershell"
        "redhat.vscode-xml"
        "yzhang.markdown-all-in-one"
        "streetsidesoftware.code-spell-checker"
        "ms-vscode.vscode-json"
    )
    
    print_step "Installing VS Code extensions..."
    for extension in "${extensions[@]}"; do
        if $VERBOSE; then
            print_info "Installing extension: $extension"
        fi
        
        if code --install-extension "$extension" --force >/dev/null 2>&1; then
            print_success "Installed: $extension"
        else
            print_warning "Failed to install: $extension"
        fi
    done
}

create_vscode_workspace() {
    local workspace_config
    workspace_config=$(cat << 'EOF'
{
    "folders": [
        { "path": "." }
    ],
    "settings": {
        "dotnet.defaultSolution": "src/Corney.sln",
        "files.exclude": {
            "**/bin": true,
            "**/obj": true,
            "**/.vs": true
        },
        "csharp.semanticHighlighting.enabled": true,
        "omnisharp.enableEditorConfigSupport": true,
        "omnisharp.enableRoslynAnalyzers": true
    },
    "extensions": {
        "recommendations": [
            "ms-dotnettools.csharp",
            "ms-dotnettools.vscode-dotnet-runtime"
        ]
    }
}
EOF
    )
    
    local workspace_path="$PROJECT_ROOT/Corney.code-workspace"
    echo "$workspace_config" > "$workspace_path"
    print_success "Created VS Code workspace: $workspace_path"
}

setup_development_config() {
    print_step "Setting up development configuration..."
    
    # Create directories
    local dirs=(
        "$DEV_CONFIG_PATH"
        "$APP_VS_PATH"
        "$APP_VS_PATH/config"
        "$APP_VS_PATH/config/corney"
        "$APP_VS_PATH/log"
    )
    
    for dir in "${dirs[@]}"; do
        if [[ ! -d "$dir" ]]; then
            mkdir -p "$dir"
            print_success "Created directory: $dir"
        fi
    done
    
    # Create sample crontab file
    local sample_crontab
    sample_crontab=$(cat << 'EOF'
# Sample crontab file for Corney development
# Format: [minute] [hour] [day] [month] [day_of_week] [command]

# Run a simple command every minute (for testing)
# */1 * * * * echo "Hello from Corney!" > /tmp/corney-test.txt

# Run a shell script every 5 minutes
# */5 * * * * /home/user/scripts/monitor.sh

# Run a command daily at 9 AM
# 0 9 * * * /home/user/scripts/daily-backup.sh

# Examples of different time formats:
# 0 * * * *     - Every hour
# 0 0 * * *     - Every day at midnight
# 0 0 * * 0     - Every Sunday at midnight
# 0 0 1 * *     - First day of every month
EOF
    )
    
    local crontab_path="$APP_VS_PATH/config/corney/crontab.txt"
    if [[ ! -f "$crontab_path" || "$FORCE" == true ]]; then
        echo "$sample_crontab" > "$crontab_path"
        print_success "Created sample crontab: $crontab_path"
    fi
    
    # Create development configuration
    local dev_config
    dev_config=$(cat << EOF
{
    "CrontabFiles": ["$crontab_path"],
    "CheckIntervalSeconds": 10,
    "FileMonitoringDebounceSeconds": 2
}
EOF
    )
    
    local config_path="$APP_VS_PATH/config/corney/config.json"
    if [[ ! -f "$config_path" || "$FORCE" == true ]]; then
        echo "$dev_config" > "$config_path"
        print_success "Created development config: $config_path"
    fi
}

setup_debug_configuration() {
    print_step "Setting up debugging configuration..."
    
    local vscode_dir="$PROJECT_ROOT/.vscode"
    [[ ! -d "$vscode_dir" ]] && mkdir -p "$vscode_dir"
    
    # Create launch.json for VS Code debugging
    local launch_config
    launch_config=$(cat << 'EOF'
{
    "version": "0.2.0",
    "configurations": [
        {
            "name": "Launch Corney (Debug)",
            "type": "coreclr",
            "request": "launch",
            "program": "${workspaceFolder}/src/Corney/bin/Debug/net8.0-windows/Corney.exe",
            "args": [],
            "cwd": "${workspaceFolder}",
            "console": "internalConsole",
            "stopAtEntry": false,
            "env": {
                "ASPNETCORE_ENVIRONMENT": "Development",
                "CORNEY_ENV": "Development"
            }
        },
        {
            "name": "Attach to Corney Process",
            "type": "coreclr",
            "request": "attach",
            "processName": "Corney.exe"
        }
    ]
}
EOF
    )
    
    local launch_path="$vscode_dir/launch.json"
    if [[ ! -f "$launch_path" || "$FORCE" == true ]]; then
        echo "$launch_config" > "$launch_path"
        print_success "Created VS Code launch configuration: $launch_path"
    fi
    
    # Create tasks.json for build tasks
    local tasks_config
    tasks_config=$(cat << 'EOF'
{
    "version": "2.0.0",
    "tasks": [
        {
            "label": "build",
            "command": "dotnet",
            "type": "process",
            "args": ["build", "${workspaceFolder}/src/Corney/Corney.csproj"],
            "group": {
                "kind": "build",
                "isDefault": true
            },
            "presentation": {
                "echo": true,
                "reveal": "silent",
                "focus": false,
                "panel": "shared"
            },
            "problemMatcher": "$msCompile"
        },
        {
            "label": "test",
            "command": "dotnet",
            "type": "process",
            "args": ["test", "${workspaceFolder}/src/Corney.Tests/Corney.Tests.csproj"],
            "group": "test",
            "presentation": {
                "echo": true,
                "reveal": "always",
                "focus": false,
                "panel": "shared"
            }
        },
        {
            "label": "clean",
            "command": "dotnet",
            "type": "process",
            "args": ["clean", "${workspaceFolder}/src/Corney.sln"],
            "group": "build"
        }
    ]
}
EOF
    )
    
    local tasks_path="$vscode_dir/tasks.json"
    if [[ ! -f "$tasks_path" || "$FORCE" == true ]]; then
        echo "$tasks_config" > "$tasks_path"
        print_success "Created VS Code tasks configuration: $tasks_path"
    fi
}

test_build_environment() {
    print_step "Testing build environment..."
    
    cd "$PROJECT_ROOT/src"
    
    print_info "Restoring NuGet packages..."
    dotnet restore Corney.sln
    
    print_info "Building solution..."
    dotnet build Corney.sln --no-restore
    
    print_info "Running tests..."
    dotnet test Corney.Tests/Corney.Tests.csproj --no-build --verbosity minimal
    
    print_success "Build environment is working correctly!"
    
    cd "$PROJECT_ROOT"
}

create_developer_guide() {
    local guide
    guide=$(cat << 'EOF'
# Corney Developer Setup Guide (Linux/macOS)

This guide will help you set up a complete development environment for Corney on Linux or macOS.

## Quick Start

1. **Run the setup script:**
   ```bash
   ./scripts/dev-setup.sh --vscode --debug
   ```

2. **Open the project:**
   - VS Code: Open `Corney.code-workspace`
   - Or open `src/Corney.sln` in your preferred IDE

3. **Start development:**
   ```bash
   dotnet run --project src/Corney/Corney.csproj
   ```

## Development Workflow

### Building
```bash
# Build the solution
dotnet build src/Corney.sln

# Build and run
dotnet run --project src/Corney/Corney.csproj
```

### Testing
```bash
# Run all tests
dotnet test src/Corney.Tests/

# Run tests with coverage
dotnet test src/Corney.Tests/ --collect:"XPlat Code Coverage"
```

### Cross-Platform Considerations

Corney is primarily designed for Windows, but the development environment works on Linux/macOS:
- File paths use forward slashes in configuration
- Process execution may behave differently
- Some Windows-specific features may not work

## Configuration

Development configuration is in `dev/app.vs/config/corney/`:
- `config.json` - Main application configuration  
- `crontab.txt` - Sample crontab file for testing

## Debugging

Use VS Code with C# extension for the best debugging experience:
- Set breakpoints in your code
- Press F5 to start debugging
- Use the Debug Console for interactive debugging

## Troubleshooting

### .NET Installation
If .NET 8.0 is not installed:
```bash
# Ubuntu/Debian
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt update && sudo apt install -y dotnet-sdk-8.0

# macOS (with Homebrew)
brew install --cask dotnet-sdk
```

### VS Code Setup
```bash
# Install VS Code (Ubuntu/Debian)
wget -qO- https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > packages.microsoft.gpg
sudo install -o root -g root -m 644 packages.microsoft.gpg /etc/apt/trusted.gpg.d/
sudo sh -c 'echo "deb [arch=amd64,arm64,armhf signed-by=/etc/apt/trusted.gpg.d/packages.microsoft.gpg] https://packages.microsoft.com/repos/code stable main" > /etc/apt/sources.list.d/vscode.list'
sudo apt update && sudo apt install code

# macOS (with Homebrew)
brew install --cask visual-studio-code
```
EOF
    )
    
    local guide_path="$PROJECT_ROOT/DEVELOPER-UNIX.md"
    if [[ ! -f "$guide_path" || "$FORCE" == true ]]; then
        echo "$guide" > "$guide_path"
        print_success "Created Unix developer guide: $guide_path"
    fi
}

# Main execution
main() {
    echo -e "\n\033[0;35m🚀 Corney Developer Environment Setup (Linux/macOS)\033[0m"
    echo -e "\033[0;35m=====================================================\033[0m\n"
    
    # Dependency checks
    if [[ "$SKIP_DEPENDENCIES" != true ]]; then
        print_step "Verifying dependencies..."
        
        if ! command_exists dotnet; then
            print_error ".NET SDK not found. Please install .NET 8.0 SDK"
            print_info "Visit: https://dotnet.microsoft.com/download"
            exit 1
        fi
        
        if ! check_dotnet_version; then
            print_error ".NET 8.0 or later required. Current version: $(dotnet --version)"
            exit 1
        fi
        
        print_success ".NET SDK $(dotnet --version) is installed"
        
        if ! command_exists git; then
            print_warning "Git not found. Some features may not work properly."
        else
            print_success "Git is available"
        fi
        
        if ! command_exists bc; then
            print_warning "bc calculator not found. Version comparison may not work."
        fi
    fi
    
    # Setup development environment
    setup_development_config
    create_developer_guide
    
    if [[ "$SETUP_DEBUGGING" == true ]]; then
        setup_debug_configuration
    fi
    
    if [[ "$CONFIGURE_VSCODE" == true ]]; then
        if command_exists code; then
            install_vscode_extensions
            create_vscode_workspace
            print_success "VS Code configured successfully"
        else
            print_warning "VS Code not found. Skipping VS Code configuration."
            print_info "Install VS Code and run with --vscode flag"
        fi
    fi
    
    # Test the build environment
    test_build_environment
    
    echo -e "\n\033[0;32m🎉 Development environment setup complete!\033[0m"
    echo -e "\033[0;36m📖 See DEVELOPER-UNIX.md for detailed workflow\033[0m"
    echo -e "\033[0;36m🚀 Run 'dotnet run --project src/Corney/Corney.csproj' to start\033[0m"
}

# Run main function with error handling
if ! main "$@"; then
    print_error "Setup failed. Check the error messages above."
    echo -e "\nFor help, run: $0 --help"
    exit 1
fi