# 03 · C 的任务书：AI、测试与发布

**你的板块：** `src/Tetris.AI`（自动玩家）+ `tests/`（单元测试）+ 发布/文档/视频。
**你负责的是"可选功能 15 分"和"架构与测试 10 分"里的测试部分，也是唯一能让 A 的代码被证明正确的人。**

---

## 一、你要交什么

| # | 交付物 | 文件 | 截止 | 验收 |
|---|---|---|---|---|
| 1 | 启发式 AI（评估 + 落点搜索 + 指令序列） | `src/Tetris.AI/**` | D6 | 自动玩 3 分钟不崩，每局稳定消行 |
| 2 | 单元测试 ≥15 个 | `tests/Tetris.Core.Tests/**` | D5 | `dotnet test` 全绿 |
| 3 | 覆盖率 / AI 平均分数据 | 报告 4.3 | D10 | 有具体数字 |
| 4 | publish 产物 + README + 演示视频 | `publish/`、`README.md` | D12 | 双击 exe 能跑 |
| 5 | AI 接进界面（与 B 配合） | 见总览 §5 节点 4 | D8 | F3 切换人机 |

---

## 二、只改这些文件

```
src/Tetris.AI/**                 你的
tests/Tetris.Core.Tests/**       你的
docs/**、README.md               你的（保持和代码一致）
```

**不要动：** `src/Tetris.Core`（A 的）、`src/Tetris.App`、`src/Tetris.Persistence`（B 的）。
AI 接界面时，B 改 `AppBootstrap` 和 `MainForm`，你只在旁边看，别自己上手改。

---

## 三、接口契约

### 3.1 你实现的：`IPlayerAgent`

```csharp
public interface IPlayerAgent
{
    string Name { get; }                          // 显示在界面上
    void Reset();                                 // 开新局
    GameCommand NextCommand(GameState state);     // 每逻辑帧调用一次
}

public enum GameCommand
{
    None, MoveLeft, MoveRight, SoftDrop, HardDrop,
    RotateCw, RotateCcw, Pause, Resume
}
```

规则：

- 每帧只返回**一个**动作，返回 `None` 表示这帧不动；
- **不许修改 `state`**（要推演就 `state.Clone()`）；
- 不许抛异常；想不出办法就返回 `HardDrop` 或 `None`；
- 单帧耗时 ≤ 8ms（60 帧只有 16.7ms）。

### 3.2 你调用的：`IGameRules` 与 `GameState`

```csharp
public interface IGameRules
{
    bool Collides(GameState state, Tetromino piece);
    int  DropDistance(GameState state, Tetromino piece);
    bool TryMove(GameState state, int deltaX);
    bool TryRotate(GameState state, int direction);
    int  LockActive(GameState state);       // 锁定 + 消行 + 计分，返回消除行数
}

public sealed class GameState
{
    PieceKind?[,] Board;      // Board[x, y]，x 列、y 行、y=0 顶部
    Tetromino Active;         // 当前方块（Cells 是绝对坐标）
    int Width, Height;        // 12, 16
    int Score, Level, LinesCleared;
    GameState Clone();        // 深拷贝，推演必须用它
}

public sealed class Tetromino
{
    PieceKind Kind; int X, Y, Rotation;
    IReadOnlyList<CellPos> Cells;
    Tetromino With(int? x = null, int? y = null, int? rotation = null);
}
```

**A 的规则没交付之前怎么办：** 在测试里写一个 `FakeGameRules`（能判边界碰撞即可），先用它把 AI 算法跑通；A 交付后换成 `StandardGameRules`，只改一行。

---

## 四、任务 1：AI（可选功能 15 分）

### 4.1 整体结构（建议三个文件）

```
src/Tetris.AI/
├─ HeuristicAgent.cs     实现 IPlayerAgent，维护"当前方块的落点计划"
├─ BoardEvaluator.cs     给一个棋盘打分
└─ PlacementFinder.cs    枚举 4 旋转 × 12 列，返回最优落点
```

### 4.2 评估函数

对棋盘按列统计：

| 指标 | 定义 | 越……越好 |
|---|---|---|
| `aggregateHeight` | 各列最高格 y 之和（越高越糟） | 小 |
| `holes` | 某列空格上方已有方块的格子数 | 小 |
| `bumpiness` | 相邻列高度差绝对值之和 | 小 |
| `clearedLines` | 本次落子消除的行数 | 大 |

评分（经典权重，可直接用，也可自己调）：

```
score = 0.76 * clearedLines
      - 0.51 * aggregateHeight
      - 0.36 * holes
      - 0.18 * bumpiness
```

### 4.3 落点搜索

```
foreach rotation in 0..3:
    piece = state.Active.With(rotation: rotation)
    if rules.Collides(state, piece) continue
    foreach targetX in 0..Width-1:
        sim = state.Clone()                       // 关键：绝不改原状态
        p = piece.With(x: targetX)
        if rules.Collides(sim, p) continue
        p = p.With(y: p.Y + rules.DropDistance(sim, p))
        sim.Active = p
        cleared = rules.LockActive(sim)
        s = Eval(sim.Board, cleared)
        记录最优
```

> 旋转会改变方块在包围盒里的形状，**先转再算落点列**；`Collides` 判不下的列直接跳过。

### 4.4 指令状态机（每帧只发一个命令）

```
if (_plan == null || 方块换了) _plan = FindBest(state);
if (state.Active.Rotation != _plan.Rotation) return RotateCw;
if (state.Active.X != _plan.X) return (state.Active.X < _plan.X) ? MoveRight : MoveLeft;
return HardDrop;
```

兜底（很重要，答辩会问）：

- 旋转 4 帧还没到位 → 放弃旋转，直接降；
- 同一方块超过 120 帧还没落下 → `HardDrop`；
- `FindBest` 找不到任何落点（棋盘满了）→ `HardDrop`。

