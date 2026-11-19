// PostToolUse (Agent|Task): sau khi mot vai tra ket qua, dua CANH BAO chua bao vao ngu canh cua phien chinh (ORCH):
// vai ghi ngoai vung, thieu artifact sau lan chan, khong so duoc git status. ORCH xu ly theo skill /doc-gate.

import { join } from 'node:path';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { readStdinJson, projectRoot, stateDir } from './lib.mjs';

try {
  const input = readStdinJson();
  if (input.agent_type) process.exit(0); // chi bao cho phien chinh
  const root = projectRoot(input);
  const dir = stateDir(root);
  const file = join(dir, 'canh-bao.jsonl');
  if (!existsSync(file)) process.exit(0);
  const lines = readFileSync(file, 'utf8').split('\n').filter(Boolean);
  const offFile = join(dir, 'canh-bao.offset');
  const off = existsSync(offFile) ? Number(readFileSync(offFile, 'utf8')) || 0 : 0;
  const moi = lines.slice(off).map((l) => {
    try { return JSON.parse(l); } catch { return null; }
  }).filter(Boolean);
  writeFileSync(offFile, String(lines.length), 'utf8');
  if (!moi.length) process.exit(0);

  const dong = moi.map((r) => {
    const parts = [];
    if (r.ngoaiVung && r.ngoaiVung.length) parts.push(`GHI NGOAI VUNG: ${r.ngoaiVung.join(', ')} ⇒ git restore/xoa dung cac tep nay roi goi lai vai voi nhac pham vi`);
    if (r.thieu && r.thieu.length) parts.push(`thieu: ${r.thieu.join('; ')}`);
    if (r.ghiChuGit) parts.push(r.ghiChuGit);
    return `- vai ${r.role} (${r.agentId}): ${parts.join(' | ')}`;
  });
  process.stdout.write(JSON.stringify({
    hookSpecificOutput: {
      hookEventName: 'PostToolUse',
      additionalContext: `[hang rao agent] ${moi.length} canh bao moi (chi tiet: artifacts/agents/canh-bao.jsonl):\n${dong.join('\n')}`,
    },
  }));
} catch (err) {
  process.stderr.write(`[bao-orch] ${err.message}\n`);
}
process.exit(0);
