// PreToolUse (Bash|PowerShell): luat lenh shell theo vai — .claude/scopes.json "shell".
//   read               chi lenh doc (git diff/log/..., grep/rg, Get-Content, cong kiem tai lieu)
//   read+psql-rollback them psql/docker exec psql khi cau lenh co BEGIN va ROLLBACK, khong COMMIT
//   read+test          them dotnet build/test, pnpm lint/typecheck/test/build, cong tools/kiem-*.ps1
//   dev                moi lenh, tru danh sach cam chung
//   Cap do ghep duoc bang '+', vd decider: read+psql-rollback+test.
// Moi subagent: cam git ghi (add/commit/push/merge/reset/checkout/...), git config --global, gh pr merge,
// dotnet ef database update, xoa volume Docker. Git do ORCH (phien chinh) lam theo CLAUDE.md §8.
// Phien chinh (khong agent_type): khong xet — nguoi co mat; permissions.deny trong settings.json lo phan cung.
// Day la loc than trong, KHONG phai sandbox: luoi thu hai la check-agent-stop.mjs (so git status).

import { readStdinJson, projectRoot, loadScopes, deny } from './lib.mjs';

const CAM_CHUNG = [
  [/^git\s+(push|merge|rebase|reset|checkout|switch|restore|stash|clean|commit|add|rm|mv|tag|cherry-pick|revert|am|apply|worktree|update-ref|filter-branch)\b/i, 'git ghi do ORCH lam (CLAUDE.md §8), vai khong tu chay'],
  [/^git\s+branch\s+.*(-d|-D|-m|-M|--delete|--move)\b/i, 'xoa/doi ten nhanh do ORCH/nguoi lam'],
  [/^git\s+config\s+.*--global/i, 'khong doi cau hinh git global (CLAUDE.md §8)'],
  [/^gh\s+(pr\s+(merge|close)|repo\s+|api\s+.*-X\s*(POST|PUT|PATCH|DELETE)|release\s+)/i, 'thao tac GitHub ghi la vung cam'],
  [/^dotnet\s+ef\s+database\s+update\b/i, 'migration chay qua cong/DbMigrator, nguoi chay khi can (v2 Q15)'],
  [/^docker\s+(volume\s+rm|system\s+prune|compose\s+.*down\s+.*-v)/i, 'xoa du lieu Docker la vung cam (du lieu that)'],
  [/^(rm|Remove-Item|del|rmdir|rd)\b.*(\s|^)(\.git|\/|\\|[A-Za-z]:\\?)(\s|$)/i, 'xoa thu muc goc/.git bi cam'],
];

