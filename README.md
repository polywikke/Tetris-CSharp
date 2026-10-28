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
| F3 | AI 观战 / 玩家接管（AI 侧已就绪，界面接线由 B 完成） |

分数以十六进制显示；最高分保存在 `%AppData%\TetrisCourse\highscore.xml`。

## AI 自动玩家（C 部分）

`src/Tetris.AI` 是不依赖界面的启发式 AI，实现 `IPlayerAgent`：

```csharp
var agent = new HeuristicAgent(state.Config, rules);   // rules 由 A 的引擎提供
var command = agent.NextCommand(state);                // 每逻辑帧一条指令，不修改 state
```

- **评估函数**（`BoardEvaluator`）：消行 ×0.76 − 聚合高度 ×0.51 − 洞 ×0.36 − 凹凸度 ×0.18。
- **落点搜索**（`PlacementFinder`）：4 个旋转态 × 每一列，`Clone` 推演落底并消行后打分；2-ply 只在"并列最优"的候选里做裁决。
- **指令状态机**（`HeuristicAgent`）：旋转 → 平移 → 硬降；带旋转放弃、单块 120 帧超时、计划不可达三路兜底，保证不会卡死。
- **决策可视化**（`IPlayerAgentDebug`）：`Candidates` / `Chosen` 供界面画候选热力图（F3 接线由 B 完成）。

12×16 棋盘、7-bag 随机出块、10 局实测：平均 1970 分 / 19.7 行（纯 1-ply 为 1420 分 / 14.2 行），单次决策平均 0.17ms，远低于 8ms 帧预算。数据与调参记录见 [docs/03](docs/03-C-AI与测试.md) 第八节。

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
