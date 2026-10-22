$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root
$reportDir = Join-Path $root "artifacts"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$report = Join-Path $reportDir "build-report.txt"
"ZTE Network Manager build report - $(Get-Date -Format o)" | Set-Content $report
function Invoke-Step([string]$Title, [scriptblock]$Action) {
    Add-Content $report "`n=== $Title ==="
    & $Action 2>&1 | Tee-Object -FilePath $report -Append
    if ($LASTEXITCODE -ne 0) { Add-Content $report "FAILED: $Title (exit $LASTEXITCODE)"; throw "$Title failed; see $report" }
    Add-Content $report "PASSED: $Title"
}
Invoke-Step "SDK info" { dotnet --info }
Invoke-Step "Restore" { dotnet restore .\ZteNetManager.sln }
Invoke-Step "Build Release" { dotnet build .\ZteNetManager.sln -c Release --no-restore }
$testProjects = Get-ChildItem -Path $root -Filter "*Tests.csproj" -Recurse
if ($testProjects.Count -gt 0) { Invoke-Step "Tests" { dotnet test .\ZteNetManager.sln -c Release --no-build } }
else { Add-Content $report "Tests not run: no test project exists yet." }
Invoke-Step "Publish Windows x64" { dotnet publish .\src\ZteNetManager.App\ZteNetManager.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\artifacts\publish }
Write-Host "Build complete. Review $report and artifacts\publish."
Write-Host "Installer packaging is not yet configured; this script does not claim to produce an installer."
