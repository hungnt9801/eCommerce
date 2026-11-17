// SessionStart: nhac phien moi diem vao dung — handover MOI NHAT (khong phai tep nhan LICH SU),
// dong trang thai §6 cua no, so dong dang cho trong hang doi xem lai. Ngan, de khong ton ngu canh.

import { join } from 'node:path';
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { readStdinJson, projectRoot } from './lib.mjs';

try {
  const input = readStdinJson();
  if (input.agent_type) process.exit(0);
  const root = projectRoot(input);
  const nhanh = join(root, 'docs', 'nhanh');
  const parts = [];

  if (existsSync(nhanh)) {
    const ho = readdirSync(nhanh)
      .map((f) => ({ f, m: /^handover-n(\d+)([a-z]?)\.md$/i.exec(f) }))
      .filter((x) => x.m)
      .sort((a, b) => Number(b.m[1]) - Number(a.m[1]) || b.m[2].localeCompare(a.m[2]));
    if (ho.length) {
      const f = ho[0].f;
      const txt = readFileSync(join(nhanh, f), 'utf8');
      const s6 = txt.split(/^## 6\./m)[1] || '';
      const trangThai = (s6.split(/\r?\n/).find((l) => /Trạng thái phiên|XONG|DỞ DANG/i.test(l)) || '').trim();
      parts.push(`Handover moi nhat: docs/nhanh/${f}${trangThai ? ` — ${trangThai.slice(0, 200)}` : ''}.`);
      parts.push('Neu docs/nhanh/decisions.md co D-xx moi hon handover (vd D-71 mo WP-14) thi D-xx thang.');
    }
  }

  const hd = join(root, 'docs', 'agents', 'hang-doi-xem-lai.md');
  if (existsSync(hd)) {
    const t = readFileSync(hd, 'utf8');
    const cho = (t.split(/^## Đang chờ/m)[1] || '').split(/^## /m)[0];
    const n = cho.split(/\r?\n/).filter((l) => /^\|\s*D-\d+/.test(l)).length;
    if (n) parts.push(`Hang doi xem lai: ${n} quyet dinh tam cua decider dang cho nguoi duyet.`);
  }

  parts.push('Day chuyen agent: /mo-phien (baseline) · /doc-gate <MA…> (BA → DOCREV → decider) · /dong-phien (handover). Vung ghi: .claude/scopes.json.');

  process.stdout.write(JSON.stringify({
    hookSpecificOutput: { hookEventName: 'SessionStart', additionalContext: parts.join('\n') },
  }));
} catch (err) {
  process.stderr.write(`[session-context] ${err.message}\n`);
}
process.exit(0);
