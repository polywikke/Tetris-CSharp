# 俄罗斯方块（C# 大作业）

.NET 10 + WinForms。核心玩法与界面完全分离：`Tetris.Core` 不依赖任何界面技术，可被单元测试、AI 直接复用。

## 环境要求

- .NET SDK 10.0（`dotnet --version` → `10.0.x`）
- Windows 10 / 11

## 构建、运行、测试

```bash
dotnet build Tetris.sln
dotnet run --project src/Tetris.App
dotnet test
```

发布（需要 .NET 10 运行时的机器可直接运行）：

```bash
dotnet publish src/Tetris.App -c Release -r win-x64 --self-contained false -o publish
```

> 首次构建若报 `NU1301` / SSL 错误，说明连不上 nuget.org，换镜像：
> `dotnet nuget add source https://mirrors.huaweicloud.com/repository/nuget/v3/index.json -n huawei`

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
| F3 | 人机切换（AI 接入后可用） |

分数以十六进制显示；最高分保存在 `%AppData%\TetrisCourse\highscore.xml`。

## 文档

| 文档 | 给谁看 |
|---|---|
| [00 项目总览与集成](docs/00-项目总览与集成.md) | 三个人都看，重点看第 5 节"怎么拼起来" |
| [01 A：核心引擎](docs/01-A-核心引擎.md) | A |
| [02 B：界面与存档](docs/02-B-界面与存档.md) | B |
| [03 C：AI 与测试](docs/03-C-AI与测试.md) | C |

## 目录

```
src/Tetris.Core          规则、引擎、全部接口（A）
src/Tetris.Persistence   高分 XML 存档（B）
src/Tetris.AI            启发式自动玩家（C）
src/Tetris.App           WinForms 界面（B）
tests/Tetris.Core.Tests  xUnit 测试（C）
docs/                    任务书与集成说明
```

## 当前状态

骨架阶段：界面已能运行，接的是 `DemoEngine`（假引擎）。
A 交付 `GameEngine` 后，改 `src/Tetris.App/AppBootstrap.cs` 一处即可切换到真实引擎。
