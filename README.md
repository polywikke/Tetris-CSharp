# 俄罗斯方块（C# 大作业）

.NET 10 + WinForms 实现的俄罗斯方块。玩法与界面完全分离：`Tetris.Core` 不依赖任何界面技术，规则、引擎与 AI 都能被单元测试直接驱动。

## 功能

- 12 列 × 16 行棋盘，7-bag 随机出块，同一种子序列可复现
- 移动、双向旋转（带简化 SRS 踢墙）、软降、硬降、暂停、重开
- 消行计分、每 10 行升级、重力随等级加快、十六进制显示分数
- 最高分自动保存到 XML，重启自动读取；缺失/损坏/只读/被占用都能优雅处理
- **AI 自动玩家**：启发式评估 + 落点搜索（2-ply），F3 随时观战 / 接管
- **观战节奏可调**：分步档每 110ms 一条指令、落块后停 140ms，能看清每一步（分步档会冻结重力，保证落点与极速档一致）；极速档恢复快速对局
- **下落速度三档**（0.5× / 1× / 2×），两种模式下都生效
- AI 落点可视化：选中落点实心高亮 + 整体外框，其余只画前 6 名轮廓
- DAS 连发（长按左右/软降）、下一个方块预览

## 一键启动（推荐给第一次下载的人）

双击仓库根目录的 **`run.cmd`**，脚本会自动完成四件事：

1. 检查 Windows 与 .NET 10 SDK；
2. 缺少 SDK 时**自动下载并安装**（用户级，不需要管理员权限）；
3. 只编译游戏项目（不碰测试，所以**离线也能编译**）；
4. 启动游戏窗口。

也可以在 PowerShell 里手动执行：

```powershell
.\run.ps1              # 检查 + 编译 + 启动
.\run.ps1 -SkipLaunch  # 只检查 + 编译，不启动
```

> 执行策略被拦时用：`powershell -ExecutionPolicy Bypass -File .\run.ps1`

## 环境要求

- Windows 10 / 11
- .NET SDK 10.0（`dotnet --version` → `10.0.x`）——没有的话 `run.cmd` 会自动装

## 构建、运行、测试

```bash
dotnet build Tetris.sln
dotnet run --project src/Tetris.App
dotnet test
```

发布（目标机器需要 .NET 10 运行时可直接运行）：

```bash
dotnet publish src/Tetris.App -c Release -r win-x64 --self-contained false -o publish
```

> 首次构建若报 `NU1301` / SSL 错误，说明连不上 nuget.org，换镜像：
> `dotnet nuget add source https://mirrors.huaweicloud.com/repository/nuget/v3/index.json -n huawei`

## 操作

| 按键 | 功能 |
|---|---|
| ← / → | 左右移动（长按连发） |
| ↑ 或 X | 顺时针旋转 |
| Z | 逆时针旋转 |
| ↓ | 软降 |
| 空格 | 硬降 |
| P / Esc | 暂停 / 继续 |
| F2 | 重新开始 |
| F3 | AI 观战 / 玩家接管（同一棋盘轮流控制） |
| F4 | 下落速度 0.5× / 1× / 2×（两种模式都生效） |
| F5 | AI 观战节奏 分步 / 极速 |

## 规则与计分

- 一次消 k 行得 **100 × k** 分；硬降本身不计分
- 每消 10 行升 1 级，重力间隔从 800ms 递减到 50ms
- 分数以十六进制显示；最高分保存在 `%AppData%\TetrisCourse\highscore.xml`

## 自动玩家

`src/Tetris.AI` 实现 `IPlayerAgent`，不依赖界面：

- **评估函数**：`0.76 × 消行 − 0.51 × 总高度 − 0.36 × 空洞 − 0.18 × 凹凸度`
- **落点搜索**：4 个旋转态 × 全部合法列，`Clone` 推演落底消行后打分；2-ply 只在"并列最优"里裁决
- **指令状态机**：旋转 → 平移 → 硬降，带三路兜底，保证不卡死

真引擎上 10 个种子实测：平均 **68730 分 / 687 行**，10 局全部未被顶死；单次决策平均 0.38ms、P99 4.66ms，远低于 8ms 帧预算。

## 项目结构

```
src/Tetris.Core          规则、引擎、全部接口（无 UI 依赖）
src/Tetris.Persistence   高分 XML 存档
src/Tetris.AI            启发式自动玩家
src/Tetris.App           WinForms 界面
tests/Tetris.Core.Tests  88 个单元 / 集成测试
docs/                    设计说明
```

依赖只允许单向：`App → Persistence → Core`、`App → Core`、`AI → Core`、`tests → Core/AI`。

## 文档

- [设计说明](docs/设计说明.md)：架构、规则细节、AI 设计、存档契约、测试清单、**所有主动取舍及其理由**

## 已知限制

只支持单块棋盘（无双人对战、无回放、无网络）；AI 权重未做网格调参；界面仅 Windows。
