// Thu vien dung chung cho hook cua he agent eCommerce.
// Nguon luat: .claude/scopes.json. Ke hoach: docs/agents/ke-hoach-v2-claude-code-pro.md §7.
// Chi dung module co san cua Node (khong npm install) de hook chay ngay tren may moi.

import { readFileSync, existsSync, mkdirSync, appendFileSync, writeFileSync } from 'node:fs';
import { join, resolve, relative, isAbsolute, sep } from 'node:path';
import { tmpdir } from 'node:os';
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';

export const IS_WIN = process.platform === 'win32';

export function readStdinJson() {
  let raw = '';
  try {
    raw = readFileSync(0, 'utf8');
  } catch {
    raw = '';
  }
  if (!raw.trim()) return {};
  return JSON.parse(raw);
}

export function projectRoot(input) {
  return resolve(process.env.CLAUDE_PROJECT_DIR || input.cwd || process.cwd());
}

export function loadScopes(root) {
  const p = join(root, '.claude', 'scopes.json');
  return JSON.parse(readFileSync(p, 'utf8'));
}

// Glob -> RegExp. Ho tro: ** (moi cap), * (trong mot doan), ? (mot ky tu). Duong dan dung '/'.
export function globToRegex(glob) {
  let re = '';
  for (let i = 0; i < glob.length; i++) {
    const c = glob[i];
    if (c === '*') {
      if (glob[i + 1] === '*') {
        // "**/" khop 0..n thu muc; "**" o cuoi khop tat ca
        if (glob[i + 2] === '/') {
          re += '(?:.*/)?';
          i += 2;
        } else {
          re += '.*';
          i += 1;
        }
      } else {
        re += '[^/]*';
      }
    } else if (c === '?') {
      re += '[^/]';
    } else if ('\\^$+.()|{}[]'.includes(c)) {
      re += '\\' + c;
    } else {
      re += c;
    }
  }
  return new RegExp('^' + re + '$', IS_WIN ? 'i' : '');
}

export function matchAny(relPath, globs = []) {
  return globs.some((g) => globToRegex(g).test(relPath));
}

// Tra ve { rel, inside } — rel dung '/', inside = nam trong repo.
export function toRepoRelative(root, filePath) {
  const abs = isAbsolute(filePath) ? resolve(filePath) : resolve(root, filePath);
  let rel = relative(root, abs);
  const inside = rel !== '' && !rel.startsWith('..') && !isAbsolute(rel);
  rel = rel.split(sep).join('/');
  return { abs, rel, inside };
}

export function isUnder(abs, dir) {
  if (!dir) return false;
  const r = relative(resolve(dir), abs);
  return r === '' || (!r.startsWith('..') && !isAbsolute(r));
}

// Quyet dinh ghi cho mot vai. Tra ve null neu duoc phep, hoac chuoi ly do neu bi chan.
export function checkWrite(scopes, role, root, filePath, input = {}) {
  const { abs, rel, inside } = toRepoRelative(root, filePath);
  if (!inside) {
    if (isUnder(abs, tmpdir()) || isUnder(abs, input.scratchpad_dir)) return null;
    return `vai "${role}" chi duoc ghi trong repo (hoac thu muc tam). Duong dan bi chan: ${abs}`;
  }
  const r = scopes.roles[role];
  if (!r) return `vai "${role}" khong co trong .claude/scopes.json ⇒ khong duoc ghi (fail-closed)`;
  const exceptions = scopes.neverWriteExceptRoles || {};
  const neverHit = (scopes.neverWrite || []).find((g) => globToRegex(g).test(rel));
  if (neverHit) {
    const allowedRoles = exceptions[neverHit] || [];
    if (!allowedRoles.includes(role)) {
      return `"${rel}" nam trong neverWrite (${neverHit}) — chi nguoi (hoac vai ${allowedRoles.join('/') || 'khong vai nao'}) duoc sua`;
    }
    return null;
  }
  if (matchAny(rel, r.deny)) {
    return `vai "${role}" KHONG duoc ghi "${rel}" (deny trong .claude/scopes.json)`;
  }
  if (!matchAny(rel, r.write)) {
    return `vai "${role}" chi duoc ghi: ${r.write.join(', ')}. Bi chan: "${rel}". Neu can doi ngoai vung: ghi vao ket qua JSON de ORCH goi dung vai.`;
  }
  return null;
}

export function checkOrchWrite(scopes, root, filePath, input = {}) {
  const { abs, rel, inside } = toRepoRelative(root, filePath);
  if (!inside) {
    if (isUnder(abs, tmpdir()) || isUnder(abs, input.scratchpad_dir)) return null;
    return null; // ngoai repo: phien chinh do nguoi quan ly, khong chan
  }
  const w = (scopes.orch && scopes.orch.write) || [];
  if (matchAny(rel, w)) return null;
  return `ORCH dang chay day chuyen (/doc-gate hoac /feature): khong tu sua "${rel}". Goi dung vai (Agent) de sua; ORCH chi ghi: ${w.join(', ')}. Muon sua tu do ⇒ mo phien moi.`;
}

