#!/usr/bin/env node
/**
 * Claude Code Hook: PostToolUse (Edit|Write)
 * Deterministically audits written/edited files against:
 *  1. .NET Native AOT & Zero Reflection rules (.cs, .csproj, .axaml)
 *  2. 8-Point Spatial Grid & Zero-Collision UI rules (.axaml, .kt)
 *  3. Zero Hardcoded IKU Formula enforcement (.cs, .kt)
 */

import fs from "node:fs";
import path from "node:path";

const ALLOWED_SPACING = new Set([0, 1, 2, 4, 6, 8, 12, 16, 20, 24, 28, 32, 36, 40, 48, 56, 64, 72, 80, 96]);

function readStdin() {
  try {
    return fs.readFileSync(0, "utf8");
  } catch {
    return "";
  }
}

function isTestOrSeedPath(filePath) {
  const norm = filePath.replace(/\\/g, "/").toLowerCase();
  return (
    norm.includes("/tests/") ||
    norm.includes(".tests/") ||
    norm.includes("/seeds/") ||
    norm.includes("/templates/") ||
    norm.endsWith("tests.cs") ||
    norm.endsWith("test.kt")
  );
}

function auditAxaml(content, relPath) {
  const issues = [];

  // 1. Native AOT compiled bindings check
  const hasBinding = /\{Binding\b/.test(content);
  const hasDataType = /\bx:DataType\s*=/.test(content);
  if (hasBinding && !hasDataType) {
    issues.push(
      `[AOT-AXAML] ${relPath}: Uses {Binding ...} without x:DataType. Native AOT requires compiled bindings (x:DataType="vm:...") to avoid reflection.`
    );
  }

  // 2. Zero-Collision Law: forbid <Canvas> for text/token layouts unless explicitly marked graphics-only
  if (/<Canvas\b(?![^>]*<!--\s*canvas-graphics-only\s*-->)/.test(content) && !content.includes("canvas-graphics-only")) {
    issues.push(
      `[UI-ZERO-COLLISION] ${relPath}: Found <Canvas>. Use WrapPanel, StackPanel, DockPanel, or Grid instead of fixed Canvas positioning.`
    );
  }

  // 3. 8-Point Spatial Grid check on Margin, Padding, Spacing, CornerRadius, ItemSpacing, ColumnSpacing, RowSpacing
  const spacingAttrRe = /\b(Margin|Padding|Spacing|CornerRadius|ItemSpacing|ColumnSpacing|RowSpacing)\s*=\s*"([^"]+)"/g;
  let match;
  while ((match = spacingAttrRe.exec(content)) !== null) {
    const [, attr, val] = match;
    if (val.includes("{")) continue; // Dynamic/StaticResource token is good
    const nums = val
      .split(/[,\s]+/)
      .map((s) => s.trim())
      .filter(Boolean);
    for (const nStr of nums) {
      const n = Number(nStr);
      if (!Number.isFinite(n)) continue;
      const absN = Math.abs(n);
      if (!ALLOWED_SPACING.has(absN)) {
        issues.push(
          `[UI-8PT-GRID] ${relPath}: ${attr}="${val}" contains off-grid value (${nStr}px). Use 8pt Spatial Grid tokens: 0, 4, 6, 8, 12, 16, 24, 32px.`
        );
        break;
      }
    }
  }

  return issues;
}

