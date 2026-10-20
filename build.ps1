param(
    [switch]$Publish,
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')]
    [string]$PackageName = 'QuietMonitor-win-x64'
)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$localSdk = Join-Path $PSScriptRoot '.tools\dotnet\dotnet.exe'
$dotnetCommand = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { (Get-Command dotnet -ErrorAction Stop).Source }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.tools\cli-home'
& $dotnetCommand build 'src\QuietMonitor.App\QuietMonitor.App.csproj' -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
& $dotnetCommand run --project 'tests\QuietMonitor.Checks\QuietMonitor.Checks.csproj' -c Release
if ($LASTEXITCODE -ne 0) { throw 'Core checks failed.' }
if ($Publish) {
    $publishDirectory = Join-Path (Join-Path $PSScriptRoot 'artifacts') $PackageName
    & $dotnetCommand publish 'src\QuietMonitor.App\QuietMonitor.App.csproj' -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $publishDirectory --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $publishDirectory 'README.md')
    # Use distinct names so the bundled .NET runtime license/notices remain intact.
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination (Join-Path $publishDirectory 'QUIETMONITOR-LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'THIRD_PARTY_NOTICES.md') -Destination (Join-Path $publishDirectory 'QUIETMONITOR-NOTICES.md')
    # Publish does not automatically copy NuGet package license files.
    $assets = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'src\QuietMonitor.App\obj\project.assets.json') -Raw | ConvertFrom-Json
    $runtimeConfig = Get-Content -LiteralPath (Join-Path $publishDirectory 'QuietMonitor.runtimeconfig.json') -Raw | ConvertFrom-Json
    $frameworks = @($runtimeConfig.runtimeOptions.includedFrameworks)
    if ($frameworks.Count -ne 2) { throw 'Expected Core and Windows Desktop runtime packages for license collection.' }
    foreach ($framework in $frameworks) {
        $packagePath = "$($framework.name.ToLowerInvariant()).runtime.win-x64/$($framework.version)"
        $packageDirectory = $null
        foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
            $candidate = Join-Path $folder $packagePath
            if (Test-Path -LiteralPath $candidate) { $packageDirectory = $candidate; break }
        }
        if (-not $packageDirectory) { throw "Runtime package missing: $packagePath" }
        $noticeFiles = @(Get-ChildItem -LiteralPath $packageDirectory -File | Where-Object { $_.Name -match '^(LICENSE(\.TXT)?|THIRD.PARTY.NOTICES(\.TXT)?)$' })
        if (-not ($noticeFiles | Where-Object { $_.Name -match '^LICENSE' })) { throw "Runtime license missing: $packagePath" }
        $noticeDirectory = Join-Path (Join-Path $publishDirectory 'licenses') $packagePath
        New-Item -ItemType Directory -Path $noticeDirectory -Force | Out-Null
        foreach ($notice in $noticeFiles) { Copy-Item -LiteralPath $notice.FullName -Destination $noticeDirectory }
    }
    if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'VALIDATION.md')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'VALIDATION.md') -Destination (Join-Path $publishDirectory 'VALIDATION.md')
    }
    Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath (Join-Path $PSScriptRoot "artifacts\$PackageName.zip") -Force
    Write-Output "Ready: $publishDirectory\QuietMonitor.exe"
}
