# -*- coding: utf-8 -*-
"""生成研究报告用的示意图（架构图 / 每帧流程 / 存档状态机 / 界面布局）。"""

import math
import os
from PIL import Image, ImageDraw, ImageFont

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "figures")
os.makedirs(OUT, exist_ok=True)

FONT_REG = r"C:\Windows\Fonts\msyh.ttc"
FONT_BOLD = r"C:\Windows\Fonts\msyhbd.ttc"
S = 2  # 超采样倍数


def font(size, bold=False):
    return ImageFont.truetype(FONT_BOLD if bold else FONT_REG, int(size * S))


INK = (32, 36, 48)
MUTED = (96, 104, 124)
BLUE = (40, 104, 200)
BLUE_BG = (232, 240, 253)
GREEN = (26, 132, 84)
GREEN_BG = (230, 246, 238)
ORANGE = (196, 108, 18)
ORANGE_BG = (253, 240, 226)
PURPLE = (112, 70, 190)
PURPLE_BG = (242, 236, 253)
GREY_BG = (245, 246, 249)
WHITE = (255, 255, 255)


def canvas(w, h):
    img = Image.new("RGB", (w * S, h * S), WHITE)
    return img, ImageDraw.Draw(img)


class Painter:
    def __init__(self, d):
        self.d = d

    def width(self, text, size, bold=False):
        return self.d.textlength(text, font=font(size, bold)) / S

    def fit(self, lines, inner, preferred, minimum=9.5, bold=False):
        """把字号压到所有行都能放进 inner 宽度。"""
        if not lines:
            return preferred
        size = preferred
        while size > minimum and max(self.width(l, size, bold) for l in lines) > inner:
            size -= 0.5
        return size

    def box(self, xy, title, lines, color, bg, title_size=17, body_size=13, radius=10, gap=9):
        x0, y0, x1, y1 = xy
        inner = (x1 - x0) - 28
        t_size = self.fit([title], inner, title_size, 11, True)
        b_size = self.fit(lines, inner, body_size, 9)
        d = self.d
        d.rounded_rectangle([x0 * S, y0 * S, x1 * S, y1 * S], radius=radius * S,
                            fill=bg, outline=color, width=2 * S)
        tf, bf = font(t_size, True), font(b_size)
        total = t_size + 10 + len(lines) * (b_size + gap)
        y = y0 + ((y1 - y0) - total) / 2 + 2
        d.text(((x0 + x1) / 2 * S, y * S), title, font=tf, fill=color, anchor="ma")
        y += t_size + 14
        for line in lines:
            d.text(((x0 + x1) / 2 * S, y * S), line, font=bf, fill=INK, anchor="ma")
            y += b_size + gap


