# -*- coding: utf-8 -*-
"""生成《研究报告》DOCX：封面 + 摘要 + 六章 + 参考文献 + 附录。"""

import os
import re

from docx import Document
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Cm, Pt, RGBColor

from report_content import CHAPTERS

ROOT = r"C:\Users\17102\Desktop\C#程序设计"
OUT_DIR = os.path.join(ROOT, "提交材料")
FIG = os.path.join(OUT_DIR, "figures")
DOCX = os.path.join(OUT_DIR, "研究报告-俄罗斯方块.docx")

BODY_FONT = "宋体"
HEAD_FONT = "黑体"
CODE_FONT = "Consolas"
INK = RGBColor(0x1A, 0x1A, 0x1A)
GREY = RGBColor(0x55, 0x55, 0x55)

TABLES = {
    "t21": dict(
        cap="表 2-1　需求条目与实现口径对照",
        header=["需求条目", "我们的实现口径"],
        widths=[4.6, 11.2],
        rows=[
            ["棋盘 12 列 × 16 行", "GameConfig.BoardWidth=12、BoardHeight=16；坐标 [x, y]，x 为列、y 为行、y=0 在顶部"],
            ["七种方块与旋转", "手写 4 个旋转态数据，双向旋转，简化 SRS 踢墙（见 2.2 第 1 条）"],
            ["计分 100 × k", "ScoreFor(k) 返回 100×k，无等级倍率、无硬降分、无 T-spin 奖励"],
            ["等级加速", "每消 10 行升 1 级；13 档重力 800→50 ms（见 2.2 第 2 条）"],
            ["分数十六进制", "界面统一格式化为 0x 前缀大写十六进制；内部与存档一律十进制"],
            ["最高分 XML", "单条 HighScore 记录，%AppData%\\TetrisCourse\\highscore.xml，启动即读、破纪录即写"],
            ["逻辑与界面分离", "Tetris.Core 不引用任何界面技术；GameState 可独立 new、可 Clone 深拷贝"],
            ["单元测试不少于 5 个", "实际交付 88 个 xUnit 用例，覆盖碰撞、旋转、消行、计分、引擎时序、存档与 AI"],
        ],
    ),
    "t31": dict(
        cap="表 3-1　项目划分与代码规模（不含生成代码）",
        header=["项目", "行数", "职责"],
        widths=[5.2, 2.2, 8.4],
        rows=[
            ["Tetris.Core", "1335", "状态、规则、引擎、全部接口，无任何界面依赖"],
            ["Tetris.App", "1297", "WinForms 界面、自绘渲染、输入路由、存档接线"],
            ["Tetris.AI", "585", "评估函数、落点搜索、指令状态机"],
            ["Tetris.Persistence", "192", "最高分 XML 读写"],
            ["tests/Tetris.Core.Tests", "1637", "88 个用例与两套测试替身"],
        ],
    ),
    "t32": dict(
        cap="表 3-2　五个接口契约",
        header=["接口", "实现", "调用方", "职责"],
        widths=[3.4, 4.0, 3.4, 5.2],
        rows=[
            ["IGameRules", "StandardGameRules", "引擎、AI、测试", "碰撞、下落距离、移动、旋转踢墙、锁定消行"],
            ["IGameEngine", "GameEngine", "界面", "开局、接受命令、推进一帧、读状态"],
            ["IHighScoreStore", "XmlHighScoreStore", "界面、测试", "读写最高分，失败不抛异常"],
            ["IPlayerAgent", "HeuristicAgent", "界面", "每帧产出一条 GameCommand"],
            ["IPlayerAgentDebug", "HeuristicAgent", "界面（可选）", "暴露候选落点与选中项，用于画热力图"],
        ],
    ),
    "t41": dict(
        cap="表 4-1　实现效果与验证结果",
        header=["验证项", "结果"],
        widths=[6.4, 9.4],
        rows=[
            ["Release 编译", "5 个项目 + 测试工程，0 警告 0 错误"],
            ["单元 / 集成测试", "88 个用例全部通过，整轮约 1.25 秒"],
            ["AI 长跑（10 个种子，每局上限 10000 帧）", "平均 68730 分 / 687 行，10 局全部跑满上限仍未顶死"],
            ["存档异常", "缺失 / 损坏 / 超大 / 只读 / 被占用 / 路径是目录，六种场景均不崩溃"],
            ["窗口缩放与失焦", "最小 720 × 560，缩放不闪；失焦自动暂停"],
            ["发布产物", "dotnet publish（win-x64 框架依赖）11 个文件、约 0.3 MB"],
        ],
    ),
    "t42": dict(
        cap="表 4-2　核心性能数据",
        header=["指标", "数值", "测量方式"],
        widths=[6.2, 5.6, 6.2],
        rows=[
            ["逻辑帧耗时（纯重力）", "0.44 µs / 帧", "20 局共 69041 帧，Stopwatch 计时"],
            ["逻辑帧耗时（含命令）", "2.00 µs / 帧", "20 局，每帧一条命令 + 一次 Tick"],
            ["GameState.Clone", "0.17 µs / 次", "20 万次深拷贝"],
            ["AI 落点搜索（1-ply）", "0.069 ms 均值 / 0.49 ms P99", "1.8 万次搜索，平均 42 个候选"],
            ["AI 落点搜索（2-ply）", "0.213 ms 均值 / 0.94 ms P99", "2.1 万次搜索，最坏 10.7 ms（首块 JIT）"],
            ["单元测试", "88 个 / 1.25 秒", "反射直接驱动 xUnit 用例"],
        ],
    ),
    "t43": dict(
        cap="表 4-3　AI 在真引擎上的长跑成绩",
        header=["配置", "平均分", "平均消行", "平均存活帧", "顶死局数"],
        widths=[3.8, 3.4, 3.4, 4.4, 2.8],
        rows=[
            ["1-ply", "59330", "593.3", "8713", "3 / 10"],
            ["2-ply（默认）", "68730", "687.3", "10000（上限）", "0 / 10"],
        ],
    ),
    "ta1": dict(
        cap="表 A-1　AI 使用情况明细",
        header=["工具", "使用位置", "采纳情况", "被否决 / 修改的内容"],
        widths=[2.6, 5.4, 3.2, 5.6],
        rows=[
            ["DeepSeek", "接口草案、注释、任务书结构", "部分采纳", "过度抽象（容器、IRenderer）被去掉"],
            ["DeepSeek", "计分规则与等级曲线建议", "否决", "带等级倍率与硬降奖励，偏离 100×k 需求"],
            ["Claude", "WinForms 布局与自绘样板代码", "采纳后重构", "布局改为 TableLayoutPanel 两栏"],
            ["Claude", "2-ply 落点加权方案", "否决", "全量加权导致 380 帧顶死，改为并列最优裁决"],
            ["Claude", "测试用例边界清单", "采纳", "逐条改写为可直接运行的 xUnit 用例"],
        ],
    ),
}


