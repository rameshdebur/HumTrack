[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })]
    [string]$CaptureDirectory,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ExpectedDeviceId
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$resultPath = Join-Path $CaptureDirectory 'capture-result.json'
$framesPath = Join-Path $CaptureDirectory 'frames.csv'
$mediaPath = Join-Path $CaptureDirectory 'capture.mp4'
foreach ($requiredPath in @($resultPath, $framesPath, $mediaPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required capture artifact is missing: $requiredPath"
    }
}

$result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json -Depth 20
$rows = @(Import-Csv -LiteralPath $framesPath)
if ($result.device_id -ine $ExpectedDeviceId) { throw 'Capture result device identity differs from the expected exact identity.' }
if (-not $result.negotiated_exact) { throw 'Capture did not report exact profile negotiation.' }
if (-not $result.sink_finalized) { throw 'Capture sink did not finalize.' }
if ($result.PSObject.Properties.Name -contains 'requested_duration_reached' -and -not $result.requested_duration_reached) {
    throw "Capture ended before its requested duration: $($result.terminal_reason)"
}
if ($result.PSObject.Properties.Name -contains 'terminal_reason' -and $result.terminal_reason -ne 'duration_reached') {
    throw "Ordinary short-capture verification does not accept terminal reason: $($result.terminal_reason)"
}
if ($result.requested.width_px -ne 1920 -or $result.requested.height_px -ne 1080 -or
    $result.requested.fps_numerator -ne 30 -or $result.requested.fps_denominator -ne 1 -or
    $result.requested.subtype -ne 'H264') {
    throw 'Capture result does not describe the expected exact H.264 1080p30 diagnostic.'
}
if ($rows.Count -eq 0 -or $rows.Count -ne $result.sample_count) { throw 'Frame/sample evidence count is empty or inconsistent.' }

$sourceRegressions = 0
$arrivalRegressions = 0
$invalidRows = 0
for ($index = 0; $index -lt $rows.Count; $index++) {
    $row = $rows[$index]
    if ($row.run_id -ine $result.run_id -or $row.stream_id -ne 'master' -or
        $row.source_timestamp_domain -ne 'media_presentation' -or $row.source_timestamp_unit -ne 'ticks_100ns' -or
        $row.host_arrival_timestamp_domain -ne 'host_qpc' -or $row.host_arrival_timestamp_unit -ne 'ns' -or
        [uint64]$row.encoded_size_bytes -eq 0 -or [uint32]$row.width_px -ne 1920 -or [uint32]$row.height_px -ne 1080) {
        $invalidRows++
    }
    if ($index -gt 0) {
        if ([int64]$row.source_timestamp -lt [int64]$rows[$index - 1].source_timestamp) { $sourceRegressions++ }
        if ([uint64]$row.host_arrival_timestamp -lt [uint64]$rows[$index - 1].host_arrival_timestamp) { $arrivalRegressions++ }
    }
}
if ($invalidRows -ne 0) { throw "Capture contains $invalidRows invalid timing/sample rows." }
if ($sourceRegressions -ne 0 -or $arrivalRegressions -ne 0) { throw 'Capture timestamp ordering regressed.' }

$probeErrorPath = [System.IO.Path]::GetTempFileName()
try {
    $probeText = ffprobe -v error -count_frames -select_streams v:0 `
        -show_entries stream=codec_name,width,height,r_frame_rate,avg_frame_rate,duration,nb_frames,nb_read_frames `
        -show_entries format=size -of json $mediaPath 2> $probeErrorPath | Out-String
    $probeExit = $LASTEXITCODE
    $probeErrors = @(Get-Content -LiteralPath $probeErrorPath -ErrorAction SilentlyContinue)
}
finally {
    Remove-Item -LiteralPath $probeErrorPath -Force -ErrorAction SilentlyContinue
}
if ($probeExit -ne 0 -or $probeErrors.Count -ne 0) {
    $firstProbeError = if ($probeErrors.Count -gt 0) { $probeErrors[0] } else { 'no ffprobe message' }
    throw "ffprobe found an unreadable/corrupt media condition (exit $probeExit): $firstProbeError"
}
$probe = $probeText | ConvertFrom-Json -Depth 20
if ($probe.streams.Count -ne 1) { throw "Expected one video stream; found $($probe.streams.Count)." }
$stream = $probe.streams[0]
if ($stream.codec_name -ne 'h264' -or $stream.width -ne 1920 -or $stream.height -ne 1080) {
    throw 'Finalized media is not the expected 1920x1080 H.264 stream.'
}
if ([uint64]$stream.nb_read_frames -ne [uint64]$result.sample_count) {
    throw 'Decoded frame count differs from recorded sample count.'
}

if (-not (Get-Command ffmpeg -ErrorAction SilentlyContinue)) {
    throw 'ffmpeg is required to verify that every frame decodes without media errors.'
}
$decodeErrorPath = [System.IO.Path]::GetTempFileName()
try {
    & ffmpeg -hide_banner -nostdin -v error -xerror -i $mediaPath `
        -map 0:v:0 -f null NUL 2> $decodeErrorPath | Out-Null
    $decodeExit = $LASTEXITCODE
    $decodeErrors = @(Get-Content -LiteralPath $decodeErrorPath -ErrorAction SilentlyContinue)
}
finally {
    Remove-Item -LiteralPath $decodeErrorPath -Force -ErrorAction SilentlyContinue
}
if ($decodeExit -ne 0 -or $decodeErrors.Count -ne 0) {
    $firstDecodeError = if ($decodeErrors.Count -gt 0) { $decodeErrors[0] } else { 'no decoder message' }
    throw "Finalized media contains a decode error (exit $decodeExit): $firstDecodeError"
}

function Convert-Rational([string]$Value) {
    $parts = $Value.Split('/')
    if ($parts.Count -ne 2 -or [double]$parts[1] -eq 0) { throw "Invalid rational value: $Value" }
    return [double]$parts[0] / [double]$parts[1]
}

$averageFps = Convert-Rational $stream.avg_frame_rate
$nominalFps = Convert-Rational $stream.r_frame_rate
if ($averageFps -lt 29.0 -or $averageFps -gt 31.0) {
    throw "Measured average frame rate is outside the diagnostic 30 fps band: $averageFps"
}

[ordered]@{
    status = 'VALID_SHORT_CAPTURE'
    work_tags = @('P0.2B', 'UVC', 'CORE', 'CAPTURE', 'TIMING', 'HARDWARE')
    run_id = $result.run_id
    device_id = $result.device_id
    samples = $result.sample_count
    encoded_bytes = $result.encoded_bytes
    duration_seconds = [double]$stream.duration
    average_fps = $averageFps
    nominal_stream_fps = $nominalFps
    nominal_rate_differs_from_average = [Math]::Abs($nominalFps - $averageFps) -gt 1.0
    stream_ticks = $result.stream_tick_count
    media_type_changes = $result.media_type_change_count
    timestamp_regressions = $result.timestamp_regression_count
    decoded_frames = [uint64]$stream.nb_read_frames
    decode_errors = 0
    media_size_bytes = [uint64]$probe.format.size
} | ConvertTo-Json -Depth 10
