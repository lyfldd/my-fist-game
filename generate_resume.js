const fs = require("fs");
const {
  Document, Packer, Paragraph, TextRun, Table, TableRow, TableCell,
  Header, AlignmentType, BorderStyle, WidthType, ShadingType
} = require("docx");

// ============ 样式常量 ============
const PRIMARY = "1a5276";
const ACCENT = "2980b9";
const LIGHT_BG = "eaf2f8";
const DARK_BG = "1a5276";
const TEXT = "2c3e50";
const LIGHT_TEXT = "7f8c8d";
const BORDER_COLOR = "bdc3c7";
const WHITE = "ffffff";

const border = { style: BorderStyle.SINGLE, size: 1, color: BORDER_COLOR };
const noBorder = { style: BorderStyle.NONE, size: 0 };
const noBorders = { top: noBorder, bottom: noBorder, left: noBorder, right: noBorder };

function cellMargins(top = 60, bottom = 60, left = 100, right = 100) {
  return { top, bottom, left, right };
}

function p(text, opts = {}) {
  return new Paragraph({
    spacing: { before: opts.before || 0, after: opts.after || 40, line: opts.line || 276 },
    alignment: opts.align,
    children: [new TextRun({
      text, bold: opts.bold, size: opts.size || 20,
      font: "Microsoft YaHei", color: opts.color || TEXT, italics: opts.italics
    })]
  });
}

function bulletP(text, opts = {}) {
  return new Paragraph({
    spacing: { before: 0, after: 30, line: 260 },
    indent: { left: 360, hanging: 180 },
    children: [
      new TextRun({ text: "\u2022 ", bold: true, color: ACCENT, size: 20, font: "Microsoft YaHei" }),
      new TextRun({ text, size: opts.size || 20, font: "Microsoft YaHei", color: TEXT, bold: opts.bold })
    ]
  });
}

function sectionDivider() {
  return new Paragraph({
    spacing: { before: 80, after: 80 },
    border: { bottom: { style: BorderStyle.SINGLE, size: 2, color: ACCENT, space: 1 } },
    children: []
  });
}

function sectionTitle(text) {
  return new Paragraph({
    spacing: { before: 160, after: 60 },
    border: { bottom: { style: BorderStyle.SINGLE, size: 4, color: PRIMARY, space: 4 } },
    children: [
      new TextRun({ text: "  ", size: 10, font: "Microsoft YaHei", color: WHITE,
        shading: { fill: PRIMARY, type: ShadingType.CLEAR } }),
      new TextRun({ text: " " + text, bold: true, size: 26, font: "Microsoft YaHei", color: PRIMARY })
    ]
  });
}

function subTitle(text) {
  return new Paragraph({
    spacing: { before: 100, after: 40 },
    children: [new TextRun({ text, bold: true, size: 22, font: "Microsoft YaHei", color: ACCENT })]
  });
}

function techLabel(text) {
  return new TextRun({ text, bold: true, size: 20, font: "Microsoft YaHei", color: ACCENT });
}
function techBody(text) {
  return new TextRun({ text, size: 20, font: "Microsoft YaHei", color: TEXT });
}