const DOC = [
  /^git\s+(diff|log|show|status|blame|grep|ls-files|rev-parse|merge-base|describe|cat-file|shortlog)\b/i,
  /^git\s+branch(\s+(--show-current|--list|-a|-r|-v|-vv|--contains\s+\S+))*\s*$/i,
  /^(rg|grep|egrep|fgrep|findstr|ls|dir|cat|type|head|tail|wc|sort|uniq|cut|tr|pwd|echo|cd|jq|diff|cmp|stat|file|basename|dirname|realpath|which|where)\b/i,
  /^find\b(?!.*\s-(delete|exec|execdir|ok|fprint)\b)/i,
  /^(Get-ChildItem|gci|Get-Content|gc|Select-String|sls|Test-Path|Resolve-Path|Measure-Object|Select-Object|Sort-Object|Where-Object|Get-Item|Format-Table|Format-List|Out-String|Out-Null|ConvertFrom-Json|ConvertTo-Json|Get-Location|Set-Location|Write-Output|Write-Host|Get-FileHash|Compare-Object|Group-Object|Split-Path|Join-Path)\b/i,
  /^(pwsh|powershell)(\.exe)?\s+(-NoProfile\s+|-NoLogo\s+|-ExecutionPolicy\s+\S+\s+)*-File\s+["']?\.?[\\/]?tools[\\/](kiem-link-tai-lieu\.ps1|agents[\\/]check-[\w-]+\.ps1)\b/i,
  /^npm\s+--prefix\s+["']?tools["']?\s+run\s+check:mermaid\b/i,
  /^(dotnet|pnpm|node|git|npm|pwsh|psql|docker)\s+--version\s*$/i,
  /^docker\s+(ps|compose\s+ps)\b/i,
];

const TEST = [
  /^dotnet\s+(build|test)\b/i,
  /^pnpm\s+((-C|--dir)\s+\S+\s+)?(lint|typecheck|test|build)\b/i,
  /^(pwsh|powershell)(\.exe)?\s+(-NoProfile\s+|-NoLogo\s+|-ExecutionPolicy\s+\S+\s+)*-File\s+["']?\.?[\\/]?tools[\\/](kiem-[\w-]+|test-cong-kiem)\.ps1\b/i,
];

const PSQL = /^(psql\b|docker\s+(compose\s+)?exec\b.*\bpsql\b)/i;

// Thay noi dung trong dau nhay bang khoang trang de khong bat nham ky tu dac biet trong chuoi.
export function boNhay(cmd) {
  return cmd.replace(/'[^']*'/g, (m) => "'" + ' '.repeat(m.length - 2) + "'").replace(/"(?:[^"\\]|\\.)*"/g, (m) => '"' + ' '.repeat(m.length - 2) + '"');
}

// Tach chuoi lenh theo && || ; | xuong dong — vi tri cat tinh tren ban da bo nhay (dau phan cach trong chuoi
// khong tinh), noi dung lay tu ban goc (de duong dan trong nhay van khop regex).
export function tachDoan(cmd) {
  const s = boNhay(cmd);
  const out = [];
  const re = /&&|\|\||;|\||\r?\n/g;
  let last = 0;
  let m;
  while ((m = re.exec(s))) {
    out.push(cmd.slice(last, m.index));
    last = m.index + m[0].length;
  }
  out.push(cmd.slice(last));
  return out.map((x) => ({ clean: x.trim() })).filter((x) => x.clean.length > 0);
}

// Chuyen huong ghi tep (> tep, >> tep) — bo qua 2>&1, >/dev/null, > $null, > NUL.
export function coGhiTep(cmd) {
  const s = boNhay(cmd);
  const re = /(\d|\*)?>>?\s*(\S*)/g;
  let m;
  while ((m = re.exec(s))) {
    const dich = m[2];
    if (dich.startsWith('&') || /^(\/dev\/null|\$null|nul)$/i.test(dich)) continue;
    if (s[m.index - 1] === '-' || s[m.index - 1] === '=') continue; // "->", "=>" trong tham so
    return true;
  }
  return false;
}

export function xetLenh(cmd, level) {
  const doan = tachDoan(cmd);
  for (const d of doan) {
    for (const [re, lyDo] of CAM_CHUNG) if (re.test(d.clean)) return `lenh bi cam: "${d.clean}" — ${lyDo}`;
  }
  if (level === 'dev') return null;

  if (coGhiTep(cmd)) return `vai chi doc (shell=${level}): khong duoc chuyen huong ghi tep (>, >>)`;
  const coPsql = level.includes('psql-rollback'); // cap do ghep duoc: read+psql-rollback+test
  const coTest = /(^|\+)test(\+|$)/.test(level);
  for (const d of doan) {
    if (DOC.some((re) => re.test(d.clean))) continue;
    if (coTest && TEST.some((re) => re.test(d.clean))) continue;
    if (coPsql && PSQL.test(d.clean)) {
      if (/\bBEGIN\b/i.test(cmd) && /\bROLLBACK\b/i.test(cmd) && !/\bCOMMIT\b/i.test(cmd)) continue;
      return 'psql chi duoc chay trong BEGIN … ROLLBACK (khong COMMIT) — tai lieu: D4 cua doc-review';
    }
    return `vai chi doc (shell=${level}): lenh "${d.clean.slice(0, 80)}" khong nam trong danh sach cho phep (.claude/hooks/guard-shell.mjs)`;
  }
  return null;
}

const isMain = import.meta.url === `file://${process.argv[1]}` || process.argv[1]?.endsWith('guard-shell.mjs');
if (isMain) {
  let input = {};
  try {
    input = readStdinJson();
    const role = input.agent_type;
    const cmd = (input.tool_input && input.tool_input.command) || '';
    if (!role || !cmd) process.exit(0);
    const scopes = loadScopes(projectRoot(input));
    const r = scopes.roles[role];
    if (!r) deny(`vai "${role}" khong co trong .claude/scopes.json ⇒ khong chay shell (fail-closed)`);
    const why = xetLenh(cmd, r.shell || 'read');
    if (why) deny(why);
    process.exit(0);
  } catch (err) {
    if (input && input.agent_type) deny(`guard-shell loi (${err.message}) ⇒ chan de an toan`);
    process.exit(0);
  }
}