# ----------------------------------------------------------------- 排版工具
def set_run_font(run, name, size=None, bold=None, color=None, eastasia=None):
    run.font.name = name
    if size is not None:
        run.font.size = Pt(size)
    if bold is not None:
        run.bold = bold
    if color is not None:
        run.font.color.rgb = color
    rpr = run._element.get_or_add_rPr()
    rfonts = rpr.find(qn("w:rFonts"))
    if rfonts is None:
        rfonts = OxmlElement("w:rFonts")
        rpr.append(rfonts)
    rfonts.set(qn("w:ascii"), name)
    rfonts.set(qn("w:hAnsi"), name)
    rfonts.set(qn("w:eastAsia"), eastasia or name)


def shade(paragraph, hex_fill):
    ppr = paragraph._p.get_or_add_pPr()
    shd = OxmlElement("w:shd")
    shd.set(qn("w:val"), "clear")
    shd.set(qn("w:color"), "auto")
    shd.set(qn("w:fill"), hex_fill)
    ppr.append(shd)


def para_border(paragraph, color="D8D8D8"):
    ppr = paragraph._p.get_or_add_pPr()
    borders = OxmlElement("w:pBdr")
    for side in ("top", "left", "bottom", "right"):
        el = OxmlElement(f"w:{side}")
        el.set(qn("w:val"), "single")
        el.set(qn("w:sz"), "4")
        el.set(qn("w:space"), "4")
        el.set(qn("w:color"), color)
        borders.append(el)
    ppr.append(borders)


def body(doc, text, indent=True, size=12, spacing=1.5, space_after=3):
    p = doc.add_paragraph()
    pf = p.paragraph_format
    pf.line_spacing = spacing
    pf.space_before = Pt(0)
    pf.space_after = Pt(space_after)
    if indent:
        pf.first_line_indent = Pt(size * 2)
    set_run_font(p.add_run(text), BODY_FONT, size, False, INK)
    return p


