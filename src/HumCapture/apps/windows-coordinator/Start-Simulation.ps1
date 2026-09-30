$ErrorActionPreference = 'Stop'
$captureRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$desktopProject = Join-Path $PSScriptRoot 'src\HumCapture.Coordinator.Desktop\HumCapture.Coordinator.Desktop.csproj'
& dotnet build $desktopProject -c Release -p:RestoreLockedMode=true
if ($LASTEXITCODE -ne 0) { throw 'HumCapture desktop build failed. Simulation was not started.' }
$desktopExecutable = Join-Path $PSScriptRoot 'src\HumCapture.Coordinator.Desktop\bin\Release\net10.0-windows10.0.19041.0\HumCapture.Coordinator.Desktop.exe'
$simulationRoot = Join-Path $captureRoot 'evidence-vault\coordinator-ui-simulation'
& $desktopExecutable --workspace $simulationRoot
