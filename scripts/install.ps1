param (
    [string]$Version,
    [switch]$ForceUpdate,
    [switch]$SkipShortcut,
    [switch]$SkipStartup,
    [switch]$Silent
)

# --------- CONFIG ---------
$org = "Deneblab"
$repo = "Corney"
$appName = "Corney"
$launchArgs = ""

$baseDir = "$env:LOCALAPPDATA\Deneblab\$appName"
$appRoot = Join-Path $baseDir "app"
$logDir = Join-Path $baseDir "log"
$configDir = Join-Path $baseDir "config"
$syrupDir = Join-Path $baseDir ".syrup"

# --------- FLAGS ---------
Write-Host "Installer flags:"
if ($ForceUpdate)   { Write-Host " --ForceUpdate  = true" }
if ($Version)       { Write-Host " --Version       = $Version" }
if ($SkipShortcut)  { Write-Host " --SkipShortcut  = true" }
if ($SkipStartup)   { Write-Host " --SkipStartup   = true" }
if ($Silent)        { Write-Host " --Silent        = true" }

# --------- UTILITY: Safe Directory Deletion ---------
function TryRemoveDirectory($path, $maxAttempts = 5) {
    for ($i = 1; $i -le $maxAttempts; $i++) {
        try {
            if (Test-Path $path) {
                Write-Host "Attempting to delete: $path (Attempt $i)"
                Remove-Item -Recurse -Force -Path $path -ErrorAction Stop
                return $true
            } else {
                return $true
            }
        } catch {
            Write-Warning "Could not delete $path (attempt $i): $($_.Exception.Message)"
            Start-Sleep -Seconds 2
        }
    }
    return $false
}

# --------- PREP ---------
New-Item -Path $appRoot -ItemType Directory -Force | Out-Null
New-Item -Path $logDir -ItemType Directory -Force | Out-Null
New-Item -Path $configDir -ItemType Directory -Force | Out-Null
New-Item -Path $syrupDir -ItemType Directory -Force | Out-Null

# --------- FETCH RELEASE INFO ---------
$headers = @{ "User-Agent" = "$appName-Installer" }

if ($Version -and ($Version -notmatch '^--')) {
    $releaseUrl = "https://api.github.com/repos/$org/$repo/releases/tags/v$Version"
} else {
    $releaseUrl = "https://api.github.com/repos/$org/$repo/releases/latest"
}

try {
    $release = Invoke-RestMethod -Uri $releaseUrl -Headers $headers
} catch {
    Write-Error "❌ Failed to get release info for version: $Version"
    exit 1
}

$version = $release.tag_name -replace '^v', ''
$versionDir = Join-Path $appRoot "$appName.$version"

# --------- HANDLE EXISTING INSTALL ---------
if ((Test-Path "$versionDir") -and (-not $ForceUpdate)) {
    Write-Host "$appName v$version is already installed. Launching existing version..."

    $exeFiles = Get-ChildItem -Path $versionDir -Recurse -Filter *.exe -File
    if ($exeFiles.Count -eq 0) {
        Write-Warning "No .exe file found in $versionDir"
        exit 1
    }

    $exeFile = $exeFiles | Where-Object { $_.Name -like "$appName*.exe" } | Select-Object -First 1
    if (-not $exeFile) {
        $exeFile = $exeFiles | Select-Object -First 1
        Write-Warning "No .exe matching '$appName*.exe'. Using fallback: $($exeFile.Name)"
    }

    if (-not $Silent) {
        Write-Host "Launching: $($exeFile.FullName)"
        $p = Start-Process -FilePath $exeFile.FullName -ArgumentList $launchArgs -PassThru
        Write-Host "$appName launched. PID: $($p.Id)"
    } else {
        Write-Host "Silent mode: skipping app launch"
    }

    exit
}

if ($ForceUpdate -and (Test-Path "$versionDir")) {
    # Kill running processes
    $procList = Get-Process | Where-Object { $_.Path -like "$versionDir\*.exe" }
    foreach ($p in $procList) {
        Write-Host "🔪 Killing process: $($p.ProcessName) [PID $($p.Id)]"
        try { $p.Kill() } catch { Write-Warning "Failed to kill process $($p.Id): $($_.Exception.Message)" }
    }

    Write-Host "⚠ --ForceUpdate enabled — removing existing $versionDir"
    $success = TryRemoveDirectory $versionDir
    if (-not $success) {
        Write-Error "❌ Failed to remove $versionDir. Please close running app or files."
        exit 1
    }
}

# (continue script here... e.g. download/extract/install/run)