// Lay khoi JSON cuoi cung trong tin nhan cuoi cua subagent.
export function extractLastJson(text) {
  if (!text) return null;
  const fences = [...text.matchAll(/```json\s*([\s\S]*?)```/g)];
  for (let i = fences.length - 1; i >= 0; i--) {
    try {
      return JSON.parse(fences[i][1]);
    } catch {
      /* thu khoi truoc */
    }
  }
  const last = text.lastIndexOf('{');
  for (let start = last; start >= 0; start = text.lastIndexOf('{', start - 1)) {
    const candidate = text.slice(start);
    const end = candidate.lastIndexOf('}');
    if (end < 0) continue;
    try {
      return JSON.parse(candidate.slice(0, end + 1));
    } catch {
      /* lui tiep */
    }
    if (start === 0) break;
  }
  return null;
}

// Chan doan: co khoi ```json khong, parse loi gi (de phan biet "thieu" voi "khong hop le").
export function chanDoanJson(text) {
  const t = text || '';
  const fences = [...t.matchAll(/```json\s*([\s\S]*?)```/g)];
  let err = null;
  if (fences.length) {
    try {
      JSON.parse(fences[fences.length - 1][1]);
    } catch (e) {
      err = e.message;
    }
  }
  return { len: t.length, coKhoiJson: fences.length > 0, loiParse: err };
}

// Doc transcript JSONL cua subagent (agent_transcript_path), lay text cua cac tin assistant tu cuoi len,
// tim khoi JSON hop le gan nhat. Ly do (do o phien N22): last_assistant_message thuong KHONG chua khoi JSON
// ma vai da gui ve (31/34 luot bao thieu nham) — transcript la nguon day du.
export function jsonFromTranscript(p, toiDa = 12) {
  if (!p || !existsSync(p)) return { json: null, nguon: null };
  let lines;
  try {
    lines = readFileSync(p, 'utf8').split('\n').filter(Boolean);
  } catch {
    return { json: null, nguon: null };
  }
  let dem = 0;
  for (let i = lines.length - 1; i >= 0 && dem < toiDa; i--) {
    let o;
    try {
      o = JSON.parse(lines[i]);
    } catch {
      continue;
    }
    const msg = o && (o.message || o);
    const role = (o && o.type) || (msg && msg.role);
    if (role !== 'assistant') continue;
    dem++;
    const content = msg && msg.content;
    const text = typeof content === 'string'
      ? content
      : Array.isArray(content) ? content.filter((c) => c && c.type === 'text').map((c) => c.text).join('\n') : '';
    const j = extractLastJson(text);
    if (j) return { json: j, nguon: 'transcript' };
  }
  return { json: null, nguon: null };
}

export function stateDir(root) {
  const d = join(root, 'artifacts', 'agents');
  mkdirSync(join(d, 'run'), { recursive: true });
  return d;
}

export function today() {
  const d = new Date();
  const p = (n) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
}

export function appendJsonl(file, obj) {
  appendFileSync(file, JSON.stringify(obj) + '\n', 'utf8');
}

export function writeJson(file, obj) {
  writeFileSync(file, JSON.stringify(obj, null, 2), 'utf8');
}

export function readJsonIfExists(file, fallback) {
  if (!existsSync(file)) return fallback;
  try {
    return JSON.parse(readFileSync(file, 'utf8'));
  } catch {
    return fallback;
  }
}

// Anh chup cac tep "ban" (git status) kem hash noi dung — de biet subagent da doi tep nao.
export function dirtySnapshot(root) {
  let out = '';
  try {
    out = execFileSync('git', ['-C', root, 'status', '--porcelain=v1', '-z', '--untracked-files=all'], {
      encoding: 'utf8',
      maxBuffer: 64 * 1024 * 1024,
      stdio: ['ignore', 'pipe', 'ignore'],
    });
  } catch {
    return null; // khong co git / git loi (vd dubious ownership) ⇒ khong kiem duoc
  }
  const snap = {};
  const parts = out.split('\0').filter(Boolean);
  for (let i = 0; i < parts.length; i++) {
    const entry = parts[i];
    const code = entry.slice(0, 2);
    const path = entry.slice(3);
    if (code[0] === 'R' || code[0] === 'C') i++; // ban ghi doi ten co them duong dan cu
    snap[path] = hashFile(join(root, path));
  }
  return snap;
}

function hashFile(p) {
  try {
    return createHash('sha1').update(readFileSync(p)).digest('hex');
  } catch {
    return 'MISSING';
  }
}

export function changedSince(before, after) {
  const changed = [];
  const own = (p) => p.startsWith('artifacts/agents/'); // nhat ky cua chinh hook, khong tinh la vai ghi
  for (const [p, h] of Object.entries(after)) {
    if (!own(p) && before[p] !== h) changed.push(p);
  }
  for (const p of Object.keys(before)) {
    if (!own(p) && !(p in after)) changed.push(p); // da tro ve sach hoac bi xoa
  }
  return changed;
}

export function deny(reason) {
  process.stderr.write(`[guard] ${reason}\n`);
  process.exit(2);
}
