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


# Прогон тестов. Обычный путь — dotnet test; если хост тестов не стартует (в некоторых средах он
# падает с Win32Exception (5) в Process.EnsureWatchingForExit — не может открыть родительский
# процесс), те же тесты прогоняет утилита src/GoEngine.Tools.TestRunner напрямую, без vstest. Второй путь
# не заменяет первый, а страхует его: на сборочном агенте работает dotnet test, и он же остаётся
# источником истины. Утилита не входит в решение и частью продукта не является.
function Invoke-Tests {
    $solution = Join-Path $src 'GoEngine.sln'
    $log = Join-Path $root 'artifacts/tests-output.txt'
    New-Item -ItemType Directory -Path (Split-Path $log) -Force | Out-Null

    # Сначала короткая проба на одном тесте: если хост тестов не стартует, мы узнаём это
    # за секунды, а не после двух прогонов всего решения (в этой среде dotnet test падает
    # на старте, но успевает пройти сборку).
    $probe = Join-Path $root 'artifacts/tests-probe.txt'
    $null = dotnet test $solution --nologo --filter 'FullyQualifiedName~Рантайм' 2>&1 | Tee-Object -FilePath $probe

    $probeText = Get-Content $probe -Raw -ErrorAction SilentlyContinue
    $flakyHost = $LASTEXITCODE -ne 0 -and $probeText -match 'EnsureWatchingForExit|Тестовый запуск прерван'

    if (-not $flakyHost) {
        dotnet test $solution --nologo 2>&1 | Tee-Object -FilePath $log

        return
    }

    Write-Host '  хост тестов не стартует в этой среде: прогоняю тесты напрямую (src/GoEngine.Tools.TestRunner)' -ForegroundColor Yellow

    $runner = Join-Path $root 'src/GoEngine.Tools.TestRunner/TestRunner.csproj'
    dotnet build $runner --nologo | Out-Null

    if ($LASTEXITCODE -ne 0) {
        return
    }

    # Временный каталог — в рабочей копии: запись во временный каталог пользователя бывает закрыта.
    $env:TEMP = Join-Path $root 'artifacts/test-temp'
    $env:TMP = $env:TEMP
    New-Item -ItemType Directory -Path $env:TEMP -Force | Out-Null

    foreach ($assembly in @(
        (Join-Path $src 'GoEngine.Tests/bin/Debug/net8.0/GoEngine.Tests.dll'),
        (Join-Path $src 'GoEngine.App.Tests/bin/Debug/net8.0/GoEngine.App.Tests.dll'))) {
        dotnet run --project $runner --no-build -- $assembly 2>&1 | Tee-Object -FilePath $log -Append
        if ($LASTEXITCODE -ne 0) {
            $global:LASTEXITCODE = 1
            return
        }
    }

    $global:LASTEXITCODE = 0
}

Invoke-Step 'сборка решения' { dotnet build (Join-Path $src 'GoEngine.sln') --nologo }
Invoke-Step 'тесты' { Invoke-Tests }

# Режим задач обязателен в гейте: жалоба «задачи не работают» не должна ловиться игроком.
$modes = @('--smoke', '--check', '--state', '--settings', '--sgf', '--animation', '--problems', '--result', '--e2e')
if (-not $SkipStress) { $modes += '--stress' }

foreach ($mode in $modes) {
    Invoke-Step "режим $mode" { dotnet run --project $app --no-build -- $mode }
}

if ($failures -gt 0) {
    Write-Host "Go Engine: проверок не пройдено — $failures." -ForegroundColor Red
    exit 1
}

Write-Host 'Go Engine: всё зелёное.' -ForegroundColor Green