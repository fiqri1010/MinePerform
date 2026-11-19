#!/usr/bin/env node
/**
 * Claude Code Hook: UserPromptSubmit
 * Injects MinePerform's core constitutional guardrails (CLAUDE.md & UI_UX_DESIGN_SYSTEM.md)
 * and checks for newly dropped reference files (Data IP 2026.xlsx, Manual IKU 2026.docx, PDFs).
 */

import fs from "node:fs";
import path from "node:path";

const projectDir = process.env.CLAUDE_PROJECT_DIR || process.cwd();

function readStdin() {
  try {
    return fs.readFileSync(0, "utf8");
  } catch {
    return "";
  }
}

function findReferenceDocs(rootDir) {
  const candidates = [
    "Data IP 2026.xlsx",
    "Manual IKU 2026.docx",
    path.join("docs", "references", "Data IP 2026.xlsx"),
    path.join("docs", "references", "Manual IKU 2026.docx"),
  ];
  const found = [];
  for (const rel of candidates) {
    if (fs.existsSync(path.join(rootDir, rel))) {
      found.push(rel);
    }
  }
  const refDir = path.join(rootDir, "docs", "references");
  if (fs.existsSync(refDir)) {
    for (const entry of fs.readdirSync(refDir)) {
      if (/\.(xlsx|xls|csv|docx|pdf)$/i.test(entry)) {
        const rel = path.join("docs", "references", entry);
        if (!found.includes(rel)) found.push(rel);
      }
    }
  }
  return found;
}

const rawInput = readStdin();
let promptText = "";
try {
  const parsed = rawInput ? JSON.parse(rawInput) : {};
  promptText = String(parsed.prompt || "");
} catch {
  // Ignore malformed stdin
}

const hasClaudeMd = fs.existsSync(path.join(projectDir, "CLAUDE.md"));
const hasDesignMd = fs.existsSync(path.join(projectDir, "UI_UX_DESIGN_SYSTEM.md"));
const refDocs = findReferenceDocs(projectDir);

const lines = [
  "[MinePerform Always-On Constitution Active]",
  `- Constitutions: CLAUDE.md=${hasClaudeMd ? "OK" : "MISSING"}, UI_UX_DESIGN_SYSTEM.md=${hasDesignMd ? "OK" : "MISSING"}`,
  "- Non-Negotiables: Native AOT (<25MB RAM, Zero Reflection, x:DataType, [LibraryImport]), Zero Hardcoded Formulas (100% Lego-Token AST), 8pt Grid (4/6/8/12/16/24/32px) + Zero-Collision UI (WrapPanel/FlowRow, 4 Super-Headers, 3 Frozen Columns), Defensive Sanitization ($- -> 0, Windows path guard, date anomaly radar), Hybrid Sync (Google Auth + Firebase Spark + OneDrive).",
];

if (refDocs.length > 0) {
  lines.push(`- Reference Docs Detected (parse via markitdown/MiniExcel): ${refDocs.join(", ")}`);
}

// Hint relevant auto-invoked skills based on prompt keywords
const hints = [];
if (/axaml|xaml|ui|ux|figma|penpot|semi\.avalonia|compose|layout|grid|wireframe/i.test(promptText)) {
  hints.push("sleek-ui-figma-architect");
}
if (/aot|memory|ram|csproj|reflection|source generator|libraryimport|performance/i.test(promptText)) {
  hints.push("aot-memory-guardian");
}
if (/sqlite|schema|tabel|table|kolom|column|sanitizer|cloner|linker|sql/i.test(promptText)) {
  hints.push("layman-sqlite-architect");
}
if (/iku|1a-n|1b-n|2a-n|formula|rumus|lego|token|ast|chart|summary|premi/i.test(promptText)) {
  hints.push("lego-token-ast-compiler");
}
if (/pib|lhp|b\/l|bl|lembar lanjutan|excel|xlsx|pdf|ocr|impor|import|data ip/i.test(promptText)) {
  hints.push("customs-doc-compiler");
}
if (/siap-neo|siap-desktop|bot-monitor|onedrive|macro|movefile|foto|photo|5gb|5 gb/i.test(promptText)) {
  hints.push("siap-bot-onedrive-bridge");
}
if (/firebase|google|oauth|sync|sinkron|android|kotlin|backup|cloud/i.test(promptText)) {
  hints.push("immortal-cloud-sync");
}
if (hints.length > 0) {
  lines.push(`- Auto-Matched Project Skills: ${hints.join(", ")}`);
}

process.stdout.write(lines.join("\n") + "\n");
process.exit(0);
