// SubagentStart: chup trang thai tep "ban" (git status + hash) truoc khi vai chay,
// de check-agent-stop.mjs biet CHINH vai nay da doi tep nao (ke ca ghi qua shell).

import { join } from 'node:path';
import { readStdinJson, projectRoot, loadScopes, stateDir, dirtySnapshot, writeJson } from './lib.mjs';

try {
  const input = readStdinJson();
  const role = input.agent_type;
  const id = input.agent_id;
  if (!role || !id) process.exit(0);
  const root = projectRoot(input);
  const scopes = loadScopes(root);
  if (!scopes.roles[role]) process.exit(0);
  const snap = dirtySnapshot(root);
  writeJson(join(stateDir(root), 'run', `${id}.start.json`), {
    role,
    startedAt: new Date().toISOString(),
    gitError: snap === null,
    snapshot: snap || {},
  });
} catch (err) {
  process.stderr.write(`[subagent-start] ${err.message}\n`);
}
process.exit(0);
