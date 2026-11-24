// PreToolUse (Grep|Read) trong VAI: chan doc code that khi phien CHUA co truy van do thi nao.
//
// Vi sao can hook (do 23-24/11, N56/N57): ba lop luat van ban — khoi ⛔ dau tep vai, cau dan trong prompt
// cua ORCH, buoc kiem trong skill /doc-gate — khong tao ra noi MOT loi goi MCP nao: 12 lan Grep code,
// 8 P2 ve as-built, 0 truy van. Luat chi bind khi bi chan that.
//
// Pham vi (fail-open co chu y):
//   - Chi subagent (input.agent_type). ORCH da bi guard-orch-mcp.mjs chan goi MCP ⇒ khong the thoa, nen mien.
//   - Chi vai CO khai mcp__codegraph__ / mcp__gitnexus__ trong `tools:` cua .claude/agents/<vai>.md.
//   - Chi duong dan code that: modules/, src/, shared/, test/, apps/.
//   - Chi chan khi transcript CUA PHIEN chua co tool_use nao la mcp__*.
//   - Toi da 2 lan chan moi phien, lan thu 3 cho qua (khong the treo phien neu model khong hop tac).
//
// Ca thu: .claude/hooks/test/hooks.test.mjs (nhom G).

import { projectRoot, toRepoRelative, deny, moRongHome } from './lib.mjs';
import { readFileSync, existsSync, statSync, writeFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';

// Doc stdin chiu duoc BOM: JSON.parse('\uFEFF{...}') nem loi, va hook se fail-open im lang.
function docJson() {
  try {
    const raw = readFileSync(0, 'utf8').replace(/^\uFEFF/, '').trim();
    return raw ? JSON.parse(raw) : {};
  } catch {
    return {};
  }
}

const CODE_DIRS = /^(modules|src|shared|test|apps)\//i;
const MCP_CALL = /mcp__(codegraph|gitnexus)__/;
const TOOL_USE = '"type":"tool_use"';
const MAX_DENY = 2;
const MAX_READ = 50 * 1024 * 1024; // transcript lon hon => bo qua (khong chan vi khong kiem duoc)

function agentDeclaresGraph(root, role) {
  const p = join(root, '.claude', 'agents', `${role}.md`);
  if (!existsSync(p)) return false;
  try {
    const t = readFileSync(p, 'utf8');
    const fm = t.split(/^---$/m)[1] || t; // chi xet frontmatter neu co
    return MCP_CALL.test(fm);
  } catch {
    return false;
  }
}

// Tim mot tool_use MCP trong transcript, quet TU CUOI LEN (loi goi gan nhat la quan trong nhat).
function transcriptHasGraphCall(p) {
  if (!p || !existsSync(p)) return false;
  try {
    if (statSync(p).size > MAX_READ) return false;
    const lines = readFileSync(p, 'utf8').split('\n');
    for (let i = lines.length - 1; i >= 0; i--) {
      const l = lines[i];
      if (l.length < 20) continue;
      if (l.includes(TOOL_USE) && MCP_CALL.test(l)) return true;
    }
  } catch {
    return false;
  }
  return false;
}

function markerDir(root) {
  const d = join(root, 'artifacts', 'agents', 'run');
  try {
    mkdirSync(d, { recursive: true });
  } catch {
    /* khong ghi duoc thi bo qua */
  }
  return d;
}

function demLanChan(root, key) {
  const f = join(markerDir(root), `code-lookup-${key}.txt`);
  let n = 0;
  try {
    n = parseInt(readFileSync(f, 'utf8'), 10) || 0;
  } catch {
    n = 0;
  }
  n += 1;
  try {
    writeFileSync(f, String(n), 'utf8');
  } catch {
    /* bo qua */
  }
  return n;
}

let input = {};
try {
  input = docJson();
} catch {
  process.exit(0);
}

const role = input.agent_type;
if (!role) process.exit(0); // phien chinh: khong xet

const tool = input.tool_name || '';
if (tool !== 'Grep' && tool !== 'Read') process.exit(0);

const ti = input.tool_input || {};
const target = ti.file_path || ti.path || ti.target || ti.directory || '';
if (!target || typeof target !== 'string') process.exit(0);

const root = projectRoot(input);
const { rel, inside } = toRepoRelative(root, target);
if (!inside || !CODE_DIRS.test(rel)) process.exit(0); // khong phai code that

if (!agentDeclaresGraph(root, role)) process.exit(0); // vai khong co cong cu ⇒ khong the thoa

const tep = input.agent_transcript_path || input.transcript_path;
if (transcriptHasGraphCall(moRongHome(tep))) process.exit(0); // da co truy van do thi

const key = String(input.session_id || input.agent_id || role).replace(/[^\w.-]/g, '_');
const lan = demLanChan(root, key);
if (lan > MAX_DENY) {
  process.stderr.write(`[guard] guard-code-lookup: da chan ${MAX_DENY} lan trong phien, bo qua de khong treo vai "${role}".\n`);
  process.exit(0);
}

deny(
  `Vai "${role}" doc code (${rel}) khi phien CHUA co truy van do thi nao. ` +
    `Goi mcp__codegraph__codegraph_explore (query = symbol/tep can tim) hoac mcp__gitnexus__context TRUOC, ` +
    `roi Grep/Read lai. (N56/N57: luat van ban 3 lop khong du - 12 Grep code, 0 truy van; lan chan ${lan}/${MAX_DENY}.)`
);
