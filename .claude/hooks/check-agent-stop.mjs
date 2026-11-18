// SubagentStop: kiem ket qua cua vai truoc khi cho no dung.
//  1) Khoi JSON cuoi co du khoa bat buoc (scopes.json roles.<vai>.stop.jsonKeys); artifact khai trong JSON co that,
//     co chuoi bat buoc (vd "VERDICT:"). Thieu ⇒ CHAN DUNG MOT LAN, bao vai tu bo sung; lan 2 van thieu ⇒ cho dung,
//     ghi canh bao de ORCH xu ly theo tran 2 vong.
//  2) Luoi thu hai: so git status truoc/sau (anh chup cua subagent-start.mjs) ⇒ tep vai da doi ngoai vung
//     (ke ca ghi qua shell) ⇒ ghi canh bao. Hook KHONG tu git restore — ORCH khoi phuc dung tep.
//  3) Ghi mot dong nhat ky: artifacts/agents/<ngay>.jsonl (artifacts/ da gitignore).

import { join } from 'node:path';
import { existsSync, readFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import {
  readStdinJson, projectRoot, loadScopes, stateDir, dirtySnapshot, changedSince, checkWrite,
  extractLastJson, appendJsonl, readJsonIfExists, writeJson, today, toRepoRelative,
  chanDoanJson, jsonFromTranscript,
} from './lib.mjs';

function kiemMucTrienKhai(root, rel, headings) {
  // BE chi duoc sua tai lieu feature DUOI heading "Trang thai trien khai" / "Tien do trien khai" (khong phan biet hoa thuong).
  const abs = join(root, rel);
  if (!existsSync(abs)) return `khong thay ${rel}`;
  const ds = (Array.isArray(headings) ? headings : [headings]).map((h) => h.toLowerCase());
  const heading = ds.join(' | ');
  const lines = readFileSync(abs, 'utf8').split(/\r?\n/);
  const idx = lines.findIndex((l) => /^#{2,4}\s/.test(l) && ds.some((h) => l.toLowerCase().includes(h)));
  if (idx < 0) return `${rel}: khong co heading "${heading}" ⇒ BE khong duoc sua tai lieu nay (docDeviation ⇒ BA)`;
  let diff = '';
  try {
    diff = execFileSync('git', ['-C', root, 'diff', '-U0', 'HEAD', '--', rel], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] });
  } catch {
    return null; // tep moi chua co trong HEAD / git loi — de ORCH xem
  }
  for (const m of diff.matchAll(/^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@/gm)) {
    const start = Number(m[1]);
    if (start <= idx + 1) return `${rel}: sua o dong ${start}, TREN heading "${heading}" (dong ${idx + 1}) ⇒ lech hop dong phai qua BA`;
  }
  return null;
}

