param([ValidateSet('lap','manual','resets','no-key','frame')][string]$Test = 'lap', [float]$Seconds = 0)
$projectDirectory = Split-Path -Parent $PSScriptRoot
$playerPath = Join-Path $projectDirectory 'Builds\Windows\JevVisionLab.exe'
if (-not (Test-Path -LiteralPath $playerPath)) { throw 'Build mancante. In Unity: JEV Vision > Build Windows player and validate.' }
if ($Test -eq 'no-key') {
    $configuredKey = [Environment]::GetEnvironmentVariable('TYPESAFE_API_KEY', 'Process')
    if ([string]::IsNullOrWhiteSpace($configuredKey)) { $configuredKey = [Environment]::GetEnvironmentVariable('TYPESAFE_API_KEY', 'User') }
    if (-not [string]::IsNullOrWhiteSpace($configuredKey)) { throw 'Test no-key annullato: esiste gia una chiave. Nessuna chiamata avviata.' }
}
$mode = if ($Test -eq 'no-key') { 'jev' } elseif ($Test -eq 'manual' -or $Test -eq 'resets') { 'manual' } else { 'local' }
$duration = if ($Test -eq 'lap') { 160 } elseif ($Test -eq 'resets') { 15 } else { 7 }
if ($Seconds -gt 0) { $duration = $Seconds }
$outputDirectory = Join-Path $projectDirectory 'Validation\Runs'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$logFile = Join-Path $projectDirectory ('Validation\player-' + $Test + '.log')
$arguments = '-batchmode -screen-fullscreen 0 -screen-width 1440 -screen-height 900 -logFile "' + $logFile + '" -jev-validation ' + $Test + ' -jev-mode ' + $mode + ' -jev-seconds ' + $duration + ' -jev-output "' + $outputDirectory + '"'
$process = Start-Process -FilePath $playerPath -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
Write-Host ('Validation ' + $Test + ': exit code ' + $process.ExitCode)
exit $process.ExitCode

