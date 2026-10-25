# 03 · C 的任务书：AI、测试与发布

**你的板块：** `src/Tetris.AI`（自动玩家）+ `tests/`（单元测试）+ 发布/文档/视频。
**扩展功能只做这一项（AI），不做第二项**——评分对可选功能是 15 分封顶，做一项"完美实现"就能拿到 12–15，多做的收益接近零。

---

## 一、你要交什么

| 顺序 | 交付物 | 文件 | 验收 |
|---|---|---|---|
| ① | 测试骨架 + 假规则 | `tests/Tetris.Core.Tests/**` | 能在 A 交付前先跑起来 |
| ② | 评估函数 | `src/Tetris.AI/BoardEvaluator.cs`（新建） | 四项特征算对，有单元测试 |
| ③ | 落点搜索（1-ply） | `src/Tetris.AI/PlacementFinder.cs`（新建） | 48 个候选里选出最优 |
| ④ | 指令状态机 + 兜底 | `src/Tetris.AI/HeuristicAgent.cs` | 能自动玩完整局，不卡死 |
| ⑤ | 2-ply 搜索（top-K） | 同上 | 比 1-ply 平均分高，有数据 |
| ⑥ | 决策数据（可视化接口） | `HeuristicAgent` 实现 `IPlayerAgentDebug` | B 能画热力图 |
| ⑦ | 测试 ≥20 个 | `tests/**` | `dotnet test` 全绿 |
| ⑧ | 覆盖率 + AI 平均分 | 报告 4.3 | 有具体数字 |
| ⑨ | README + 演示视频 | `README.md` | 文档与程序一致，视频 2–3 分钟 |

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

### 3.2 你实现的（可选）：`IPlayerAgentDebug`

给界面画"AI 在想什么"。不实现也能拿分，实现了演示效果明显更好。

```csharp
public interface IPlayerAgentDebug
{
    IReadOnlyList<AgentCandidate> Candidates { get; }  // 最近一次决策的全部候选
    AgentCandidate? Chosen { get; }
}

public sealed record AgentCandidate(
    PieceKind Kind, int Rotation, int X, int Y,
    double Score, int ClearedLines, int Holes, int AggregateHeight, int Bumpiness);
```

- `X/Y` 是落点方块 4×4 包围盒的左上角，`Y` 是已经落到底的位置；
- 每次重新规划时整体替换 `Candidates`，`Chosen` 同步更新；
- 没有新决策时保持上一次的值也没关系，但不能返回 null 集合。

### 3.3 你调用的：`IGameRules` 与 `GameState`

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

## 四、AI 实现（这是你最重要的活）

### 4.1 结构

```
src/Tetris.AI/
├─ HeuristicAgent.cs     实现 IPlayerAgent + IPlayerAgentDebug，维护当前方块的落点计划
├─ BoardEvaluator.cs     给一个棋盘打分（四项特征 + 权重）
└─ PlacementFinder.cs    枚举旋转和落点，返回候选与最优
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

### 4.3 落点搜索（1-ply）

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
        记录候选 (rotation, targetX, p.Y, s, ...)
```

> 旋转会改变方块在包围盒里的形状，**先转再算落点列**；`Collides` 判不下的列直接跳过。

### 4.4 2-ply 搜索（比 1-ply 强，成本可控）

1-ply 只看当前块放哪；2-ply 再看"下一块放到这个新棋盘上最好能得多少分"。

不要 48 × 48 全展开，**只对前 K 个候选做第二层**（K = 6）：

```
first = 1-ply 全部候选，按 Score 降序
best = null
foreach c in first.Take(K):
    second = 在 c 的结果棋盘上，对 NextQueue.Peek(1) 做 1-ply 枚举
    total = c.Score + 0.4 * best(second)          // 下一块权重 0.3~0.5 之间调
    if (total > best) ...
```

复杂度：48 + 6 × 48 ≈ 336 次模拟/块，仍然在 8ms 预算内。
**报告里写清"K=6 的截断是性能与棋力的折中"，并用数据说明 2-ply 比 1-ply 平均分高多少。**

### 4.5 指令状态机（每帧只发一个命令）

```
if (_plan == null || 方块换了) _plan = FindBest(state);
if (state.Active.Rotation != _plan.Rotation) return RotateCw;
if (state.Active.X != _plan.X) return (state.Active.X < _plan.X) ? MoveRight : MoveLeft;
return HardDrop;
```

