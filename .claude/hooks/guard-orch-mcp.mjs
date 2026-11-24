// PreToolUse (mcp__gitnexus__.*|mcp__codegraph__.*) — chi dang ky trong frontmatter skill /chay-phien va /doc-gate.
// Phien chinh (ORCH, khong agent_type) KHONG tu do code bang do thi: viec doi chieu code la cua vai `ba` (va `docrev` khi kiem cheo).
// Do o 22–23/11: ORCH goi codegraph_explore 19 lan + gitnexus 6 lan trong /chay-phien — ton han muc, lech vai (huong A, D-xx).
// Subagent (co agent_type) ⇒ cho qua; vai chi co cong cu nao duoc khai trong `tools:` cua no.
import { readStdinJson, deny } from './lib.mjs';

let input = {};
try {
  input = readStdinJson();
} catch {
  process.exit(0);
}
if (input.agent_type) process.exit(0);
const ten = input.tool_name || '(mcp)';
deny(`ORCH dang chay day chuyen: khong tu do code bang ${ten}. Giao viec doi chieu code cho vai \`ba\` (Agent subagent_type "ba", ghi ro ma/ky hieu can tra); ORCH chi dieu phoi + chay script cong.`);
