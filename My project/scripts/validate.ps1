param([ValidateSet('setup','setup-jev','setup-screen','manual','resets','no-key','frame','jev-frame','jev-smoke','jev')][string]$Test = 'frame', [float]$Seconds = 0)
$projectDirectory = Split-Path -Parent $PSScriptRoot
$playerPath = Join-Path $projectDirectory 'Builds\Windows\JevVisionLab.exe'
if (-not (Test-Path -LiteralPath $playerPath)) { throw 'Build mancante. In Unity: JEV Vision > Build Windows player and validate.' }
$mode = if ($Test -in @('manual','resets','frame')) { 'manual' } else { 'jev' }
$duration = switch ($Test) { 'jev-frame' { 25 } 'jev-smoke' { 60 } 'jev' { 600 } 'resets' { 15 } default { 7 } }
if ($Seconds -gt 0) { $duration = $Seconds }
$outputDirectory = Join-Path $projectDirectory 'Validation\Runs'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$logFile = Join-Path $projectDirectory ('Validation\player-' + $Test + '.log')
$renderMode = if ($Test -eq 'setup-screen') { '' } else { '-batchmode ' }
$arguments = $renderMode + '-screen-fullscreen 0 -screen-width 1440 -screen-height 900 -logFile "' + $logFile + '" -jev-validation ' + $Test + ' -jev-mode ' + $mode + ' -jev-seconds ' + $duration + ' -jev-output "' + $outputDirectory + '"'
$windowStyle = if ($Test -eq 'setup-screen') { 'Normal' } else { 'Hidden' }
$process = Start-Process -FilePath $playerPath -ArgumentList $arguments -WindowStyle $windowStyle -PassThru -Wait
Write-Host ('Validation ' + $Test + ': exit code ' + $process.ExitCode)
exit $process.ExitCode