兜底（很重要，别让 AI 卡死）：

- 旋转 4 帧还没到位 → 放弃旋转，直接降；
- 同一方块超过 120 帧还没落下 → `HardDrop`；
- `FindBest` 找不到任何落点（棋盘满了）→ `HardDrop`；
- 连续两帧状态没变化 → 重新规划一次，避免死锁。

### 4.6 观战 / 接管（需求里写的"人机对战或观战演示"）

单棋盘项目里没法做字面的"人机对战"，按下面解释，并写进报告第二章"需求理解与假设"：

- **观战演示**：AI 全程自动，玩家只看；
- **人机对战**：AI 与玩家轮流控制同一块棋盘，F3 随时切换；AI 控制期间玩家按键不生效；
- 切换时**不重置当前方块**，玩家接手即可继续操作。

这是主动的需求解释，比硬凑双棋盘安全，也是评分标准里"对歧义需求说明处理方式"的得分点。

### 4.7 决策数据（给 B 画热力图）

每块规划一次时，把全部候选填进 `Candidates`，把最优填进 `Chosen`。
界面按 `Score` 归一化成绿→红，`Chosen` 描边。**画图是 B 的交付物（见 02 §8.3），你只负责数据正确、不返回 null 集合。**

### 4.8 性能

- 1-ply：48 次模拟；2-ply：约 336 次；每次模拟是 `Clone + LockActive`，O(12×16)；
- 用 `Stopwatch` 测一次决策耗时，写进报告（要求 ≤ 8ms，目标 < 2ms）；
- 不要在 `NextCommand` 里做 IO 或日志。

---

## 五、测试（≥20 个，评分点）

### 5.1 现状

`tests/Tetris.Core.Tests/ContractTests.cs` 已有 9 个（契约级）。你要再加 12 个以上。

### 5.2 建议清单

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
| 21 | 决策耗时 | 单次 `NextCommand` < 8ms |
| 22 | 候选数据 | `Candidates` 非空且 `Chosen` 在候选里 |

### 5.3 写法要求

- **一个用例只测一件事**，名字写清意图：`TryRotate_AtLeftWall_ShouldKickRight`；
- 用 `GameState` 直接摆盘（`state.Board[x, y] = PieceKind.O`），不要依赖引擎；
- A 还没交付的用例先加 `[Fact(Skip = "等 A 交付规则")]`，交付当天去掉 `Skip`；
- 提交前必须 `dotnet test` 全绿。

---

## 六、文档与演示

### 6.1 发布产物（由 A 提供）

publish 是 A 的交付物（见 01 §七）。你录视频前找 A 要一份最新的 `publish/` 目录，**不要自己发布**——产物和代码对不上，视频就白录了。

### 6.2 README 定稿

必须写：环境要求、构建命令、运行命令、**操作说明**、存档路径、目录结构、三人分工。

### 6.3 演示视频（2–3 分钟）

| 时间 | 内容 |
|---|---|
| 0:00–0:20 | 开场：项目名、三人分工 |
| 0:20–1:10 | 玩一局：移动/旋转/消行/升级/暂停 |
| 1:10–1:40 | **异常演示（必拍）**：删掉存档文件（或写坏）→ 启动 → 优雅降级 → 再破纪录保存 |
| 1:40–2:20 | F3 切 AI：热力图 + AI 自己玩 |
| 2:20–2:40 | `dotnet test` 全绿 + 收尾 |

---

## 七、自测清单

| # | 检查 | 期望 |
|---|---|---|
| 1 | `dotnet test` | 全绿，用例数 ≥ 20 |
| 2 | AI 连玩 3 局 | 不崩、不卡、每局都能消行 |
| 3 | 单次 AI 决策耗时 | ≤ 8ms（有数字） |
| 4 | 2-ply vs 1-ply | 平均分对比有数字 |
| 5 | AI 10 局平均分 | 有数字（报告 4.3） |
| 6 | 覆盖率 | `dotnet test --collect:"XPlat Code Coverage"` 有报告 |
| 7 | 用 A 给的 publish 产物 | 双击能运行 |
| 8 | 文档 | README 与实际一致，没有过时内容 |