def arrow(d, p0, p1, color=MUTED, width=2, head=9, dash=False):
    x0, y0 = p0[0] * S, p0[1] * S
    x1, y1 = p1[0] * S, p1[1] * S
    if dash:
        total = math.hypot(x1 - x0, y1 - y0)
        seg = 8 * S
        n = max(1, int(total // (seg * 2)))
        for i in range(n):
            a = i * 2 * seg / total
            b = min(1.0, (i * 2 + 1) * seg / total)
            d.line([x0 + (x1 - x0) * a, y0 + (y1 - y0) * a,
                    x0 + (x1 - x0) * b, y0 + (y1 - y0) * b], fill=color, width=width * S)
    else:
        d.line([x0, y0, x1, y1], fill=color, width=width * S)

    ang = math.atan2(y1 - y0, x1 - x0)
    h, w = head * S, head * S * 0.6
    d.polygon([
        (x1, y1),
        (x1 - h * math.cos(ang) + w * math.sin(ang), y1 - h * math.sin(ang) - w * math.cos(ang)),
        (x1 - h * math.cos(ang) - w * math.sin(ang), y1 - h * math.sin(ang) + w * math.cos(ang)),
    ], fill=color)


def label(d, xy, text, color=INK, size=12.5, bold=False, anchor="lm"):
    d.text((xy[0] * S, xy[1] * S), text, font=font(size, bold), fill=color, anchor=anchor)


def diamond(d, cx, cy, w, h, text, color=ORANGE, bg=ORANGE_BG, size=13.5):
    cx, cy, w, h = cx * S, cy * S, w * S, h * S
    d.polygon([(cx, cy - h / 2), (cx + w / 2, cy), (cx, cy + h / 2), (cx - w / 2, cy)],
              fill=bg, outline=color, width=2 * S)
    d.text((cx, cy), text, font=font(size, True), fill=color, anchor="mm")


def step(d, p, cx, text, color=BLUE, bg=BLUE_BG, w=430, h=52, size=13.5):
    x0, x1 = cx - w / 2, cx + w / 2
    d.rounded_rectangle([x0 * S, p * S, x1 * S, (p + h) * S], radius=8 * S,
                        fill=bg, outline=color, width=2 * S)
    d.text((cx * S, (p + h / 2) * S), text, font=font(size, True), fill=color, anchor="mm")
    return p + h


# ---------------------------------------------------------------- 图 1 架构
def fig_architecture():
    W, H = 1000, 640
    img, d = canvas(W, H)
    p = Painter(d)

    p.box((30, 26, 470, 156), "界面层  Tetris.App",
          ["MainForm（定时器 · 输入路由 · 存档接线）",
           "BoardView / NextPieceView（GDI+ 自绘）",
           "KeyRepeater（DAS 180ms / ARR 40ms）"],
          BLUE, BLUE_BG, 18, 13)

    p.box((530, 26, 970, 156), "扩展层  Tetris.AI（可选功能）",
          ["HeuristicAgent（指令状态机）",
           "PlacementFinder（1-ply / 2-ply 搜索）",
           "BoardEvaluator（四项特征线性打分）"],
          PURPLE, PURPLE_BG, 18, 13)

    p.box((30, 250, 470, 400), "领域层  Tetris.Core（无任何界面依赖）",
          ["GameState（可独立创建 / 深拷贝）",
           "IGameEngine → GameEngine",
           "IGameRules → StandardGameRules",
           "SevenBag + IRandomSource（可复现序列）"],
          GREEN, GREEN_BG, 18, 13)

    p.box((530, 250, 970, 350), "存储层  Tetris.Persistence",
          ["IHighScoreStore → XmlHighScoreStore",
           "HighScoreDto（XmlSerializer 映射）"],
          ORANGE, ORANGE_BG, 18, 13)

    p.box((30, 460, 470, 580), "测试  tests/Tetris.Core.Tests",
          ["88 个 xUnit 用例（核心 · 引擎 · AI · 存档）",
           "FakeGameRules / SimulatedGame 测试替身"],
          MUTED, GREY_BG, 18, 13)

    p.box((530, 460, 970, 580), "文档  README + docs/设计说明.md",
          ["环境要求 · 构建运行 · 操作说明",
           "架构、规则口径与全部主动取舍"],
          MUTED, GREY_BG, 18, 13)

    arrow(d, (250, 158), (250, 248))
    label(d, (262, 190), "只读 GameState；调 Apply / Tick", BLUE, 12)

    arrow(d, (472, 90), (528, 90))
    label(d, (500, 72), "F3 接管", PURPLE, 11.5, False, "mm")

    arrow(d, (750, 158), (750, 248))
    label(d, (762, 190), "同一套规则在副本上推演", PURPLE, 12)

    arrow(d, (528, 300), (474, 300), ORANGE)
    label(d, (500, 320), "仅用模型", ORANGE, 9.5, False, "mm")

    arrow(d, (250, 458), (250, 404), MUTED, 2, 9, True)
    label(d, (262, 430), "tests → Core / AI", MUTED, 11.5)

    img.resize((W * 2, H * 2), Image.LANCZOS).save(os.path.join(OUT, "fig1-architecture.png"))
    print("fig1 ok")


# ---------------------------------------------------------------- 图 2 每帧流程
def fig_frame():
    W, H = 900, 720
    img, d = canvas(W, H)
    cx = 380
    bw = 470

    y = 18
    y = step(d, y, cx, "Timer.Tick 触发（Interval = 15ms，约 66 次/秒）", w=bw) + 18
    y = step(d, y, cx, "累加逻辑时间；按下落速度倍率换算 delta", w=bw) + 18
    y = step(d, y, cx, "喂输入：KeyRepeater 连发命令 或 AI 一条指令", w=bw) + 18
    y = step(d, y, cx, "IGameEngine.Apply(command)：移动 / 旋转 / 硬降 / 暂停", w=bw) + 18

    # 判断 1
    dy1 = y + 34
    diamond(d, cx, dy1, 330, 68, "AI 分步观战？")
    arrow(d, (cx - 165, dy1), (70, dy1), ORANGE)
    d.line([70 * S, dy1 * S, 70 * S, (dy1 + 300) * S], fill=ORANGE, width=2 * S)
    label(d, (84, dy1 - 18), "是：冻结重力，跳过 Tick", ORANGE, 11.5)

    # 否 → Tick
    arrow(d, (cx, dy1 + 34), (cx, dy1 + 62))
    label(d, (cx + 14, dy1 + 46), "否", MUTED, 11.5)
    y = step(d, dy1 + 62, cx, "每满 1/60 秒 Tick 一次（单次刷新最多补 4 帧）", w=bw) + 18
    y = step(d, y, cx, "结束时判定纪录：写 XML（.tmp + 原子替换）", ORANGE, ORANGE_BG, bw) + 18

    # 判断 2
    dy2 = y + 34
    diamond(d, cx, dy2, 330, 68, "状态指纹变化？")
    # 是 → 重绘（右侧）
    arrow(d, (cx + 165, dy2), (cx + 250, dy2))
    label(d, (cx + 196, dy2 - 16), "是", GREEN, 12)
    d.text(((cx + 262) * S, dy2 * S), "重绘 HUD 与棋盘\n（否则本帧直接返回）",
           font=font(12.5, True), fill=GREEN, anchor="lm", spacing=8 * S)

    # 分步档的“是”分支接到判断 2 的左侧
    arrow(d, (70, dy1 + 300), (cx - 170, dy2), ORANGE)

    img.resize((W * 2, H * 2), Image.LANCZOS).save(os.path.join(OUT, "fig2-frame-loop.png"))
    print("fig2 ok")


# ---------------------------------------------------------------- 图 3 存档状态机
def fig_save():
    W, H = 980, 540
    img, d = canvas(W, H)
    p = Painter(d)

    d.rounded_rectangle([30 * S, 26 * S, 190 * S, 76 * S], radius=8 * S,
                        fill=GREY_BG, outline=MUTED, width=2 * S)
    d.text((110 * S, 51 * S), "启动 Load()", font=font(15, True), fill=INK, anchor="mm")

    outcomes = [
        ("Missing（首次运行）", "最高分 0，正常开局", 96, GREEN, GREEN_BG),
        ("Ok（格式正确）", "显示历史最高分", 196, GREEN, GREEN_BG),
        ("Corrupted", "忽略旧档，按 0 分开局", 296, ORANGE, ORANGE_BG),
        ("Unreadable", "忽略旧档，按 0 分开局", 396, ORANGE, ORANGE_BG),
    ]

    for title, note, yc, c, bg in outcomes:
        d.line([190 * S, 51 * S, 232 * S, 51 * S], fill=MUTED, width=2 * S)
        d.line([232 * S, 51 * S, 232 * S, yc * S], fill=MUTED, width=2 * S)
        arrow(d, (232, yc), (268, yc))
        d.rounded_rectangle([268 * S, (yc - 32) * S, 470 * S, (yc + 32) * S], radius=8 * S,
                            fill=bg, outline=c, width=2 * S)
        d.text((282 * S, yc * S), title, font=font(13, True), fill=c, anchor="lm")
        d.text((490 * S, yc * S), note, font=font(12.5), fill=INK, anchor="lm")

    p.box((268 + 0, 452, 950, 512), "Save()：写 highscore.xml.tmp → Flush(true) → File.Move 覆盖",
          [], BLUE, BLUE_BG, 13, 12)
    d.text((490 * S, 434 * S), "写失败（只读 / 磁盘满 / 被占用）→ 返回 false，界面提示未保存，游戏不崩溃",
           font=font(11.5), fill=ORANGE, anchor="lm")

    img.resize((W * 2, H * 2), Image.LANCZOS).save(os.path.join(OUT, "fig3-save-states.png"))
    print("fig3 ok")


# ---------------------------------------------------------------- 图 4 界面布局
def fig_ui():
    W, H = 860, 660
    img, d = canvas(W, H)

    BG = (16, 16, 22)
    BOARD = (26, 26, 34)
    GRID = (44, 44, 58)
    TEXT = (232, 232, 240)
    MUTE = (140, 140, 160)
    ACC = (120, 210, 255)

    d.rectangle([0, 0, W * S, H * S], fill=BG)
    d.rectangle([0, 0, W * S, 34 * S], fill=(22, 22, 30))
    d.text((14 * S, 17 * S), "俄罗斯方块 · C# 大作业", font=font(13, True), fill=TEXT, anchor="lm")

    cell = 33
    bw, bh = cell * 12, cell * 16
    ox, oy = 24, 66
    d.rectangle([ox * S, oy * S, (ox + bw) * S, (oy + bh) * S], fill=BOARD)
    for i in range(1, 12):
        d.line([(ox + i * cell) * S, oy * S, (ox + i * cell) * S, (oy + bh) * S], fill=GRID, width=S)
    for j in range(1, 16):
        d.line([ox * S, (oy + j * cell) * S, (ox + bw) * S, (oy + j * cell) * S], fill=GRID, width=S)

    colors = {"I": (0, 229, 255), "O": (255, 212, 0), "T": (177, 76, 255), "S": (61, 220, 132),
              "Z": (255, 77, 77), "J": (77, 121, 255), "L": (255, 159, 28)}
    rows = [
        "............",
        "............",
        "............",
        "............",
        "............",
        "............",
        "............",
        "............",
        "............",
        ".....SS.....",
        ".....SS..LL.",
        "..TTT....L..",
        "..T....ZZZ..",
        "III....Z....",
        "I.JJJ..ZZ.OO",
        "IIJJJ...OOO.",
    ]
    for y, line in enumerate(rows):
        for x, ch in enumerate(line):
            if ch == ".":
                continue
            d.rectangle([(ox + x * cell + 1) * S, (oy + y * cell + 1) * S,
                         (ox + (x + 1) * cell - 1) * S, (oy + (y + 1) * cell - 1) * S],
                        fill=colors[ch])

    # 正在下落的方块（顶部）
    for (px_, py_) in [(5, 1), (6, 1), (5, 2), (5, 3)]:
        d.rectangle([(ox + px_ * cell + 1) * S, (oy + py_ * cell + 1) * S,
                     (ox + (px_ + 1) * cell - 1) * S, (oy + (py_ + 1) * cell - 1) * S],
                    fill=colors["T"])

    sx = ox + bw + 26
    sw = W - sx - 14
    y = 60

    def hud(text, size, color, dy, bold=False):
        nonlocal y
        d.text((sx * S, y * S), text, font=font(size, bold), fill=color, anchor="lt")
        y += dy

    hud("分数  0x1A2B", 15, ACC, 40, True)
    hud("最高  0x3C50", 12, TEXT, 30)
    hud("等级  5", 12, TEXT, 28)
    hud("消行  42", 12, TEXT, 28)
    hud("速度  1.0×（F4）", 10, MUTE, 26)
    hud("下一个", 10, MUTE, 22)

    pv_w = (sw - 12) / 3
    preview = {"J": [(0, 1), (1, 1), (2, 1), (2, 0)],
               "S": [(1, 0), (2, 0), (0, 1), (1, 1)],
               "I": [(0, 1), (1, 1), (2, 1), (3, 1)]}
    for i, kind in enumerate(["J", "S", "I"]):
        px = sx + i * (pv_w + 6)
        d.rectangle([px * S, y * S, (px + pv_w) * S, (y + 62) * S], fill=BOARD, outline=GRID, width=S)
        cellp = 12
        cells = preview[kind]
        min_x = min(c[0] for c in cells)
        max_x = max(c[0] for c in cells)
        min_y = min(c[1] for c in cells)
        max_y = max(c[1] for c in cells)
        wpx = (max_x - min_x + 1) * cellp
        hpx = (max_y - min_y + 1) * cellp
        base_x = px + (pv_w - wpx) / 2
        base_y = y + (62 - hpx) / 2
        for (gx, gy) in cells:
            d.rectangle([(base_x + (gx - min_x) * cellp + 1) * S, (base_y + (gy - min_y) * cellp + 1) * S,
                         (base_x + (gx - min_x + 1) * cellp - 1) * S, (base_y + (gy - min_y + 1) * cellp - 1) * S],
                        fill=colors[kind])
    y += 62 + 18

    for h in ["← →  左右移动", "↑ / X  顺时针", "Z  逆时针", "↓  软降    空格  硬降",
              "P / Esc  暂停", "F2  重新开始", "F3  AI 观战 / 接管",
              "F4  下落速度    F5  AI 节奏"]:
        hud(h, 10, MUTE, 20)

    y += 10
    hud("评分 12.4 | 消行 1 | 空洞 3 | 凹凸 5", 9, ACC, 18)
    hud("AI 接管（F3 交回玩家）", 9, MUTE, 18)

    img.resize((W * 2, H * 2), Image.LANCZOS).save(os.path.join(OUT, "fig4-ui-layout.png"))
    print("fig4 ok")


fig_architecture()
fig_frame()
fig_save()
fig_ui()
print("done", OUT)