// ============ 构建文档 ============
const doc = new Document({
  styles: {
    default: { document: { run: { font: "Microsoft YaHei", size: 20 } } },
  },
  sections: [{
    properties: {
      page: {
        size: { width: 11906, height: 16838 },
        margin: { top: 900, right: 1080, bottom: 900, left: 1080 }
      }
    },
    headers: {
      default: new Header({
        children: [new Paragraph({
          alignment: AlignmentType.RIGHT,
          border: { bottom: { style: BorderStyle.SINGLE, size: 1, color: BORDER_COLOR, space: 2 } },
          children: [new TextRun({ text: "Unity \u6E38\u620F\u5BA2\u6237\u7AEF\u5F00\u53D1\u5B9E\u4E60\u7B80\u5386",
            size: 16, color: LIGHT_TEXT, font: "Microsoft YaHei" })]
        })]
      })
    },
    children: [
      // ===== 个人信息 =====
      new Paragraph({
        spacing: { after: 0 },
        children: [new TextRun({ text: "[\u59D3\u540D]", bold: true, size: 44, font: "Microsoft YaHei", color: PRIMARY })]
      }),
      new Paragraph({
        spacing: { before: 0, after: 20, line: 300 },
        children: [
          new TextRun({ text: "\u6C42\u804C\u610F\u5411: Unity \u6E38\u620F\u5BA2\u6237\u7AEF\u5F00\u53D1\uFF08\u5B9E\u4E60\uFF09  |  \u5B66\u5386: [\u5B66\u6821] \u00B7 [\u4E13\u4E1A]  |  \u9884\u8BA1\u6BD5\u4E1A: [\u65F6\u95F4]",
            size: 20, font: "Microsoft YaHei", color: TEXT })
        ]
      }),
      new Paragraph({
        spacing: { before: 0, after: 20 },
        children: [
          new TextRun({ text: "\u7535\u8BDD: [\u8054\u7CFB\u65B9\u5F0F]  |  \u90AE\u7BB1: [\u90AE\u7BB1]  |  GitHub: [GitHub\u94FE\u63A5]",
            size: 20, font: "Microsoft YaHei", color: TEXT })
        ]
      }),

      sectionDivider(),

      // ===== 项目经历 =====
      sectionTitle("\u9879\u76EE\u7ECF\u5386"),

      new Paragraph({
        spacing: { before: 60, after: 20 },
        children: [
          new TextRun({ text: "3D \u672B\u65E5\u4E27\u5C38\u751F\u5B58\u6C99\u76D2\u6E38\u620F (\u7C7B Project Zomboid)",
            bold: true, size: 24, font: "Microsoft YaHei", color: PRIMARY }),
          new TextRun({ text: "  |  \u72EC\u7ACB\u5F00\u53D1  |  2026.05 \u2013 \u4ECA",
            size: 20, font: "Microsoft YaHei", color: LIGHT_TEXT })
        ]
      }),
      new Paragraph({
        spacing: { before: 0, after: 40 },
        children: [new TextRun({
          text: "Unity 2022 LTS / \u56E2\u7ED3\u5F15\u64CE + C# + Built-in RP  |  3D 45\u00B0\u4FEF\u89C6\u89D2  |  \u5173\u5361\u751F\u6210 / \u7269\u7406 / \u52A8\u753B / UI / \u7C92\u5B50 / AI  |  211\u4E2A C#\u811A\u672C / 14+\u6838\u5FC3\u7CFB\u7EDF",
          size: 18, font: "Microsoft YaHei", color: ACCENT, italics: true
        })]
      }),

      // --- STAR 概述 ---
      new Table({
        width: { size: 9746, type: WidthType.DXA },
        columnWidths: [9746],
        rows: [new TableRow({
          children: [new TableCell({
            borders: { top: border, bottom: border,
              left: { style: BorderStyle.SINGLE, size: 8, color: PRIMARY }, right: border },
            width: { size: 9746, type: WidthType.DXA },
            shading: { fill: LIGHT_BG, type: ShadingType.CLEAR },
            margins: cellMargins(80, 80, 160, 120),
            children: [new Paragraph({
              spacing: { after: 0 },
              children: [new TextRun({
                text: "\u72EC\u7ACB\u8BBE\u8BA1\u5E76\u5B9E\u73B0\u4E86\u4E00\u6B3E\u7C7B Project Zomboid \u7684 3D \u4E27\u5C38\u751F\u5B58\u6C99\u76D2\u6E38\u620F\uFF0C\u6DB5\u76D6\u5173\u5361\u751F\u6210\u3001\u6218\u6597\u3001\u5EFA\u9020\u3001\u4EA4\u901A\u3001\u8D44\u6E90\u7BA1\u7406\u3001\u4E27\u5C38AI\u3001\u7535\u529B\u7F51\u3001\u5408\u6210\u5236\u9020\u3001\u52A8\u753B\u3001\u7C92\u5B50\u7279\u6548\u7B49 14+ \u6838\u5FC3\u7CFB\u7EDF\uFF0C\u5B9E\u73B0\u4E86\u4ECE\u67B6\u6784\u8BBE\u8BA1\u5230\u7CFB\u7EDF\u96C6\u6210\u7684\u5B8C\u6574\u5F00\u53D1\u95ED\u73AF\u3002",
                bold: true, size: 19, font: "Microsoft YaHei", color: PRIMARY
              })]
            })]
          })]
        })]
      }),

      // --- S: 情境 ---
      subTitle("S \u2014 \u9879\u76EE\u80CC\u666F"),
      p("\u5F00\u53D1\u4E00\u6B3E\u5305\u542B\u5173\u5361\u751F\u6210\u3001\u6218\u6597\u3001\u5EFA\u9020\u3001\u4EA4\u901A\u3001\u751F\u5B58\u7684 3D \u6C99\u76D2\u6E38\u620F\u3002\u9700\u8981\u4ECE\u96F6\u8BBE\u8BA1\u53EF\u6269\u5C55\u67B6\u6784\uFF0C\u6574\u5408\u7269\u7406\u3001\u52A8\u753B\u3001UI\u3001\u7C92\u5B50\u7B49\u591A\u6A21\u5757\u5B9E\u73B0\u5B8C\u6574\u73A9\u6CD5\u95ED\u73AF\uFF0C\u5E76\u72EC\u7ACB\u5B8C\u6210\u6A21\u578B/\u8D34\u56FE/\u52A8\u753B\u7B49\u7F8E\u672F\u8D44\u6E90\u7684\u5BFC\u5165\u4E0E\u6574\u5408\u3002", { size: 20 }),

      // --- T: 任务 ---
      subTitle("T \u2014 \u6211\u7684\u804C\u8D23"),

      bulletP("\u5173\u5361\u8BBE\u8BA1 & \u573A\u666F\u642D\u5EFA: \u8BBE\u8BA1\u4E09\u5C42\u6A21\u5757\u5316\u7A0B\u5E8F\u5316\u57CE\u5E02\u751F\u6210\u7CFB\u7EDF\uFF0C\u57FA\u4E8E WFC \u7EA6\u675F\u6C42\u89E3\u5668\u5B9E\u73B0 20\u00D720 \u7F51\u683C (\u8986\u76D6 800m\u00D7800m) \u7684\u81EA\u52A8\u5173\u5361\u5E03\u5C40\uFF0C\u542B\u57CE\u5E02\u98CE\u683C\u89C4\u5212 (\u4E2D\u5FC3\u8F90\u5C04/\u6CBF\u8DEF\u5EF6\u4F38/\u6CBF\u6CB3\u4E24\u5CB8) \u81EA\u52A8\u529F\u80FD\u5206\u533A\u3002"),
      bulletP("\u6838\u5FC3\u73A9\u6CD5\u5F00\u53D1: \u4ECE\u96F6\u5B9E\u73B0\u6218\u6597\u7CFB\u7EDF (\u8FD1\u6218 OverlapSphere/\u8FDC\u7A0B\u96F7\u8FBE\u5C04\u7EBF\u68C0\u6D4B/\u9525\u5F62\u6563\u5C04 \u3001\u5DE6\u53F3\u624B\u53CC\u6301\u3001\u8F85\u52A9\u7784\u51C62m\u81EA\u52A8\u9501\u654C)\u3001\u5EFA\u9020\u7CFB\u7EDF (Ghost\u9884\u89C8+\u5341\u5B57\u7F51\u683C\u63A2\u6D4B+6\u5927\u7C7B46\u79CD\u5EFA\u9020\u7269)\u3001\u8F66\u8F86\u9A7E\u9A76 (WheelCollider\u7269\u7406+\u4E09\u5C42\u9632\u4FA7\u7FFB)\u3001\u751F\u5B58\u7CFB\u7EDF (\u9965\u997F/\u6E34\u4E86/\u4F53\u6E29/\u75B2\u52B3\u56DB\u7EF4\u5EA6)\u3002"),
      bulletP("\u52A8\u753B & \u7279\u6548: \u4F7F\u7528 Animator \u72B6\u6001\u673A\u5B9E\u73B0\u73A9\u5BB6\u79FB\u52A8/\u6218\u6597\u52A8\u753B\u5207\u6362\uFF1B\u57FA\u4E8E\u4E8B\u4EF6\u9A71\u52A8\u5B9E\u73B0 MuzzleFlashSystem \u67AA\u53E3\u706B\u7130\u7C92\u5B50\u7279\u6548\uFF0C\u652F\u6301 Small/Medium/Large \u4E09\u7EA7\u7C92\u5B50\u3002"),
      bulletP("\u7F8E\u672F\u8D44\u6E90\u6574\u5408: \u72EC\u7ACB\u5B8C\u6210 3D \u6A21\u578B\u5BFC\u5165 (OffRoad\u8D8A\u91CE\u8F66/FBX)\u3001\u7EB9\u7406/\u6750\u8D28\u81EA\u52A8\u5339\u914D\u3001ScriptableObject \u6570\u636E\u914D\u7F6E\u7BA1\u7EBF\u642D\u5EFA\u3002"),
      bulletP("AI \u7CFB\u7EDF: \u8BBE\u8BA1\u516D\u72B6\u6001\u901A\u7528 NPC \u72B6\u6001\u673A + \u89C6\u89C9\u9525\u611F\u77E5 (120\u00B0) + \u542C\u89C9\u611F\u77E5 + NavMeshAgent \u52A8\u6001\u8DEF\u5F84\u89C4\u5212 + \u5206\u5E27\u611F\u77E5\u4F18\u5316\u3002"),
      bulletP("\u7535\u529B/\u5408\u6210\u7CFB\u7EDF: \u5B9E\u73B0\u7535\u7F51\u62D3\u6251 (PowerSource\u2192PowerTerminal\u2192PowerConsumer) + 159\u79CD\u914D\u65B9 + 6\u7EA7\u7535\u6E90\u9012\u8FDB\u3002"),
      bulletP("\u5DE5\u5177\u94FE: \u5F00\u53D1 25+ Editor \u81EA\u52A8\u5316\u5DE5\u5177 (\u4E00\u952E\u751F\u6210\u7269\u54C1\u8D44\u4EA7\u3001\u63A7\u5236\u53F0\u8C03\u8BD5\u5668\u3001\u57CE\u5E02\u751F\u6210\u53EF\u89C6\u5316\u5DE5\u5177)\u3002"),

      // --- A: 行动 ---
      subTitle("A \u2014 \u6280\u672F\u5B9E\u8DF5"),

      new Paragraph({
        spacing: { before: 0, after: 40, line: 260 },
        children: [
          techLabel("\u67B6\u6784\u8BBE\u8BA1: "),
          techBody("EventBus \u6CDB\u578B\u4E8B\u4EF6\u7CFB\u7EDF + \u59D4\u6258 + try-catch \u5D29\u6E83\u9694\u79BB\uFF0C14+ \u7CFB\u7EDF\u5B9E\u73B0\u96F6\u8026\u5408\uFF0C\u65B0\u589E\u529F\u80FD\u65E0\u9700\u4FEE\u6539\u73B0\u6709\u4EE3\u7801\u3002\u6240\u6709\u6E38\u620F\u6570\u636E\u7EDF\u4E00\u4F7F\u7528 ScriptableObject \u914D\u7F6E\uFF0C\u53EF\u5728 Inspector \u76F4\u63A5\u7F16\u8F91\u3002")
        ]
      }),
      new Paragraph({
        spacing: { before: 0, after: 40, line: 260 },
        children: [
          techLabel("\u5173\u5361\u751F\u6210: "),
          techBody("WFC \u7EA6\u675F\u6C42\u89E3\u7B97\u6CD5 + 40m \u57FA\u7840\u5355\u5143 + \u591A\u5C3A\u5BF8\u6A21\u5757\u7CFB\u7EDF (1\u00D71~5\u00D75) + \u90BB\u63A5\u7EA6\u675F\u4F20\u64AD + \u5C3A\u5BF8\u611F\u77E5\u5019\u9009\u96C6\u7BA1\u7406\u51B2\u7A81\u3002\u6309\u57CE\u5E02\u98CE\u683C\u81EA\u52A8\u89C4\u5212\u529F\u80FD\u5206\u533A\u3002")
        ]
      }),
      new Paragraph({
        spacing: { before: 0, after: 40, line: 260 },
        children: [
          techLabel("\u7269\u7406 + AI: "),
          techBody("Rigidbody + 4\u4E2A WheelCollider \u5B9E\u73B0\u8F66\u8F86\u7269\u7406\uFF0C\u901A\u8FC7\u91CD\u5FC3\u504F\u79FB(-0.3m) + \u53CD\u4FA7\u503E\u6746(\u5F39\u7C27\u521A\u5EA65000) + \u89D2\u901F\u5EA6\u963B\u5C3C \u4E09\u5C42\u9632\u62A4\u89E3\u51B3\u9AD8\u901F\u4FA7\u7FFB\u3002\u4E27\u5C38 AI \u4F7F\u7528 NavMeshAgent \u52A8\u6001\u89C4\u5212\uFF0C\u542B\u89C6\u7EBF\u906E\u853D\u3001\u5206\u5E27\u611F\u77E5 (10\u5E27\u5206\u644A)\u3001\u5BFC\u822A\u5361\u4F4F\u68C0\u6D4B\u4E0E\u6062\u590D\u3002")
        ]
      }),
      new Paragraph({
        spacing: { before: 0, after: 40, line: 260 },
        children: [
          techLabel("\u6027\u80FD\u4F18\u5316: "),
          techBody("ChunkManager \u4E09\u7EA7\u533A\u5757 + \u5F02\u6B65\u9884\u70ED\u961F\u5217 + \u5BB9\u5668\u61D2\u52A0\u8F7D\uFF0C\u4E16\u754C\u52A0\u8F7D < 3\u79D2\u3002\u4F7F\u7528 Physics.OverlapSphereNonAlloc \u907F\u514D GC \u5206\u914D\uFF0C\u5730\u9762\u7269\u54C1\u6570\u91CF\u4E0A\u9650\u63A7\u5236\u3002")
        ]
      }),
      new Paragraph({
        spacing: { before: 0, after: 40, line: 260 },
        children: [
          techLabel("\u52A8\u753B & \u7C92\u5B50: "),
          techBody("Animator \u72B6\u6001\u673A\u9A71\u52A8\u89D2\u8272\u79FB\u52A8/\u6218\u6597\u8868\u73B0\uFF1BMuzzleFlashSystem \u4E8B\u4EF6\u9A71\u52A8\u751F\u6210/\u56DE\u6536\u4E09\u7EA7\u67AA\u53E3\u706B\u7130\u7C92\u5B50\uFF0C\u901A\u8FC7 Resources.Load \u52A8\u6001\u52A0\u8F7D Prefab\u3002")
        ]
      }),

      // --- R: 成果 ---
      subTitle("R \u2014 \u9879\u76EE\u6210\u679C"),
      bulletP("14+ \u6838\u5FC3\u7CFB\u7EDF\u5168\u90E8\u7A33\u5B9A\u8FD0\u884C\uFF0C\u5F62\u6210\u5B8C\u6574\u6E38\u620F\u5FAA\u73AF\u95ED\u73AF (\u63A2\u7D22\u2192\u6218\u6597\u2192\u641C\u522E\u2192\u5EFA\u9020\u2192\u751F\u4EA7\u2192\u751F\u5B58)"),
      bulletP("\u4E8B\u4EF6\u9A71\u52A8\u67B6\u6784\u5B9E\u73B0\u96F6\u8026\u5408\uFF0C\u65B0\u589E\u7CFB\u7EDF\u65E0\u9700\u4FEE\u6539\u73B0\u6709\u4EE3\u7801\uFF0C\u4EE3\u7801\u53EF\u7EF4\u62A4\u6027\u5F97\u5230\u9A8C\u8BC1"),
      bulletP("\u6DF1\u5165\u7406\u89E3\u4E86 SOLID\u3001\u4F9D\u8D56\u6CE8\u5165\u3001\u89C2\u5BDF\u8005\u6A21\u5F0F\u7B49\u8BBE\u8BA1\u539F\u5219\uFF0C\u638C\u63E1 WFC/A*/FSM/\u4E8B\u4EF6\u9A71\u52A8/\u7A7A\u95F4\u5206\u533A\u7B49\u6E38\u620F\u5F00\u53D1\u6838\u5FC3\u7B97\u6CD5"),
      bulletP("\u72EC\u7ACB\u5B8C\u6210\u4ECE\u67B6\u6784\u8BBE\u8BA1\u5230\u7F8E\u672F\u8D44\u6E90\u6574\u5408\u7684\u5B8C\u6574\u5F00\u53D1\u6D41\u7A0B\uFF0C 211\u4E2A\u811A\u672C\u5728\u56DB\u5C42\u67B6\u6784\u4E0B\u4FDD\u6301\u6E05\u6670\u8FB9\u754C\u548C\u4F9D\u8D56\u5173\u7CFB"),

      // --- 技能清单 ---
      sectionTitle("\u4E13\u4E1A\u6280\u80FD"),

      new Paragraph({
        spacing: { before: 60, after: 30, line: 280 },
        children: [
          techLabel("\u6E38\u620F\u5F15\u64CE: "),
          techBody("Unity 2022 LTS / \u56E2\u7ED3\u5F15\u64CE 1.8.5\uFF0C\u719F\u6089\u7269\u7406 (Rigidbody/WheelCollider)\u3001\u52A8\u753B (Animator)\u3001UI (uGUI/IMGUI)\u3001\u7C92\u5B50 (ParticleSystem)\u3001\u5BFB\u8DEF (NavMesh)\u3001ScriptableObject \u6570\u636E\u9A71\u52A8\u7B49\u6838\u5FC3\u6A21\u5757\uFF0C\u6B63\u5728\u5B66\u4E60 Shader (ShaderLab/HLSL) \u6E32\u67D3\u7F16\u7A0B\u3002")
        ]
      }),
      new Paragraph({
        spacing: { before: 0, after: 30, line: 280 },
        children: [
          techLabel("2D \u6E38\u620F: "),
          techBody("\u719F\u6089 2D \u6E38\u620F\u5F00\u53D1\u6D41\u7A0B (Tilemap / SpriteRenderer / Sprite Animation / 2D Physics / Cinemachine)\uFF0C\u5177\u5907 2D/3D \u53CC\u7EBF\u5F00\u53D1\u80FD\u529B\u3002")
        ]
      }),
      new Paragraph({
        spacing: { before: 0, after: 30, line: 280 },
        children: [
          techLabel("\u7F16\u7A0B\u8BED\u8A00: "),
          techBody("C# (\u719F\u7EC3)\u3001Lua (\u5B66\u4E60\u4E2D)\u3001\u6CDB\u578B\u3001\u59D4\u6258\u3001\u534F\u7A0B\u3001Linq\u3001OOP/SOLID\u3001\u8BBE\u8BA1\u6A21\u5F0F (\u5355\u4F8B/\u89C2\u5BDF\u8005/\u7B56\u7565/\u72B6\u6001\u673A)\u3002")
        ]
      }),
      new Paragraph({
        spacing: { before: 0, after: 30, line: 280 },
        children: [
          techLabel("\u5173\u5361/\u7B97\u6CD5: "),
          techBody("WFC \u7A0B\u5E8F\u5316\u5173\u5361\u751F\u6210\u3001A* \u5BFB\u8DEF\u3001FSM \u72B6\u6001\u673A\u3001\u4E8B\u4EF6\u9A71\u52A8\u67B6\u6784\u3001\u7A7A\u95F4\u5206\u533A\u3001LootTable \u6743\u91CD\u968F\u673A\u3001\u5C42\u7EA7\u5236\u914D\u65B9\u7CFB\u7EDF\u3002")
        ]
      }),
      new Paragraph({
        spacing: { before: 0, after: 30, line: 280 },
        children: [
          techLabel("\u5F00\u53D1\u5DE5\u5177: "),
          techBody("Git \u7248\u672C\u7BA1\u7406\u3001Editor \u81EA\u52A8\u5316 (MenuItem/AssetDatabase)\u3001Profiler \u6027\u80FD\u5206\u6790\u30013ds Max/Blender \u57FA\u7840\u5BFC\u5165\u3001VS Code/Rider\u3002")
        ]
      }),

      // --- 自我评价 ---
      sectionTitle("\u81EA\u6211\u8BC4\u4EF7"),
      bulletP("\u72EC\u7ACB\u4ECE\u96F6\u8BBE\u8BA1\u5E76\u5B9E\u73B0\u4E86\u4E00\u6B3E\u590D\u6742 3D\u6E38\u620F\uFF0C\u5177\u5907\u5B8C\u6574\u7684\u6E38\u620F\u5F00\u53D1\u95ED\u73AF\u80FD\u529B (\u67B6\u6784\u8BBE\u8BA1\u2192\u5173\u5361/\u73A9\u6CD5\u5B9E\u73B0\u2192\u52A8\u753B/\u7279\u6548\u2192\u6027\u80FD\u4F18\u5316\u2192\u5DE5\u5177\u94FE)\uFF0C\u80FD\u591F\u5FEB\u901F\u54CD\u5E94\u7B56\u5212\u9700\u6C42\u5E76\u72EC\u7ACB\u843D\u5730\u3002"),
      bulletP("\u5F3A\u70C8\u7684\u6280\u672F\u81EA\u9A71\u529B\uFF0C\u4E60\u60EF\u6DF1\u5165\u7406\u89E3\u5E95\u5C42\u673A\u5236 (\u5982 WheelCollider \u60AC\u67B6\u53C2\u6570\u8C03\u4F18\u3001WFC \u7EA6\u675F\u4F20\u64AD\u7B49)\uFF0C\u4E0D\u6EE1\u8DB3\u4E8E\u201C\u80FD\u8DD1\u201D\u800C\u662F\u8FFD\u6C42\u201C\u4E3A\u4EC0\u4E48\u201D\u3002"),
      bulletP("\u5177\u5907\u826F\u597D\u7684\u4EE3\u7801\u7EC4\u7EC7\u80FD\u529B\u4E0E\u89C4\u8303\u610F\u8BC6\uFF0C 211\u4E2A\u811A\u672C\u5728\u56DB\u5C42\u67B6\u6784\u4E0B\u4FDD\u6301\u6E05\u6670\u8FB9\u754C\uFF0C\u6BCF\u4E2A\u7CFB\u7EDF\u5747\u6709\u8BE6\u7EC6\u8BBE\u8BA1\u6587\u6863\u3002"),
      bulletP("\u70ED\u7231\u6E38\u620F\u5F00\u53D1\uFF0C\u5BF9\u5173\u5361\u8BBE\u8BA1\u548C\u73A9\u5BB6\u4F53\u9A8C\u6709\u654F\u9510\u7684\u6D1E\u5BDF\u529B\uFF0C\u80FD\u591F\u9002\u5E94\u5FEB\u8282\u594F\u5F00\u53D1\uFF0C\u4E50\u4E8E\u901A\u8FC7 Git \u548C\u6587\u6863\u4E0E\u56E2\u961F\u9AD8\u6548\u534F\u4F5C\u3002"),
    ]
  }]
});

// ============ 生成文件 ============
Packer.toBuffer(doc).then(buffer => {
  const outPath = "C:/Users/Administrator/Desktop/Unity学习一/mygame/Unity游戏开发实习简历_阿铁.docx";
  fs.writeFileSync(outPath, buffer);
  console.log("Resume generated: " + outPath);
});