def heading(doc, text, level):
    size = {1: 16, 2: 14, 3: 12}[level]
    before = {1: 16, 2: 12, 3: 8}[level]
    after = {1: 10, 2: 6, 3: 4}[level]
    p = doc.add_paragraph()
    pf = p.paragraph_format
    pf.space_before = Pt(before)
    pf.space_after = Pt(after)
    pf.line_spacing = 1.3
    pf.keep_with_next = True
    set_run_font(p.add_run(text), HEAD_FONT, size, True, INK)
    return p


def code_block(doc, text):
    p = doc.add_paragraph()
    pf = p.paragraph_format
    pf.line_spacing = 1.0
    pf.space_before = Pt(4)
    pf.space_after = Pt(8)
    pf.left_indent = Cm(0.5)
    pf.right_indent = Cm(0.3)
    shade(p, "F5F6F8")
    para_border(p)
    lines = text.strip("\n").split("\n")
    for i, line in enumerate(lines):
        run = p.add_run(line)
        set_run_font(run, CODE_FONT, 9, False, RGBColor(0x22, 0x26, 0x30))
        if i < len(lines) - 1:
            run.add_break()
    return p


def caption(doc, text):
    p = doc.add_paragraph()
    pf = p.paragraph_format
    pf.alignment = WD_ALIGN_PARAGRAPH.CENTER
    pf.space_before = Pt(4)
    pf.space_after = Pt(10)
    pf.line_spacing = 1.2
    set_run_font(p.add_run(text), BODY_FONT, 10, False, GREY)
    return p


def figure(doc, filename, cap, width_cm=14.5):
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p.paragraph_format.space_before = Pt(8)
    p.paragraph_format.space_after = Pt(2)
    p.paragraph_format.keep_with_next = True
    p.add_run().add_picture(os.path.join(FIG, filename), width=Cm(width_cm))
    caption(doc, cap)


def table(doc, spec, font_size=9.5):
    if spec.get("cap"):
        p = doc.add_paragraph()
        pf = p.paragraph_format
        pf.alignment = WD_ALIGN_PARAGRAPH.CENTER
        pf.space_before = Pt(8)
        pf.space_after = Pt(3)
        pf.keep_with_next = True
        set_run_font(p.add_run(spec["cap"]), BODY_FONT, 10, False, GREY)

    t = doc.add_table(rows=1, cols=len(spec["header"]))
    t.style = "Table Grid"
    t.alignment = WD_TABLE_ALIGNMENT.CENTER
    for i, h in enumerate(spec["header"]):
        cell = t.rows[0].cells[i]
        cell.text = ""
        p = cell.paragraphs[0]
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p.paragraph_format.space_before = Pt(2)
        p.paragraph_format.space_after = Pt(2)
        set_run_font(p.add_run(h), BODY_FONT, font_size, True, INK)
        shade(p, "EDEFF2")

    for row in spec["rows"]:
        cells = t.add_row().cells
        for i, value in enumerate(row):
            cell = cells[i]
            cell.text = ""
            p = cell.paragraphs[0]
            p.alignment = WD_ALIGN_PARAGRAPH.LEFT if len(str(value)) > 16 else WD_ALIGN_PARAGRAPH.CENTER
            p.paragraph_format.space_before = Pt(1.5)
            p.paragraph_format.space_after = Pt(1.5)
            p.paragraph_format.line_spacing = 1.15
            set_run_font(p.add_run(str(value)), BODY_FONT, font_size, False, INK)

    for i, w in enumerate(spec["widths"]):
        for row in t.rows:
            row.cells[i].width = Cm(w)
    return t


def page_number_footer(section):
    footer = section.footer
    p = footer.paragraphs[0]
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run = p.add_run()
    fld_begin = OxmlElement("w:fldChar")
    fld_begin.set(qn("w:fldCharType"), "begin")
    instr = OxmlElement("w:instrText")
    instr.set(qn("xml:space"), "preserve")
    instr.text = "PAGE"
    fld_end = OxmlElement("w:fldChar")
    fld_end.set(qn("w:fldCharType"), "end")
    run._r.append(fld_begin)
    run._r.append(instr)
    run._r.append(fld_end)
    set_run_font(run, BODY_FONT, 9, False, GREY)


def new_document():
    doc = Document()
    style = doc.styles["Normal"]
    style.font.name = BODY_FONT
    style.font.size = Pt(12)
    style.element.rPr.rFonts.set(qn("w:eastAsia"), BODY_FONT)

    section = doc.sections[0]
    section.page_width = Cm(21.0)
    section.page_height = Cm(29.7)
    section.top_margin = Cm(2.54)
    section.bottom_margin = Cm(2.54)
    section.left_margin = Cm(2.6)
    section.right_margin = Cm(2.6)
    page_number_footer(section)
    return doc


