# One text-only request. The API key goes to curl through stdin, never its command line or output files.
$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path -Parent $PSScriptRoot
$sessionDirectory = Join-Path $projectDirectory ('Validation\Runs\JevText_' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'))
$apiKey = [Environment]::GetEnvironmentVariable('TYPESAFE_API_KEY', 'User')
if ([string]::IsNullOrWhiteSpace($apiKey)) { $apiKey = [Environment]::GetEnvironmentVariable('TYPESAFE_API_KEY', 'Process') }
if ([string]::IsNullOrWhiteSpace($apiKey)) { throw 'TYPESAFE_API_KEY non disponibile per questo utente.' }
$apiKey = $apiKey.Trim()
if ($apiKey -match '[\r\n]') { throw 'La chiave contiene caratteri di nuova riga non validi.' }

New-Item -ItemType Directory -Force -Path $sessionDirectory | Out-Null
$request = [ordered]@{
    model = 'jev-1.14.0'
    state = 'The traffic light is red. The car is approaching the intersection.'
    questions = @{
        action = @{
            type = 'choice'
            instructions = 'Which action should the driver take?'
            criteria = @{
                stop = 'Stop before entering the intersection.'
                proceed = 'Continue driving through the intersection.'
            }
        }
    }
}
$requestFile = Join-Path $sessionDirectory 'request.json'
$utf8 = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllText($requestFile, ($request | ConvertTo-Json -Depth 8), $utf8)
$escapedKey = $apiKey.Replace('\', '\\').Replace('"', '\"')
$curlConfiguration = 'header = "Authorization: Bearer ' + $escapedKey + '"'
$timer = [Diagnostics.Stopwatch]::StartNew()
try {
    $curlLines = $curlConfiguration | & curl.exe --config - --request POST `
        --url 'https://api.typesafe.ai/v1/systemone' `
        --header 'Content-Type: application/json' `
        --data-binary ('@' + $requestFile) `
        --silent --show-error --connect-timeout 15 --max-time 40 --retry 0 `
        --proto '=https' --write-out '\n%{http_code}'
    $curlExitCode = $LASTEXITCODE
    $timer.Stop()
    $safeOutput = ($curlLines -join "`n").Replace($apiKey, '[REDACTED]')
    if ($curlExitCode -ne 0) { throw ('curl failed with exit code ' + $curlExitCode + '. No automatic retry performed.') }
    $splitAt = $safeOutput.LastIndexOf("`n")
    if ($splitAt -lt 0) { throw 'Missing HTTP status in curl output.' }
    $httpStatus = $safeOutput.Substring($splitAt + 1).Trim()
    $responseBody = $safeOutput.Substring(0, $splitAt)
    [IO.File]::WriteAllText((Join-Path $sessionDirectory 'response.json'), $responseBody, $utf8)
    $result = $responseBody | ConvertFrom-Json
    $summary = [ordered]@{
        utc = [DateTime]::UtcNow.ToString('O')
        httpStatus = $httpStatus
        latencyMs = [Math]::Round($timer.Elapsed.TotalMilliseconds, 1)
        requests = 1
        images = 0
        model = $result.model
        answers = $result.answers
        usage = $result.usage
        passed = ($httpStatus -eq '200' -and $result.answers.action.type -eq 'choice' -and $result.answers.action.choice -eq 'stop')
        outputDirectory = $sessionDirectory
    }
    $summaryJson = $summary | ConvertTo-Json -Depth 8
    [IO.File]::WriteAllText((Join-Path $sessionDirectory 'summary.json'), $summaryJson, $utf8)
    Write-Output $summaryJson
    if (-not $summary.passed) { exit 2 }
} finally {
    $apiKey = $null
    $escapedKey = $null
    $curlConfiguration = $null
}