try {
  const input = readStdinJson();
  const role = input.agent_type;
  const id = input.agent_id || 'khong-id';
  if (!role) process.exit(0);
  const root = projectRoot(input);
  const scopes = loadScopes(root);
  const cfg = scopes.roles[role];
  if (!cfg) process.exit(0);

  const dir = stateDir(root);
  const runDir = join(dir, 'run');
  // Nguon 1: tin cuoi; nguon 2 (du phong): transcript cua chinh subagent.
  let json = extractLastJson(input.last_assistant_message || '');
  let nguonJson = json ? 'last_assistant_message' : null;
  let tDiag = null;
  if (!json) {
    const t = jsonFromTranscript(input.agent_transcript_path);
    json = t.json;
    nguonJson = t.nguon;
    tDiag = t.diag;
  }
  const chanDoan = chanDoanJson(input.last_assistant_message);
  // Chi CHAN khi THAY duoc noi dung vai ma khong co JSON. Khong doc duoc noi dung (tin cuoi rong + transcript
  // khong doc duoc / khong co text) => chi ghi nhat ky: cong that la script ORCH chay (check-feature-doc,
  // check-agent-artifact); chan khi khong chac chi ton luot (N22: 31/34, N23: 12/12 chan nham).
  const thayNoiDung = chanDoan.len > 0 || Boolean(tDiag && tDiag.coText);
  const stop = cfg.stop || {};
  const thieu = [];
  const khongXacMinh = !json && !thayNoiDung;

  if (!json && chanDoan.coKhoiJson && chanDoan.loiParse) {
    thieu.push(`khoi JSON KHONG HOP LE (${chanDoan.loiParse}) — sua dau nhay kep trong chuoi (\\"), bo dau phay thua`);
  } else if (!json && thayNoiDung) thieu.push('khoi JSON cuoi (```json ... ```) theo muc "Dinh dang tra ve" cua vai');
  else if (json) {
    for (const k of stop.jsonKeys || []) if (!(k in json)) thieu.push(`khoa JSON "${k}"`);
    const ak = stop.artifactKey;
    if (ak && typeof json[ak] === 'string' && json[ak]) {
      const { abs, inside } = toRepoRelative(root, json[ak]);
      if (!inside || !existsSync(abs)) thieu.push(`tep "${json[ak]}" (khai o khoa ${ak}) chua ton tai`);
      else if (stop.artifactMustContain && !readFileSync(abs, 'utf8').includes(stop.artifactMustContain)) {
        thieu.push(`tep "${json[ak]}" thieu dong "${stop.artifactMustContain}"`);
      }
    }
  }

  // Luoi thu hai: tep da doi trong luc vai chay
  const startFile = join(runDir, `${id}.start.json`);
  const start = readJsonIfExists(startFile, null);
  const sau = dirtySnapshot(root);
  let doi = [];
  let ngoaiVung = [];
  let ghiChuGit = null;
  if (!start || start.gitError || sau === null) {
    ghiChuGit = 'khong so duoc git status (thieu anh chup bat dau hoac git loi) ⇒ ORCH tu chay git status --porcelain';
  } else {
    doi = changedSince(start.snapshot, sau);
    for (const p of doi) {
      const why = checkWrite(scopes, role, root, p, input);
      if (why) ngoaiVung.push(p);
      else if (cfg.docSection && /^docs\/features\//.test(p)) {
        const lech = kiemMucTrienKhai(root, p, cfg.docSection);
        if (lech) ngoaiVung.push(`${p} (${lech})`);
      }
    }
  }

  const blocksFile = join(runDir, `${id}.blocks.json`);
  const blocks = readJsonIfExists(blocksFile, { n: 0 }).n;
  const record = {
    ts: new Date().toISOString(), agentId: id, role, json: json ? true : false, nguonJson,
    lam: { len: chanDoan.len, coKhoiJson: chanDoan.coKhoiJson, loiParse: chanDoan.loiParse, coTranscript: Boolean(input.agent_transcript_path) },
    transcript: tDiag, khongXacMinh, khoaInput: Object.keys(input).slice(0, 20),
    verdict: json && (json.verdict || json.outcome) || null, thieu, doi, ngoaiVung, ghiChuGit, lanChan: blocks,
  };
  appendJsonl(join(dir, `${today()}.jsonl`), record);
  if (thieu.length || ngoaiVung.length || ghiChuGit) {
    appendJsonl(join(dir, 'canh-bao.jsonl'), { ...record, daBao: false });
  }

  // Chi chan khi bat cong tac (scopes.json chanDungKhiThieuJson) — mac dinh tat tu D-104.
  if (scopes.chanDungKhiThieuJson === true && thieu.length && !input.stop_hook_active && blocks < 1) {
    writeJson(blocksFile, { n: blocks + 1 });
    process.stdout.write(JSON.stringify({
      decision: 'block',
      reason: `Chua du ket qua de dung. Bo sung ngay: ${thieu.join('; ')}. Ket thuc bang dung MOT khoi \`\`\`json theo muc "Dinh dang tra ve" cua vai.`,
    }));
  }
} catch (err) {
  process.stderr.write(`[check-agent-stop] ${err.message}\n`);
}
process.exit(0);
