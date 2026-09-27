<#
.SYNOPSIS
    Сборка Go Engine под Windows, macOS и Linux.

.DESCRIPTION
    Публикует самодостаточные сборки приложения под три системы в каталог artifacts.
    Проверяет, что тесты зелёные, и складывает рядом файл версии.

.PARAMETER Runtime
    Идентификаторы систем для сборки. По умолчанию все три.

.EXAMPLE
    ./build.ps1
    ./build.ps1 -Runtime win-x64
#>
[CmdletBinding()]
param(
    [string[]] $Runtime = @('win-x64', 'osx-x64', 'linux-x64')
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root 'src'
$artifacts = Join-Path $root 'artifacts'

Write-Host 'Go Engine: проверка тестов'
dotnet test (Join-Path $src 'GoEngine.sln') --nologo
if ($LASTEXITCODE -ne 0) { throw 'Тесты не прошли: сборка остановлена.' }

foreach ($rid in $Runtime) {
    $output = Join-Path $artifacts $rid

    Write-Host "Go Engine: сборка $rid"
    dotnet publish (Join-Path $src 'GoEngine.App.Desktop/GoEngine.App.Desktop.csproj') `
        -c Release -r $rid --self-contained true -o $output --nologo
    if ($LASTEXITCODE -ne 0) { throw "Сборка $rid не удалась." }

    # Проверка, что приложение запускается: проверочный режим не открывает окно.
    Write-Host "Go Engine: проверка $rid"
    $exe = if ($rid -like 'win-*') { Join-Path $output 'GoEngine.App.Desktop.exe' } else { Join-Path $output 'GoEngine.App.Desktop' }

    if ($rid -like 'win-*') {
        & $exe --smoke
        if ($LASTEXITCODE -ne 0) { throw "Проверка $rid не прошла." }
    }
    else {
        Write-Host "Go Engine: $rid собрана; запуск проверяется на целевой системе"
    }
}

Write-Host "Go Engine: сборки готовы в $artifacts"