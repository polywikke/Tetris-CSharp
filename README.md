# 俄罗斯方块（C# 大作业）

.NET 10 + WinForms 的俄罗斯方块课程作业。核心逻辑与界面完全分离，核心库可被测试、AI、回放复用。

> 分工与接口约定见 **[docs/分工与接口契约.md](docs/分工与接口契约.md)**。

## 环境要求

- .NET SDK 10.0（`dotnet --version` 应为 `10.0.x`）
- Windows 10/11（界面使用 WinForms）
- 首次还原测试项目需要联网（xUnit 包）

## 构建与运行

```bash
dotnet build Tetris.sln
dotnet run --project src/Tetris.App
dotnet test
```

发布：

```bash
dotnet publish src/Tetris.App -c Release -r win-x64 --self-contained false -o publish
```

## 操作

| 按键 | 功能 |
|---|---|
| ← / → | 左右移动 |
| ↑ 或 X | 顺时针旋转 |
| Z | 逆时针旋转 |
| ↓ | 软降 |
| 空格 | 硬降 |
| P / Esc | 暂停 / 继续 |
| F2 | 重新开始 |

分数以十六进制显示；历史最高分保存在 `%AppData%\TetrisCourse\highscore.xml`。

## 常见问题

首次 `dotnet build` 报 `NU1301` / `The SSL connection could not be established`，说明本机连不上 nuget.org，
换一个镜像源即可（只需一次）：

```bash
dotnet nuget add source https://mirrors.huaweicloud.com/repository/nuget/v3/index.json -n huawei
dotnet restore --interactive
```

## 目录结构

```
src/Tetris.Core          核心契约：状态、规则、引擎、存档与 AI 接口（无界面依赖）
src/Tetris.Persistence   高分 XML 持久化
src/Tetris.AI            自动玩家
src/Tetris.App           WinForms 界面
tests/Tetris.Core.Tests  xUnit 测试
docs/                    分工与接口契约、报告素材
```

## 当前状态

骨架阶段：界面对接口编程，当前接的是 `DemoEngine`（假引擎）。
A 交付 `GameEngine` 后，在 `src/Tetris.App/AppBootstrap.cs` 改一行即可切换到真实引擎。
