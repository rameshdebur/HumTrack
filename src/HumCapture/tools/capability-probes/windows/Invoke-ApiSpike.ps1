[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$DeviceId,

    [string]$OutputPath,

    [switch]$RequireEqualFormatSets,

    [switch]$IncludeAllDifferences
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = $PSScriptRoot
$managedProject = Join-Path $root 'managed\HumCapture.ManagedCameraProbe.csproj'
$nativeProject = Join-Path $root 'native-mf\HumCapture.MfEnumerator.vcxproj'
$nativeExecutable = Join-Path $root 'native-mf\x64\Release\HumCapture.MfEnumerator.exe'
$msbuild = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'

if (-not (Test-Path -LiteralPath $msbuild)) {
    throw "MSBuild was not found at the expected Visual Studio 18 path: $msbuild"
}

dotnet build $managedProject -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Managed probe build failed with exit code $LASTEXITCODE." }

& $msbuild $nativeProject /m /nologo /verbosity:minimal /p:Configuration=Release /p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw "Native probe build failed with exit code $LASTEXITCODE." }

$managedText = dotnet run --project $managedProject -c Release --no-build -- --device-id $DeviceId | Out-String
if ($LASTEXITCODE -ne 0) { throw "Managed probe failed with exit code $LASTEXITCODE.`n$managedText" }

$nativeText = & $nativeExecutable | Out-String
if ($LASTEXITCODE -ne 0) { throw "Native probe failed with exit code $LASTEXITCODE.`n$nativeText" }

$managed = $managedText | ConvertFrom-Json -Depth 20
$native = $nativeText | ConvertFrom-Json -Depth 20

if ($managed.devices.Count -ne 1) {
    throw "Exact managed selection returned $($managed.devices.Count) devices instead of one."
}

$managedDevice = $managed.devices[0]
$nativeMatches = @($native.devices | Where-Object {
    $_.interface_id -ieq $managedDevice.interface_id
})
if ($nativeMatches.Count -ne 1) {
    throw "The selected managed identity matched $($nativeMatches.Count) native Media Foundation devices instead of one."
}
$nativeDevice = $nativeMatches[0]

$subtypeNames = @{
    '{3231564E-0000-0010-8000-00AA00389B71}' = 'NV12'
    '{32595559-0000-0010-8000-00AA00389B71}' = 'YUY2'
    '{34363248-0000-0010-8000-00AA00389B71}' = 'H264'
    '{47504A4D-0000-0010-8000-00AA00389B71}' = 'MJPG'
}

function Get-ManagedSignature($format) {
    return "$($format.width_px)x$($format.height_px)|$($format.fps_numerator)/$($format.fps_denominator)|$($format.subtype.ToUpperInvariant())"
}

function Get-NativeSignature($format) {
    $guid = $format.subtype_guid.ToUpperInvariant()
    $subtype = if ($subtypeNames.ContainsKey($guid)) { $subtypeNames[$guid] } else { $guid }
    return "$($format.width_px)x$($format.height_px)|$($format.fps_numerator)/$($format.fps_denominator)|$subtype"
}

$managedSignatures = @($managedDevice.video_record_formats | ForEach-Object { Get-ManagedSignature $_ } | Sort-Object -Unique)
$nativeSignatures = @($nativeDevice.native_media_types | ForEach-Object { Get-NativeSignature $_ } | Sort-Object -Unique)
$managedOnly = @($managedSignatures | Where-Object { $_ -notin $nativeSignatures })
$nativeOnly = @($nativeSignatures | Where-Object { $_ -notin $managedSignatures })
$formatSetsEqual = $managedOnly.Count -eq 0 -and $nativeOnly.Count -eq 0
$formatRelation = if ($formatSetsEqual) {
    'equal'
} elseif ($managedOnly.Count -eq 0) {
    'native_superset'
} elseif ($nativeOnly.Count -eq 0) {
    'managed_superset'
} else {
    'overlap'
}
[string[]]$managedOnlyOutput = if ($IncludeAllDifferences) { @($managedOnly) } else { @($managedOnly | Select-Object -First 20) }
[string[]]$nativeOnlyOutput = if ($IncludeAllDifferences) { @($nativeOnly) } else { @($nativeOnly | Select-Object -First 20) }

$supportedControls = @($managedDevice.camera_controls | Where-Object supported | Select-Object -ExpandProperty control_id)
$result = [ordered]@{
    format_name = 'humcapture-windows-api-spike-comparison'
    format_version = '0.1.0'
    work_tags = @('P0.2A', 'UVC', 'CORE', 'ENUMERATION', 'HARDWARE')
    generated_utc = [DateTimeOffset]::UtcNow.ToString('O')
    requested_device_id = $DeviceId
    identity_match = $true
    managed_name = $managedDevice.name
    native_name = $nativeDevice.name
    interface_id = $managedDevice.interface_id
    managed_format_count = $managedSignatures.Count
    native_format_count = $nativeSignatures.Count
    format_sets_equal = $formatSetsEqual
    format_relation = $formatRelation
    managed_only_format_count = $managedOnly.Count
    native_only_format_count = $nativeOnly.Count
    managed_only_format_examples = $managedOnlyOutput
    native_only_format_examples = $nativeOnlyOutput
    managed_supported_controls = $supportedControls
    managed_initialization_status = $managedDevice.initialization.status
    native_inspection_status = $nativeDevice.inspection_status
}

$json = $result | ConvertTo-Json -Depth 10
if ($OutputPath) {
    $parent = Split-Path -Parent $OutputPath
    if ($parent) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    Set-Content -LiteralPath $OutputPath -Value $json -Encoding utf8NoBOM
}
$json

if ($RequireEqualFormatSets -and -not $formatSetsEqual) {
    exit 3
}
