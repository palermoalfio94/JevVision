param([ValidateSet('manual','jev')][string]$Mode = 'jev')
$projectDirectory = Split-Path -Parent $PSScriptRoot
$playerPath = Join-Path $projectDirectory 'Builds\Windows\JevVisionLab.exe'
if (-not (Test-Path -LiteralPath $playerPath)) { throw 'Build mancante. In Unity: JEV Vision > Build Windows player and validate.' }
& $playerPath -screen-fullscreen 0 -screen-width 1440 -screen-height 900 -jev-mode $Mode

