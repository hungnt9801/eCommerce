// Ca thu hang rao hook — chay: node --test .claude/hooks/test
// Khong goi model, khong cham repo that: moi ca dung mot repo git tam co ban sao .claude/scopes.json.
// Ca H1–H9 theo docs/agents/ke-hoach-v2-claude-code-pro.md §7.4; Exx la ca bo sung.

import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync, execFileSync } from 'node:child_process';
import { mkdtempSync, mkdirSync, writeFileSync, copyFileSync, rmSync, readFileSync, existsSync, appendFileSync, readdirSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { tmpdir } from 'node:os';
import { fileURLToPath } from 'node:url';

const HOOKS = join(dirname(fileURLToPath(import.meta.url)), '..');
const SCOPES = join(HOOKS, '..', 'scopes.json');
let repo;

function git(...args) {
  execFileSync('git', ['-C', repo, ...args], { stdio: 'ignore' });
}

function put(rel, text = 'x\n') {
  const p = join(repo, rel);
  mkdirSync(dirname(p), { recursive: true });
  writeFileSync(p, text, 'utf8');
  return p;
}

function run(hook, input, extraArgs = []) {
  const r = spawnSync(process.execPath, [join(HOOKS, hook), ...extraArgs], {
    input: JSON.stringify({ cwd: repo, ...input }),
    env: { ...process.env, CLAUDE_PROJECT_DIR: repo },
    encoding: 'utf8',
  });
  return { code: r.status, out: r.stdout, err: r.stderr };
}

const write = (role, file_path, tool = 'Write') =>
  run('guard-write-scope.mjs', { hook_event_name: 'PreToolUse', tool_name: tool, tool_input: { file_path }, ...(role ? { agent_type: role, agent_id: 'a1' } : {}) });
const shell = (role, command, tool = 'Bash') =>
  run('guard-shell.mjs', { hook_event_name: 'PreToolUse', tool_name: tool, tool_input: { command }, ...(role ? { agent_type: role, agent_id: 'a1' } : {}) });
const jsonBlock = (o) => 'Xong.\n```json\n' + JSON.stringify(o, null, 2) + '\n```';

before(() => {
  repo = mkdtempSync(join(tmpdir(), 'hook-test-'));
  execFileSync('git', ['init', '-q', repo]);
  git('config', 'user.email', 't@t');
  git('config', 'user.name', 't');
  mkdirSync(join(repo, '.claude'), { recursive: true });
  // Ca thu chan-dung can bat cong tac; ca N24-1 kiem mac dinh (tat).
  const sc = JSON.parse(readFileSync(SCOPES, 'utf8'));
  sc.chanDungKhiThieuJson = true;
  writeFileSync(join(repo, '.claude', 'scopes.json'), JSON.stringify(sc, null, 2));
  put('docs/features/address/addr-001-x.md', '# ADDR-001\n\n## 0. Tom tat\n\nnoi dung\n\n## 16. Trạng thái triển khai\n\nchua co\n');
  put('README.md');
  put('.gitignore', 'artifacts/\n'); // giong .gitignore that cua repo (lib.mjs cung tu bo qua artifacts/agents/)
  git('add', '-A');
  git('commit', '-q', '-m', 'init');
});

after(() => rmSync(repo, { recursive: true, force: true }));

// ── guard-write-scope ────────────────────────────────────────────────
test('H1 rev sua modules/** ⇒ chan', () => {
  const r = write('rev', 'modules/Sales/x.cs', 'Edit');
  assert.equal(r.code, 2);
  assert.match(r.err, /chi duoc ghi/);
});
test('H2 rev ghi docs/reviews/SHP-001-review.md ⇒ cho', () => assert.equal(write('rev', 'docs/reviews/SHP-001-review.md').code, 0));
test('H4 ba sua docs/features/README.md ⇒ chan', () => assert.equal(write('ba', 'docs/features/README.md', 'Edit').code, 2));
test('H5 vai la (Explore) ghi ⇒ chan fail-closed', () => {
  const r = write('Explore', 'docs/x.md');
  assert.equal(r.code, 2);
  assert.match(r.err, /fail-closed/);
});
test('H6 phien chinh khong co skill dieu phoi ⇒ cho', () => assert.equal(write(null, 'modules/x.cs', 'Edit').code, 0));
test('H7 be sua pnpm-lock.yaml ⇒ chan; fe ⇒ cho (ngoai le)', () => {
  assert.equal(write('be', 'pnpm-lock.yaml', 'Edit').code, 2);
  assert.equal(write('fe', 'pnpm-lock.yaml', 'Edit').code, 0);
});
test('E1 --orch: ORCH khong sua code, duoc ghi docs/agents; subagent bo qua hook ORCH', () => {
  const orch = (role, p) => run('guard-write-scope.mjs', { tool_input: { file_path: p }, ...(role ? { agent_type: role } : {}) }, ['--orch']);
  assert.equal(orch(null, 'modules/Sales/x.cs').code, 2);
  assert.equal(orch(null, 'docs/agents/bao-cao.md').code, 0);
  assert.equal(orch(null, 'docs/nhanh/handover-n22.md').code, 0);
  assert.equal(orch('ba', 'modules/Sales/x.cs').code, 0);
});
test('E2 duong dan tuyet doi trong repo duoc xet nhu tuong doi', () => {
  assert.equal(write('docrev', join(repo, 'docs', 'reviews', 'ADDR-001-docreview.md')).code, 0);
  assert.equal(write('docrev', join(repo, 'docs', 'reviews', 'ADDR-001-review.md')).code, 2);
});
test('E3 ngoai repo ⇒ chan; thu muc tam ⇒ cho', () => {
  assert.equal(write('ba', '/etc/hosts-khong-co').code, 2);
  assert.equal(write('ba', join(tmpdir(), 'nhap.md')).code, 0);
});
test('E4 ba: tai lieu feature ⇒ cho; *-arch.md ⇒ chan', () => {
  assert.equal(write('ba', 'docs/features/address/addr-001-x.md', 'Edit').code, 0);
  assert.equal(write('ba', 'docs/features/address/addr-001-arch.md').code, 2);
});
test('E5 decider: decisions.md ⇒ cho; CLAUDE.md, .claude/** ⇒ chan', () => {
  assert.equal(write('decider', 'docs/nhanh/decisions.md', 'Edit').code, 0);
  assert.equal(write('decider', 'CLAUDE.md', 'Edit').code, 2);
  assert.equal(write('decider', '.claude/skills/decision-policy/SKILL.md', 'Edit').code, 2);
});
test('E6 NotebookEdit dung notebook_path', () => {
  const r = run('guard-write-scope.mjs', { tool_name: 'NotebookEdit', tool_input: { notebook_path: 'modules/a.ipynb' }, agent_type: 'docrev' });
  assert.equal(r.code, 2);
});

// ── guard-shell ──────────────────────────────────────────────────────
test('H3 docrev chuyen huong ghi tep ⇒ chan', () => assert.equal(shell('docrev', 'echo x > docs/features/a.md').code, 2));
test('H8 docrev psql trong BEGIN…ROLLBACK ⇒ cho; khong ROLLBACK ⇒ chan; co COMMIT ⇒ chan', () => {
  assert.equal(shell('docrev', 'psql -h localhost -c "BEGIN; SELECT 1; ROLLBACK;"').code, 0);
  assert.equal(shell('docrev', 'psql -h localhost -c "UPDATE x SET y=1"').code, 2);
  assert.equal(shell('docrev', 'psql -c "BEGIN; UPDATE x SET y=1; COMMIT;"').code, 2);
  assert.equal(shell('ba', 'psql -c "BEGIN; SELECT 1; ROLLBACK;"').code, 2);
});
test('E7 shell doc: lenh doc + ong noi ⇒ cho', () => {
  assert.equal(shell('rev', 'git diff HEAD~1 -- modules | grep -n Money').code, 0);
  assert.equal(shell('ba', 'grep -n "a > b" docs/x.md 2>&1').code, 0);
  assert.equal(shell('docrev', 'pwsh -NoProfile -File "tools/kiem-link-tai-lieu.ps1"').code, 0);
  assert.equal(shell('docrev', 'pwsh -File .\\tools\\agents\\check-feature-doc.ps1 -Path docs/x.md', 'PowerShell').code, 0);
  assert.equal(shell('ba', 'Get-Content docs/x.md | Select-String "## 15"', 'PowerShell').code, 0);
});
test('E8 shell doc: lenh ghi / ngoai danh sach ⇒ chan', () => {
  assert.equal(shell('docrev', "Set-Content docs/x.md 'a'", 'PowerShell').code, 2);
  assert.equal(shell('ba', 'cat a > b').code, 2);
  assert.equal(shell('ba', 'dotnet test').code, 2);
  assert.equal(shell('ba', 'git branch -D wp/x').code, 2);
  assert.equal(shell('ba', 'find . -name "*.md" -delete').code, 2);
  assert.equal(shell('ba', 'node -e "require(\'fs\').writeFileSync(\'a\',\'b\')"').code, 2);
});
test('E9 read+test: rev chay test/build ⇒ cho', () => {
  assert.equal(shell('rev', 'dotnet test test\\eCommerce.Tests --no-build').code, 0);
  assert.equal(shell('rev', 'pnpm -C apps/admin-web typecheck').code, 0);
  assert.equal(shell('rev', 'pwsh -File .\\tools\\kiem-tat-ca.ps1').code, 0);
});
test('E9b cap do ghep: decider chay psql ROLLBACK + test, khong ghi', () => {
  assert.equal(shell('decider', 'psql -c "BEGIN; SELECT count(*) FROM x; ROLLBACK;"').code, 0);
  assert.equal(shell('decider', 'dotnet test test\\eCommerce.Tests --filter Money').code, 0);
  assert.equal(shell('decider', 'git commit -m x').code, 2);
  assert.equal(shell('decider', 'echo x >> docs/nhanh/decisions.md').code, 2);
});
test('E10 dev: be build duoc; git ghi, push, ef database update ⇒ chan', () => {
  assert.equal(shell('be', 'dotnet build eCommerce.slnx --no-incremental').code, 0);
  assert.equal(shell('be', 'git commit -m "x"').code, 2);
  assert.equal(shell('be', 'dotnet build && git push origin wp/x').code, 2);
  assert.equal(shell('be', 'dotnet ef database update').code, 2);
  assert.equal(shell('ops', 'git config --global user.name x').code, 2);
  assert.equal(shell(null, 'git push origin main').code, 0); // phien chinh: khong xet o day
});

// ── subagent-start / check-agent-stop / bao-orch ─────────────────────
function startStop(role, id, makeChanges, message, extra = {}) {
  run('subagent-start.mjs', { hook_event_name: 'SubagentStart', agent_type: role, agent_id: id });
  makeChanges();
  return run('check-agent-stop.mjs', { hook_event_name: 'SubagentStop', agent_type: role, agent_id: id, last_assistant_message: message, ...extra });
}

test('H9 docrev thieu JSON: lan 1 chan dung; lan 2 (stop_hook_active) cho dung + canh bao', () => {
  const r1 = startStop('docrev', 'd1', () => {}, 'Toi da xem xong.');
  assert.equal(r1.code, 0);
  assert.equal(JSON.parse(r1.out).decision, 'block');
  const r2 = run('check-agent-stop.mjs', { agent_type: 'docrev', agent_id: 'd1', last_assistant_message: 'Van khong co JSON', stop_hook_active: true });
  assert.equal(r2.out, '');
  const cb = readFileSync(join(repo, 'artifacts', 'agents', 'canh-bao.jsonl'), 'utf8');
  assert.match(cb, /"agentId":"d1"/);
});
test('E11 docrev du JSON + artifact co VERDICT ⇒ khong chan; artifact khong co VERDICT ⇒ chan', () => {
  const ok = jsonBlock({ agent: 'DOCREV', doc: 'docs/features/address/addr-001-x.md', verdict: 'DOC-OK', p1: 0, artifact: 'docs/reviews/ADDR-001-docreview.md' });
  const r = startStop('docrev', 'd2', () => put('docs/reviews/ADDR-001-docreview.md', '# Doc review\n\n**VERDICT: DOC-OK**\n'), ok);
  assert.equal(r.out, '');
  const r2 = startStop('docrev', 'd3', () => put('docs/reviews/ADDR-002-docreview.md', '# thieu\n'), ok.replace('ADDR-001-docreview', 'ADDR-002-docreview'));
  assert.equal(JSON.parse(r2.out).decision, 'block');
});
test('E12 luoi thu hai: docrev ghi modules/** qua shell ⇒ canh bao, bao-orch dua vao phien chinh mot lan', () => {
  const msg = jsonBlock({ agent: 'DOCREV', doc: 'a', verdict: 'DOC-OK', p1: 0, artifact: 'docs/reviews/ADDR-001-docreview.md' });
  startStop('docrev', 'd4', () => put('modules/Sales/len-lut.cs', 'class X {}'), msg);
  const b1 = run('bao-orch.mjs', { hook_event_name: 'PostToolUse', tool_name: 'Agent' });
  assert.match(JSON.parse(b1.out).hookSpecificOutput.additionalContext, /modules\/Sales\/len-lut\.cs/);
  const b2 = run('bao-orch.mjs', { hook_event_name: 'PostToolUse', tool_name: 'Agent' });
  assert.equal(b2.out, '');
  rmSync(join(repo, 'modules'), { recursive: true, force: true });
});
test('E13 be chi sua duoi heading "Trạng thái triển khai" ⇒ khong canh bao; sua phia tren ⇒ canh bao', () => {
  const msg = jsonBlock({ agent: 'BE', buildExit: 0, testExit: 0, docDeviation: [] });
  const doc = 'docs/features/address/addr-001-x.md';
  const goc = readFileSync(join(repo, doc), 'utf8');
  const truoc = readFileSync(join(repo, 'artifacts', 'agents', 'canh-bao.jsonl'), 'utf8').split('\n').length;
  startStop('be', 'b1', () => appendFileSync(join(repo, doc), '\n- WP-15: xong phan A\n'), msg);
  const giua = readFileSync(join(repo, 'artifacts', 'agents', 'canh-bao.jsonl'), 'utf8').split('\n').length;
  assert.equal(giua, truoc, 'sua duoi heading khong duoc sinh canh bao');
  writeFileSync(join(repo, doc), goc, 'utf8');
  startStop('be', 'b2', () => writeFileSync(join(repo, doc), goc.replace('noi dung', 'noi dung DA DOI'), 'utf8'), msg);
  const cb = readFileSync(join(repo, 'artifacts', 'agents', 'canh-bao.jsonl'), 'utf8');
  assert.match(cb, /TREN heading/);
  writeFileSync(join(repo, doc), goc, 'utf8');
});
test('E13b heading viet HOA "TIẾN ĐỘ TRIỂN KHAI" cung duoc nhan', () => {
  const doc = 'docs/features/address/addr-009-hoa.md';
  put(doc, '# X\n\n## 0. A\n\nnoi dung\n\n## 28. 💻 TIẾN ĐỘ TRIỂN KHAI — S1\n\ncu\n');
  git('add', doc);
  git('commit', '-q', '-m', 'doc hoa');
  const msg = jsonBlock({ agent: 'BE', buildExit: 0, testExit: 0, docDeviation: [] });
  const truoc = readFileSync(join(repo, 'artifacts', 'agents', 'canh-bao.jsonl'), 'utf8');
  startStop('be', 'b3', () => appendFileSync(join(repo, doc), '- WP-15 xong\n'), msg);
  assert.equal(readFileSync(join(repo, 'artifacts', 'agents', 'canh-bao.jsonl'), 'utf8'), truoc);
});
// ── Ca hoi quy tu phien N22 (handover-n22 §5 #1, #2) ────────────────
test('N22-1 JSON khong co trong tin cuoi nhung CO trong transcript ⇒ khong chan, nguonJson=transcript', () => {
  const tp = put('artifacts/transcripts/agent-t1.jsonl', [
    JSON.stringify({ type: 'user', message: { role: 'user', content: 'lam viec' } }),
    JSON.stringify({ type: 'assistant', message: { role: 'assistant', content: [{ type: 'text', text: jsonBlock({ agent: 'DEC', outcome: 'TAM_CHOT', choice: 'a' }) }] } }),
    JSON.stringify({ type: 'assistant', message: { role: 'assistant', content: [{ type: 'tool_use', id: 'x', name: 'Bash', input: {} }] } }),
    JSON.stringify({ type: 'assistant', message: { role: 'assistant', content: [{ type: 'text', text: 'Xong.' }] } }),
  ].join('\n') + '\n');
  const r = startStop('decider', 't1', () => {}, 'Xong.', { agent_transcript_path: tp });
  assert.equal(r.out, '', 'khong duoc chan dung khi transcript co JSON');
  const dir = join(repo, 'artifacts', 'agents');
  const moiNhat = readdirSync(dir).filter((f) => /^\d{4}-\d{2}-\d{2}\.jsonl$/.test(f)).sort().pop(); // ngay theo gio dia phuong cua hook
  const log = readFileSync(join(dir, moiNhat), 'utf8').trim().split('\n').map((l) => JSON.parse(l));
  assert.equal(log[log.length - 1].nguonJson, 'transcript');
});
test('N22-1b khoi ```json KHONG HOP LE ⇒ chan voi ly do "KHONG HOP LE" (khac voi "thieu")', () => {
  const r = startStop('decider', 't2', () => {}, 'Ket qua:\n```json\n{"agent": "DEC", "choice": "dung "DoiTen" tai cho"}\n```');
  assert.match(JSON.parse(r.out).reason, /KHONG HOP LE/);
});
test('N22-2 docrev: psql heredoc co BEGIN/ROLLBACK va dau > trong SQL ⇒ cho; ghi tep sau heredoc ⇒ chan', () => {
  const sql = "docker exec -i ecommerce-postgres psql -U postgres -d eCommerce <<'SQL'\nBEGIN;\nSELECT count(*) FROM x WHERE a > 0;\nROLLBACK;\nSQL";
  assert.equal(shell('docrev', sql).code, 0);
  assert.equal(shell('docrev', sql + '\necho x > docs/a.md').code, 2);
  assert.equal(shell('docrev', "psql <<'SQL'\nBEGIN;\nUPDATE x SET a=1;\nCOMMIT;\nSQL").code, 2);
});
test('N22-2b docrev: cac dang chay check:mermaid ⇒ cho', () => {
  assert.equal(shell('docrev', 'npm --prefix tools run check:mermaid').code, 0);
  assert.equal(shell('docrev', 'cd tools && npm run check:mermaid').code, 0);
  assert.equal(shell('docrev', 'npm --prefix ./tools run check:mermaid').code, 0);
  assert.equal(shell('docrev', 'npm install left-pad').code, 2);
});
// ── Ca hoi quy tu phien N23 (handover-n23 §5 #2: last_assistant_message RONG 12/12, transcript khong doc duoc) ──
test('N23-1 tin cuoi RONG + khong co transcript ⇒ KHONG chan, ghi khongXacMinh, khong sinh canh bao', () => {
  const cb = join(repo, 'artifacts', 'agents', 'canh-bao.jsonl');
  const truoc = existsSync(cb) ? readFileSync(cb, 'utf8') : '';
  const r = startStop('decider', 'n23a', () => {}, '', { agent_transcript_path: join(repo, 'khong-co.jsonl') });
  assert.equal(r.out, '', 'khong duoc chan khi khong thay noi dung vai');
  assert.equal(existsSync(cb) ? readFileSync(cb, 'utf8') : '', truoc, 'khong duoc sinh canh bao gia');
  const dir = join(repo, 'artifacts', 'agents');
  const moiNhat = readdirSync(dir).filter((f) => /^\d{4}-\d{2}-\d{2}\.jsonl$/.test(f)).sort().pop();
  const rec = readFileSync(join(dir, moiNhat), 'utf8').trim().split('\n').map((l) => JSON.parse(l)).pop();
  assert.equal(rec.khongXacMinh, true);
  assert.ok(Array.isArray(rec.khoaInput) && rec.khoaInput.includes('agent_type'), 'phai ghi ten cac truong input de chan doan');
});
test('N23-2 duong dan transcript dang "~/..." duoc mo rong theo thu muc home', () => {
  const home = mkdtempSync(join(tmpdir(), 'home-'));
  mkdirSync(join(home, 'tx'), { recursive: true });
  writeFileSync(join(home, 'tx', 'a.jsonl'), JSON.stringify({ type: 'assistant', message: { role: 'assistant', content: [{ type: 'text', text: jsonBlock({ agent: 'DEC', outcome: 'TIEN_LE', choice: 'D-08' }) }] } }) + '\n');
  run('subagent-start.mjs', { agent_type: 'decider', agent_id: 'n23b' });
  const r = spawnSync(process.execPath, [join(HOOKS, 'check-agent-stop.mjs')], {
    input: JSON.stringify({ cwd: repo, agent_type: 'decider', agent_id: 'n23b', last_assistant_message: '', agent_transcript_path: '~/tx/a.jsonl' }),
    env: { ...process.env, CLAUDE_PROJECT_DIR: repo, HOME: home, USERPROFILE: home },
    encoding: 'utf8',
  });
  assert.equal(r.stdout, '');
  const dir = join(repo, 'artifacts', 'agents');
  const moiNhat = readdirSync(dir).filter((f) => /^\d{4}-\d{2}-\d{2}\.jsonl$/.test(f)).sort().pop();
  const rec = readFileSync(join(dir, moiNhat), 'utf8').trim().split('\n').map((l) => JSON.parse(l)).pop();
  assert.equal(rec.nguonJson, 'transcript');
  rmSync(home, { recursive: true, force: true });
});
test('N23-3 transcript CO text nhung khong co JSON ⇒ van chan (co bang chung)', () => {
  const tp = put('artifacts/transcripts/agent-n23c.jsonl', JSON.stringify({ type: 'assistant', message: { role: 'assistant', content: [{ type: 'text', text: 'Toi da sua xong, khong co JSON.' }] } }) + '\n');
  const r = startStop('ba', 'n23c', () => {}, '', { agent_transcript_path: tp });
  assert.equal(JSON.parse(r.out).decision, 'block');
});
test('N24-1 mac dinh (scopes.json that: chanDungKhiThieuJson=false) ⇒ KHONG chan du thieu JSON, van ghi canh bao', () => {
  const that = JSON.parse(readFileSync(SCOPES, 'utf8'));
  assert.equal(that.chanDungKhiThieuJson, false, 'scopes.json cua repo phai tat chan dung (D-104)');
  const f = join(repo, '.claude', 'scopes.json');
  const cu = readFileSync(f, 'utf8');
  writeFileSync(f, JSON.stringify({ ...JSON.parse(cu), chanDungKhiThieuJson: false }, null, 2));
  try {
    const r = startStop('docrev', 'n24a', () => {}, 'Toi da xem xong, khong JSON.');
    assert.equal(r.out, '');
    assert.match(readFileSync(join(repo, 'artifacts', 'agents', 'canh-bao.jsonl'), 'utf8'), /"agentId":"n24a"/);
  } finally {
    writeFileSync(f, cu);
  }
});
test('E14 vai khong khai (general-purpose) ⇒ hook start/stop bo qua', () => {
  const r = startStop('general-purpose', 'g1', () => {}, 'xong');
  assert.equal(r.out, '');
  assert.equal(existsSync(join(repo, 'artifacts', 'agents', 'run', 'g1.start.json')), false);
});

// ── session-context ──────────────────────────────────────────────────
test('E15 session-context chi handover MOI NHAT + dong trang thai', () => {
  put('docs/nhanh/handover-n3.md', '# N3\n## 6. Viec dau tien\nTrạng thái phiên: XONG WP-02 → mở WP-03\n');
  put('docs/nhanh/handover-n21.md', '# N21\n## 6. Viec dau tien\nTrạng thái phiên: XONG WP-13 → mở WP-14\n');
  const r = run('session-context.mjs', { hook_event_name: 'SessionStart', source: 'startup' });
  const ctx = JSON.parse(r.out).hookSpecificOutput.additionalContext;
  assert.match(ctx, /handover-n21\.md/);
  assert.match(ctx, /mở WP-14/);
  assert.doesNotMatch(ctx, /handover-n3\.md/);
});

// F1 — frontmatter cua vai/skill phai la YAML hop le. Claude Code 2.1.328 (19/11/2026) BO QUA IM LANG tep sai:
// `description:` khong trich dan co ": " ⇒ "mapping values are not allowed here" ⇒ "Agent type 'ba' not found" (N29).
// Kiem toi thieu khong can thu vien YAML: dong khoa cap 1 co gia tri tron (khong ' " | > [ {) thi khong duoc chua ": " hay " #".
export function loiFrontmatter(text) {
  const t = text.replace(/\r\n/g, '\n');
  if (!t.startsWith('---\n')) return ['khong mo bang ---'];
  const end = t.indexOf('\n---', 4);
  if (end < 0) return ['khong dong bang ---'];
  const loi = [];
  for (const [i, dong] of t.slice(4, end).split('\n').entries()) {
    const m = /^([A-Za-z][\w-]*):[ \t]+(.*)$/.exec(dong);
    if (!m) continue;
    const v = m[2].trim();
    if (!v || /^['"|>\[{]/.test(v)) continue;
    if (/: /.test(v) || /:$/.test(v)) loi.push(`dong ${i + 2} khoa "${m[1]}": gia tri tron chua ": " (doi thanh " — " hoac trich dan)`);
    if (/\s#/.test(v)) loi.push(`dong ${i + 2} khoa "${m[1]}": gia tri tron chua " #" (YAML coi la chu thich)`);
  }
  return loi;
}

test('F1 frontmatter moi tep .claude/agents/*.md va .claude/skills/*/SKILL.md hop le', () => {
  const goc = join(HOOKS, '..');
  const tep = [];
  const ag = join(goc, 'agents');
  if (existsSync(ag)) for (const f of readdirSync(ag)) if (f.endsWith('.md')) tep.push(join(ag, f));
  const sk = join(goc, 'skills');
  if (existsSync(sk)) for (const d of readdirSync(sk)) if (existsSync(join(sk, d, 'SKILL.md'))) tep.push(join(sk, d, 'SKILL.md'));
  assert.ok(tep.length > 0, 'khong thay tep vai/skill nao');
  const hong = tep.map((f) => [f, loiFrontmatter(readFileSync(f, 'utf8'))]).filter(([, l]) => l.length);
  assert.deepEqual(hong.map(([f, l]) => `${f}: ${l.join('; ')}`), []);
});

test('F2 negative control: description co ": " bi bat, ban trich dan va CRLF thi qua', () => {
  assert.equal(loiFrontmatter('---\nname: ba\ndescription: Dung trong /doc-gate (WP-14: them muc)\n---\n# x\n').length, 1);
  assert.equal(loiFrontmatter('---\r\nname: ba\r\ndescription: "Dung (WP-14: them muc)"\r\n---\r\n').length, 0);
  assert.equal(loiFrontmatter('---\nname: ba\ndescription: Dung — them muc\nmodel: claude-sonnet-5-5\n---\n').length, 0);
});

test('E16 vai chi doc: lenh doc dang vong lap / sed -n / awk in / gan bien (bi chan nham o N35–N39) ⇒ cho', () => {
  const cho = [
    "sed -n '145p' docs/nhanh/decisions.md | cut -c1-3000; echo; sed -n '37p;140,186p' docs/nhanh/decisions.md",
    'for n in 8 18 27 31; do echo "== $n"; sed -n "${n}p" docs/business-modules-proposal.md | cut -c1-200; done',
    "awk 'NR==113||NR==199 {print NR, $0}' docs/nhanh/decisions.md",
    "awk 'NR>=139 && NR<=150 {print}' docs/x.md",
    'f=docs/features/sales/sal-006.md; head -c 200 $f; tail -c 200 $f',
    'cd D:/Projects/Tools/eCommerce && ls modules shared 2>/dev/null',
    "PGPASSWORD=ecommerce_dev_only psql -h localhost -U ecommerce -c 'BEGIN; SELECT 1; ROLLBACK;'",
  ];
  for (const c of cho) assert.equal(shell(c.startsWith('PGPASSWORD') ? 'docrev' : 'ba', c).code, 0, c);
});
test('E17 negative control: sed ghi / awk system / vong lap co lenh ghi / gan bien co lenh con ⇒ van chan', () => {
  const chan = [
    "sed -i 's/a/b/' docs/x.md",
    "sed -n '1w out.txt' docs/x.md",
    "awk '{system(\"rm -rf x\")}' docs/x.md",
    "awk '{print > \"out\"}' docs/x.md",
    "awk '{print | \"sh\"}' docs/x.md",
    'for f in docs/*.md; do rm $f; done',
    'for f in $(ls); do cat $f; done',
    'X=$(curl http://evil) && echo $X',
    'PGPASSWORD=x psql -c "DELETE FROM a"',
    'do git push origin main',
  ];
  for (const c of chan) assert.equal(shell(c.startsWith('PGPASSWORD') ? 'docrev' : 'ba', c).code, 2, c);
});