# ----------------------------------------------------------------- 前置部分
def build_cover(doc):
    for _ in range(3):
        doc.add_paragraph()

    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    set_run_font(p.add_run("C# 程序设计 · 大作业研究报告"), HEAD_FONT, 15, False, GREY)
    p.paragraph_format.space_after = Pt(28)

    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    set_run_font(p.add_run("俄罗斯方块的设计与实现"), HEAD_FONT, 30, True, INK)
    p.paragraph_format.space_after = Pt(10)

    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    set_run_font(p.add_run("——基于 .NET 10 + WinForms 的分离式架构、AI 自动玩家与健壮性设计"),
                 BODY_FONT, 13, False, GREY)
    p.paragraph_format.space_after = Pt(46)

    for text in ("项目名称：俄罗斯方块（Tetris）", "运行环境：Windows 10 / 11 · .NET SDK 10.0 · WinForms"):
        p = doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        set_run_font(p.add_run(text), BODY_FONT, 12, False, INK)
        p.paragraph_format.space_after = Pt(6)

    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    set_run_font(p.add_run("小组成员与分工"), HEAD_FONT, 12, True, INK)
    p.paragraph_format.space_before = Pt(20)
    p.paragraph_format.space_after = Pt(6)

    table(
        doc,
        dict(
            header=["学号", "姓名", "分工", "贡献比"],
            widths=[3.2, 2.6, 8.4, 2.0],
            rows=[
                ["【学号 1】", "【姓名 1】", "组长。核心规则与引擎（Tetris.Core）、发布与集成、报告数据实测", "40%"],
                ["【学号 2】", "【姓名 2】", "WinForms 界面、输入（DAS/ARR）、XML 存档与异常处理", "32%"],
                ["【学号 3】", "【姓名 3】", "启发式 AI（评估 / 落点搜索 / 指令状态机）与对应单元测试", "28%"],
            ],
        ),
        font_size=10,
    )

    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    set_run_font(p.add_run("（学号、姓名请按实际情况填写；贡献比三人不同，合计 100%）"), BODY_FONT, 9, False, GREY)
    p.paragraph_format.space_before = Pt(8)
    p.paragraph_format.space_after = Pt(34)

    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    set_run_font(p.add_run("2026 年 10 月 30 日"), BODY_FONT, 12, False, INK)

    doc.add_page_break()


def build_abstract(doc):
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    set_run_font(p.add_run("摘　要"), HEAD_FONT, 16, True, INK)
    p.paragraph_format.space_after = Pt(14)

    body(
        doc,
        "本文报告了以 C# 与 .NET 10 实现的俄罗斯方块大作业。程序采用 WinForms 界面，严格实现 12 列 × 16 行棋盘、"
        "七种方块的移动与双向旋转、等级加速、暂停与结束判定，分数按“一次消 k 行得 100 × k”计算并以十六进制显示，"
        "历史最高分以 XML 保存在用户漫游目录。工程按“逻辑与界面分离”拆成五个项目：Core 不引用任何界面技术，"
        "状态可脱离窗口独立创建并深拷贝，界面只通过五个接口读状态、下发命令。扩展功能选择 AI 自动玩家：四项棋盘特征的"
        "线性评估配合 1-ply / 2-ply 落点搜索，在真引擎上 10 个种子的平均成绩为 68730 分、687 行，10 局均未被顶死。"
        "88 个 xUnit 用例覆盖碰撞、旋转踢墙、消行计分、引擎时序、存档异常与 AI 不变量，全部通过。报告给出逻辑帧 "
        "0.44 µs、深拷贝 0.17 µs、单次落点搜索 0.21 ms 等数据，并复盘了落点盲区、2-ply 加权失效等问题。",
    )

    p = doc.add_paragraph()
    p.paragraph_format.space_before = Pt(12)
    set_run_font(p.add_run("关键词："), HEAD_FONT, 12, True, INK)
    set_run_font(p.add_run("俄罗斯方块；C#；WinForms；关注点分离；启发式搜索；异常处理"), BODY_FONT, 12, False, INK)

    doc.add_page_break()


def build_chapters(doc):
    for kind, payload in CHAPTERS:
        if kind == "h1":
            heading(doc, payload, 1)
        elif kind == "h2":
            heading(doc, payload, 2)
        elif kind == "h3":
            heading(doc, payload, 3)
        elif kind == "p":
            body(doc, payload)
        elif kind == "pi":
            body(doc, payload, indent=False)
        elif kind == "code":
            code_block(doc, payload)
        elif kind == "fig":
            figure(doc, *payload)
        elif kind == "table":
            table(doc, TABLES[payload])
        else:
            raise ValueError(kind)