function auditCSharp(content, relPath) {
  const issues = [];
  const skipFormulaCheck = isTestOrSeedPath(relPath);

  if (/\bSystem\.Reflection\.Emit\b/.test(content)) {
    issues.push(`[AOT-CS] ${relPath}: System.Reflection.Emit is forbidden under PublishAot=true.`);
  }
  if (/\bActivator\.CreateInstance\s*\(/.test(content)) {
    issues.push(`[AOT-CS] ${relPath}: Activator.CreateInstance breaks Native AOT trimming. Use explicit factories or source generation.`);
  }
  if (/\bDllImport\s*\(/.test(content)) {
    issues.push(`[AOT-CS] ${relPath}: Prefer [LibraryImport] (source-generated P/Invoke) over legacy [DllImport] for Native AOT.`);
  }
  const codeOnly = content
    .split(/\r?\n/)
    .filter((l) => !l.trim().startsWith("//") && !l.trim().startsWith("*"))
    .join("\n");
  if (/(^|[^\w.])dynamic\s+[A-Za-z_]/.test(codeOnly)) {
    issues.push(`[AOT-CS] ${relPath}: C# 'dynamic' relies on runtime DLR reflection and is forbidden in Native AOT.`);
  }

  // Detect reflection-based System.Text.Json calls lacking JsonSerializerContext/JsonTypeInfo
  const jsonLines = content.split(/\r?\n/);
  jsonLines.forEach((line, idx) => {
    const trimmed = line.trim();
    if (trimmed.startsWith("//")) return;
    if (/JsonSerializer\.(Serialize|Deserialize)\s*[<(]/.test(trimmed)) {
      const hasContextArg = /Context\.Default\.|JsonTypeInfo|typeInfo/i.test(trimmed);
      if (!hasContextArg) {
        issues.push(
          `[AOT-JSON] ${relPath}:${idx + 1}: JsonSerializer call must pass a source-generated JsonTypeInfo (e.g. AppJsonContext.Default.MyType) for Native AOT.`
        );
      }
    }
  });

  // Zero Hardcoded Formula guard
  if (!skipFormulaCheck) {
    if (/case\s+"(1a-N|1b-N|2a-N)"\s*:|==\s*"(1a-N|1b-N|2a-N)"/.test(content)) {
      issues.push(
        `[ZERO-HARDCODE-FORMULA] ${relPath}: Detected branching on hardcoded IKU code (1a-N/1b-N/2a-N). Formulas MUST be evaluated dynamically via Lego-Token AST.`
      );
    }
  }

  return issues;
}

function auditCsproj(content, relPath) {
  const issues = [];
  const isExe = /<OutputType>\s*(WinExe|Exe)\s*<\/OutputType>/i.test(content);
  if (isExe && !/<PublishAot>\s*true\s*<\/PublishAot>/i.test(content)) {
    issues.push(`[AOT-CSPROJ] ${relPath}: Executable project must configure <PublishAot>true</PublishAot>.`);
  }
  return issues;
}

function auditKotlin(content, relPath) {
  const issues = [];
  const skipFormulaCheck = isTestOrSeedPath(relPath);

  // 8pt Spatial Grid for padding/spacedBy/RoundedCornerShape
  const dpRe = /\b(padding|spacedBy|RoundedCornerShape)\s*\(\s*(\d+)\.dp\b/g;
  let match;
  while ((match = dpRe.exec(content)) !== null) {
    const [, fn, numStr] = match;
    const n = Number(numStr);
    if (!ALLOWED_SPACING.has(n)) {
      issues.push(
        `[UI-8PT-GRID-KT] ${relPath}: ${fn}(${numStr}.dp) is off-grid. Use 4, 6, 8, 12, 16, 24, or 32.dp.`
      );
    }
  }

  if (!skipFormulaCheck && /"(1a-N|1b-N|2a-N)"\s*->|==\s*"(1a-N|1b-N|2a-N)"/.test(content)) {
    issues.push(
      `[ZERO-HARDCODE-FORMULA-KT] ${relPath}: Detected hardcoded IKU code branching in Kotlin. Evaluate dynamically via shared Lego-Token AST.`
    );
  }

  return issues;
}

const rawInput = readStdin();
let payload = {};
try {
  payload = rawInput ? JSON.parse(rawInput) : {};
} catch {
  process.exit(0);
}

const filePath = payload?.tool_input?.file_path || payload?.tool_input?.path || "";
if (!filePath || !fs.existsSync(filePath)) {
  process.exit(0);
}

const projectDir = process.env.CLAUDE_PROJECT_DIR || process.cwd();
const relPath = path.relative(projectDir, filePath) || filePath;
const ext = path.extname(filePath).toLowerCase();

let content = "";
try {
  content = fs.readFileSync(filePath, "utf8");
} catch {
  process.exit(0);
}

let issues = [];
if (ext === ".axaml" || ext === ".xaml") {
  issues = auditAxaml(content, relPath);
} else if (ext === ".cs") {
  issues = auditCSharp(content, relPath);
} else if (ext === ".csproj") {
  issues = auditCsproj(content, relPath);
} else if (ext === ".kt") {
  issues = auditKotlin(content, relPath);
}

if (issues.length > 0) {
  process.stderr.write(
    `MinePerform PostToolUse Audit detected ${issues.length} violation(s) in ${relPath}:\n` +
      issues.map((i) => `  - ${i}`).join("\n") +
      "\nFix these violations to stay compliant with CLAUDE.md and UI_UX_DESIGN_SYSTEM.md.\n"
  );
  process.exit(2);
}

process.exit(0);
