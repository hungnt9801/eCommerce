// PreToolUse (Edit|Write|MultiEdit|NotebookEdit): chan ghi ngoai vung cua vai TRUOC khi ghi.
// - Trong subagent: input co agent_type ⇒ tra .claude/scopes.json, sai vung ⇒ exit 2 (stderr = ly do).
// - Phien chinh (khong agent_type): cho qua; tru khi chay voi co --orch (skill /doc-gate dang ky) ⇒ khoa vung ORCH.
// - Vai la (khong khai trong scopes.json) ⇒ chan (fail-closed). Hook loi trong subagent ⇒ chan.
// Ke hoach: docs/agents/ke-hoach-v2-claude-code-pro.md §7.2. Ca thu: .claude/hooks/test/hooks.test.mjs

import { readStdinJson, projectRoot, loadScopes, checkWrite, checkOrchWrite, deny } from './lib.mjs';

const orchMode = process.argv.includes('--orch');
let input = {};

try {
  input = readStdinJson();
  const tool = input.tool_input || {};
  const filePath = tool.file_path || tool.notebook_path || tool.path;
  if (!filePath) process.exit(0);

  const root = projectRoot(input);
  const scopes = loadScopes(root);
  const role = input.agent_type;

  if (role) {
    if (orchMode) process.exit(0); // hook ORCH khong xet subagent; hook chinh da xet
    const why = checkWrite(scopes, role, root, filePath, input);
    if (why) deny(why);
    process.exit(0);
  }

  if (orchMode) {
    const why = checkOrchWrite(scopes, root, filePath, input);
    if (why) deny(why);
  }
  process.exit(0);
} catch (err) {
  if (input && input.agent_type) deny(`guard-write-scope loi (${err.message}) ⇒ chan de an toan`);
  process.stderr.write(`[guard] guard-write-scope loi, phien chinh duoc cho qua: ${err.message}\n`);
  process.exit(0);
}
