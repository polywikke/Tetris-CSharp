<#
    俄罗斯方块 · 一键启动

    做三件事：
      1. 检查 Windows 和 .NET 10 SDK；
      2. 没有 SDK 就自动装一个（用户级安装，不需要管理员权限）；
      3. 编译并启动游戏。

    用法：
      双击 run.cmd
      或在 PowerShell 里执行  .\run.ps1
      只看编译不启动：        .\run.ps1 -SkipLaunch
#>
[CmdletBinding()]
param(
    [switch]$SkipLaunch
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Write-Step([string]$Text) {
    Write-Host "==> $Text" -ForegroundColor Cyan
}

function Write-Ok([string]$Text) {
    Write-Host "    $Text" -ForegroundColor Green
}

function Write-Note([string]$Text) {
    Write-Host "    $Text" -ForegroundColor Yellow
}

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

Write-Host ""
Write-Host "  俄罗斯方块 · 一键启动" -ForegroundColor White
Write-Host "  ----------------------------------------"

# ---------------------------------------------------------------- 1. 环境
Write-Step "检查运行环境"

if ($env:OS -ne 'Windows_NT') {
    throw "本项目使用 WinForms，只能在 Windows 上运行。"
}

$project = Join-Path (Join-Path $root 'src\Tetris.App') 'Tetris.App.csproj'
if (-not (Test-Path $project)) {
    throw "找不到项目文件：$project`n请确认 run.ps1 放在仓库根目录（和 Tetris.sln 同级）。"
}

Write-Ok "Windows 环境正常"

# ---------------------------------------------------------------- 2. .NET SDK
Write-Step "检查 .NET SDK 10"

$dotnet = $null
$candidates = @()

$command = Get-Command dotnet -ErrorAction SilentlyContinue
if ($command) { $candidates += $command.Source }
$candidates += (Join-Path (Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet') 'dotnet.exe')

foreach ($candidate in $candidates) {
    if (-not (Test-Path $candidate)) { continue }

    $sdks = & $candidate --list-sdks 2>$null
    if ($LASTEXITCODE -ne 0) { continue }

    if ($sdks | Where-Object { $_ -match '^10\.' }) {
        $dotnet = $candidate
        break
    }
}

if ($dotnet) {
    $version = (& $dotnet --version).Trim()
    Write-Ok "已找到 .NET SDK $version"
} else {
    Write-Note "未找到 .NET 10 SDK，开始自动安装（用户级，不需要管理员权限）"

    $installDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
    $installer = Join-Path $env:TEMP 'dotnet-install.ps1'

    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer -UseBasicParsing
    } catch {
        throw "下载 .NET 安装脚本失败（网络不通？）。`n可以手动安装 .NET 10 SDK：https://dotnet.microsoft.com/download"
    }

    & $installer -Channel 10.0 -InstallDir $installDir -NoPath
    $dotnet = Join-Path $installDir 'dotnet.exe'
    if (-not (Test-Path $dotnet)) {
        throw "自动安装失败：没有找到 $dotnet`n请手动安装 .NET 10 SDK：https://dotnet.microsoft.com/download"
    }

    Write-Ok "安装完成：$(& $dotnet --version)"
    Write-Note "以后想在命令行直接用 dotnet，可以把 $installDir 加进 PATH"
}

# ---------------------------------------------------------------- 3. 编译
# 只编译 App 项目：它不引用任何 NuGet 包，所以离线也能编译；
# 整个解决方案里只有测试项目需要联网还原 xunit，这里刻意不碰。
Write-Step "编译游戏"

& $dotnet build $project -c Debug -nologo -v minimal
if ($LASTEXITCODE -ne 0) {
    if (Get-Process -Name 'Tetris.App' -ErrorAction SilentlyContinue) {
        throw "编译失败：游戏当前正在运行，请先关掉它的窗口再重试。"
    }

    throw "编译失败，请把上面的错误信息发给开发者。"
}

$exe = Get-ChildItem (Join-Path (Join-Path $root 'src\Tetris.App') 'bin\Debug') `
    -Recurse -Filter 'Tetris.App.exe' -ErrorAction SilentlyContinue |
    Select-Object -First 1

if (-not $exe) {
    throw "编译完成了，但没有找到 Tetris.App.exe。"
}

Write-Ok "编译成功"

# ---------------------------------------------------------------- 4. 启动
if ($SkipLaunch) {
    Write-Ok "已跳过启动：$($exe.FullName)"
    return
}

Write-Step "启动游戏"
Start-Process -FilePath $exe.FullName -WorkingDirectory $exe.DirectoryName
Write-Ok "已启动，这个窗口可以关闭了"

Write-Host ""
Write-Host "  操作：← → 左右移动   ↑ / X 顺时针   Z 逆时针" -ForegroundColor DarkGray
Write-Host "        空格 硬降   P / Esc 暂停   F2 重新开始" -ForegroundColor DarkGray
Write-Host "        F3 AI 观战   F5 AI 快慢   F4 下落速度" -ForegroundColor DarkGray
Write-Host ""