def build_references(doc):
    heading(doc, "参考文献", 1)
    refs = [
        "Microsoft. Windows Forms Timer 类 [EB/OL]. https://learn.microsoft.com/dotnet/api/system.windows.forms.timer",
        "Microsoft. XmlSerializer 类 [EB/OL]. https://learn.microsoft.com/dotnet/api/system.xml.serialization.xmlserializer",
        "Microsoft. .NET 应用程序发布概述 [EB/OL]. https://learn.microsoft.com/dotnet/core/deploying/",
        "xUnit.net. Getting Started with xUnit.net (CLI) [EB/OL]. https://xunit.net/docs/getting-started/netcore/cmdline",
        "Tetris Wiki. Tetris Guideline [EB/OL]. https://tetris.wiki/Tetris_Guideline",
        "Tetris Wiki. Super Rotation System [EB/OL]. https://tetris.wiki/Super_Rotation_System",
        "Tetris Wiki. Random Generator (7-bag) [EB/OL]. https://tetris.wiki/Random_Generator",
    ]
    for i, ref in enumerate(refs, 1):
        p = doc.add_paragraph()
        pf = p.paragraph_format
        pf.left_indent = Cm(0.9)
        pf.first_line_indent = Cm(-0.9)
        pf.line_spacing = 1.4
        pf.space_after = Pt(3)
        set_run_font(p.add_run(f"[{i}] {ref}"), BODY_FONT, 10.5, False, INK)


def build_appendix(doc):
    doc.add_page_break()
    heading(doc, "附录 A　AI 使用声明", 1)
    body(
        doc,
        "本组在本次大作业中使用了 AI 工具辅助开发，如实声明如下：所用工具为 DeepSeek 与 Claude（对话式编码助手）。"
        "AI 参与的环节包括接口草案与注释初稿、WinForms 布局与 XML 序列化的样板代码、边界测试用例的枚举、文档措辞整理。"
        "核心算法（碰撞、旋转踢墙、锁定消行、落点搜索、AI 指令状态机）由组员自行推导、逐行核对，并承担现场解释责任。",
    )
    table(doc, TABLES["ta1"])
    body(
        doc,
        "所有 AI 生成的代码都经过组员阅读、改写与测试验证后才进入仓库；提交记录中的日期与内容反映真实开发顺序。"
        "验收时如被问到任意一段代码，我们可以说明它的写法与取舍。",
    )

    heading(doc, "附录 B　复现与验证", 1)
    body(doc, "环境要求：Windows 10 / 11，.NET SDK 10.0（dotnet --version 显示 10.0.x）。", indent=False)
    code_block(
        doc,
        """dotnet build Tetris.sln
dotnet run --project src/Tetris.App
dotnet test""",
    )
    body(doc, "发布（目标机器需安装 .NET 10 桌面运行时）：", indent=False)
    code_block(
        doc,
        """dotnet publish src/Tetris.App -c Release -r win-x64 --self-contained false -o publish""",
    )
    body(
        doc,
        "存档位于 %AppData%\\TetrisCourse\\highscore.xml。删除后启动应提示“首次运行，暂无存档”并正常开局；改成乱码后启动应"
        "提示“存档损坏，已忽略”。这两步就是演示视频里的异常场景。",
    )


def count_words(path):
    doc = Document(path)
    total = 0
    per_chapter = {}
    current = "封面/摘要"
    for p in doc.paragraphs:
        text = p.text.strip()
        if not text:
            continue
        if re.match(r"^第[一二三四五六]章", text) or text in ("参考文献", "附录 A　AI 使用声明", "附录 B　复现与验证"):
            current = text
        if text.startswith("[") and "EB/OL" in text:
            continue
        if text.startswith(("图 ", "表 ", "（学号", "摘　要", "关键词")):
            continue
        if p.runs and p.runs[0].font.name == CODE_FONT:
            continue
        count = len(re.findall(r"[\u4e00-\u9fff]", text))
        total += count
        per_chapter[current] = per_chapter.get(current, 0) + count
    for name, count in per_chapter.items():
        print(f"  {name}: {count}")
    print(f"正文汉字数（不含代码/表格/参考文献）：{total}")


def main():
    doc = new_document()
    build_cover(doc)
    build_abstract(doc)
    build_chapters(doc)
    build_references(doc)
    build_appendix(doc)
    doc.save(DOCX)
    count_words(DOCX)
    print(f"saved: {DOCX}")


if __name__ == "__main__":
    main()
