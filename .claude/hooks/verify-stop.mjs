#!/usr/bin/env node
/**
 * Claude Code Hook: Stop
 * Verifies repository integrity before finishing a turn:
 *  - Prevents re-entry loop when stop_hook_active is true
 *  - Validates .mcp.json and .claude/settings.json syntax
 *  - If a .NET solution (.sln/.slnx) exists and `dotnet` is installed, runs `dotnet build`
 *    and ensures zero compile errors and zero Native AOT / Trimming warnings (IL2xxx/IL3xxx).
 */

import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";

const projectDir = process.env.CLAUDE_PROJECT_DIR || process.cwd();

function readStdin() {
  try {
    return fs.readFileSync(0, "utf8");
  } catch {
    return "";
  }
}

const rawInput = readStdin();
try {
  const payload = rawInput ? JSON.parse(rawInput) : {};
  if (payload.stop_hook_active) {
    process.exit(0);
  }
} catch {
  // Ignore malformed stdin
}

const failures = [];

// 1. Validate critical JSON configs
for (const relJson of [".mcp.json", path.join(".claude", "settings.json")]) {
  const fullPath = path.join(projectDir, relJson);
  if (fs.existsSync(fullPath)) {
    try {
      JSON.parse(fs.readFileSync(fullPath, "utf8"));
    } catch (err) {
      failures.push(`Invalid JSON in ${relJson}: ${err.message}`);
    }
  }
}

// 2. Validate .NET build & AOT warning hygiene when solution exists and dotnet CLI is available
const entries = fs.readdirSync(projectDir);
const slnFile = entries.find((f) => f.endsWith(".sln") || f.endsWith(".slnx"));
if (slnFile) {
  const dotnetCheck = spawnSync("dotnet", ["--version"], { encoding: "utf8" });
  if (dotnetCheck.status === 0) {
    const build = spawnSync(
      "dotnet",
      ["build", path.join(projectDir, slnFile), "--nologo", "-v", "minimal"],
      { cwd: projectDir, encoding: "utf8", timeout: 50000 }
    );
    const output = `${build.stdout || ""}\n${build.stderr || ""}`;
    if (build.status !== 0) {
      failures.push(`dotnet build failed for ${slnFile}:\n${output.slice(-2000)}`);
    } else {
      const aotWarnings = output
        .split(/\r?\n/)
        .filter((line) => /\bwarning\s+IL[23]\d{3}\b/i.test(line));
      if (aotWarnings.length > 0) {
        failures.push(
          `Native AOT / Trimming warnings detected (${aotWarnings.length}):\n${aotWarnings.slice(0, 10).join("\n")}`
        );
      }
    }
  }
}

if (failures.length > 0) {
  process.stderr.write("MinePerform Stop Verification Failed:\n" + failures.join("\n\n") + "\n");
  process.exit(2);
}

process.exit(0);
