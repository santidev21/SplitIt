#!/usr/bin/env node
// Cross-platform entrypoint for the SplitIt DB backup tooling.
// Used by the root npm scripts so `npm run db:backup` works on Windows and Linux.
// The real logic lives in db-backup.sh (Linux/VPS) and db-backup.ps1 (Windows).
//
// Usage:
//   node scripts/db-backup.mjs [backup|list|verify|restore] [file]
import { spawnSync } from 'node:child_process';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = dirname(fileURLToPath(import.meta.url));
const [action = 'backup', ...rest] = process.argv.slice(2);
if (!['backup', 'list', 'verify', 'restore'].includes(action)) {
  console.error(`Unknown action '${action}'. Use: backup | list | verify | restore.`);
  process.exit(1);
}

const isWindows = process.platform === 'win32';
let command;
let args;

if (isWindows) {
  command = 'powershell';
  args = ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', join(scriptDir, 'db-backup.ps1'), '-Action', action];
  if (rest[0]) args.push('-File', rest[0]);
} else {
  command = 'bash';
  args = [join(scriptDir, 'db-backup.sh'), action, ...rest];
}

const result = spawnSync(command, args, { stdio: 'inherit' });
process.exit(result.status ?? 1);
