<#
.SYNOPSIS
    Полная проверка Go Engine: сборка, тесты и проверочные режимы приложения.

.DESCRIPTION
    Одна команда для «definition of done»: собрать решение без предупреждений, прогнать оба
    тестовых проекта, затем проверить приложение во всех режимах без окна — геометрию щелчка,
    панель статуса с ходом AI, настройки, SGF, анимацию, полный сценарий партии и нагрузку.

.PARAMETER SkipStress
    Пропустить проверку нагрузки: она играет сто партий и занимает около десяти секунд.

.EXAMPLE
    ./check.ps1
    ./check.ps1 -SkipStress
#>
[CmdletBinding()]
param(
    [switch] $SkipStress
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root 'src'
$app = Join-Path $src 'GoEngine.App.Desktop/GoEngine.App.Desktop.csproj'
$failures = 0

function Invoke-Step {
    param([string] $Title, [scriptblock] $Action)

    Write-Host "Go Engine: $Title"
    & $Action
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  не пройдено: $Title" -ForegroundColor Red
        $script:failures++
    }
}

Invoke-Step 'сборка решения' { dotnet build (Join-Path $src 'GoEngine.sln') --nologo }
Invoke-Step 'тесты' { dotnet test (Join-Path $src 'GoEngine.sln') --nologo }

$modes = @('--smoke', '--check', '--state', '--settings', '--sgf', '--animation', '--e2e')
if (-not $SkipStress) { $modes += '--stress' }

foreach ($mode in $modes) {
    Invoke-Step "режим $mode" { dotnet run --project $app --no-build -- $mode }
}

if ($failures -gt 0) {
    Write-Host "Go Engine: проверок не пройдено — $failures." -ForegroundColor Red
    exit 1
}

Write-Host 'Go Engine: всё зелёное.' -ForegroundColor Green