### 4.5 性能

- 每块最多 4 × 12 = 48 次推演，每次 `Clone + LockActive` 是 O(12×16)，完全够快；
- 用 `Stopwatch` 测一次决策耗时，写进报告（要求 ≤ 8ms）。

---

## 五、任务 2：单元测试（≥15 个，评分点）

### 5.1 现状

`tests/Tetris.Core.Tests/ContractTests.cs` 已有 9 个（契约级）。你要再加 12 个以上，凑到 20+。

### 5.2 建议的测试清单

| # | 用例 | 断言 |
|---|---|---|
| 1 | 碰左墙 | `Collides` 为 true |
| 2 | 碰右墙 | true |
| 3 | 碰底 | true |
| 4 | 与已固定块重叠 | true |
| 5 | 空盘下落距离 | 正好能落到底 |
| 6 | 堆叠上方下落距离 | 等于空位数 |
| 7 | 向左被挡 | `TryMove` false 且 X 不变 |
| 8 | 贴左墙旋转 | `TryRotate` true 且不越界（踢墙生效） |
| 9 | 四面围死旋转 | false 且状态完全不变 |
| 10 | 消 1 行 | 返回 1，+100 分，上方下移 |
| 11 | 一次消 4 行 | 返回 4，+400 分 |
| 12 | 没消行 | 返回 0，分数不变 |
| 13 | 满 10 行升级 | `Level == 2`，`GravityMsFor(2) < GravityMsFor(1)` |
| 14 | 生成即碰撞 | `Status == GameOver` |
| 15 | 暂停后 Tick | 状态与 `Frame` 都不变 |
| 16 | 同种子两局 | 方块序列一致 |
| 17 | 7-bag 公平性 | 连续 14 个中每种恰好 2 个 |
| 18 | AI 冒烟 | 返回合法命令，且原 `state` 未被修改 |
| 19 | AI 能消行 | 给定一行差一格，AI 在 200 帧内消除 |
| 20 | 消行后棋盘压缩正确 | 上方行整体下移、无残留 |

### 5.3 写法要求

- **一个用例只测一件事**，名字写清意图：`TryRotate_AtLeftWall_ShouldKickRight`；
- 用 `GameState` 直接摆盘（`state.Board[x, y] = PieceKind.O`），不要依赖引擎；
- A 还没交付的用例先加 `[Fact(Skip = "等 A 交付规则")]`，交付当天去掉 `Skip`；
- 提交前必须 `dotnet test` 全绿。

---

## 六、任务 3：发布与演示

### 6.1 publish

```bash
dotnet publish src/Tetris.App -c Release -r win-x64 --self-contained false -o publish
```

产物体积小（依赖本机 .NET 10 运行时），老师机器装了 .NET 10 就能直接跑。
如果要"双击即用"，改成 `--self-contained true`（体积约 150MB，二选一，README 里写清）。

### 6.2 README 定稿

必须写：环境要求、构建命令、运行命令、**操作说明**、存档路径、目录结构、三人分工。

### 6.3 演示视频（2–3 分钟）

| 时间 | 内容 |
|---|---|
| 0:00–0:20 | 开场：项目名、三人分工 |
| 0:20–1:20 | 玩一局：移动/旋转/消行/升级/暂停 |
| 1:20–1:50 | **异常演示（必拍）**：删掉存档文件（或写坏）→ 启动 → 优雅降级 → 再破纪录保存 |
| 1:50–2:20 | 按 F3 切 AI 自动玩 |
| 2:20–2:40 | 测试跑一遍（`dotnet test` 全绿）+ 收尾 |

---

## 七、自测清单

| # | 检查 | 期望 |
|---|---|---|
| 1 | `dotnet test` | 全绿，用例数 ≥ 20 |
| 2 | AI 连玩 3 局 | 不崩、不卡、每局都能消行 |
| 3 | 单次 AI 决策耗时 | ≤ 8ms（有数字） |
| 4 | AI 10 局平均分 | 有数字（报告 4.3） |
| 5 | 覆盖率 | `dotnet test --collect:"XPlat Code Coverage"` 有报告 |
| 6 | publish 产物拷到别的目录 | 能双击运行 |
| 7 | 文档 | README 与实际一致，没有过时截图 |

---

## 八、报告与答辩

**报告主写：** 3.4 扩展功能设计（评估函数、搜索、指令状态机）、4.3 量化数据（AI 平均分、决策耗时、测试数量与覆盖率）。
**附录：** 《AI 使用声明》（用没用、用在哪、哪些结果被你改了）。

**答辩准备：**

1. 评估函数的四项分别是什么意思？权重怎么定的？调过没有？
2. 为什么必须 `Clone` 之后推演？直接改会怎样？
3. 测试里哪个用例最难写？为什么？
4. AI 卡住不动时你怎么兜底的？

**现场小改动演练：** 把评估函数的一行权重改掉（比如 `holes` 系数从 -0.36 改成 -0.8），重新跑，说明"AI 变保守了"。

---

## 九、提交节奏

| 时间 | 提交内容 |
|---|---|
| D1 | 测试骨架 + `FakeGameRules` + 评估函数 |
| D2 | 落点搜索 + 指令状态机（用假规则跑通） |
| D3 | **A 交规则后**：换成真规则，规则类测试转绿 |
| D5 | 测试补到 20 个（**节点 2 的验收由你出**） |
| D6 | AI 跑通整局 |
| D8 | 配合 B 接界面、调权重 |
| D10 | 覆盖率、AI 数据、README |
| D12 | publish + 视频 |

提交信息：`feat(ai): 实现启发式落点搜索`、`test(core): 补充消行与踢墙用例`。
