// Sinh type TS từ OpenAPI **chỉ cho /api/v1**.
//
// Chế độ:
//   1) Mặc định: đọc **snapshot đã commit** `packages/api-client/openapi.v1.json`
//      ⇒ FE (và CI) **không cần** API host đang chạy — đây là điều kiện để 2 session chạy song song.
//   2) Làm mới snapshot: `pnpm spec:snapshot` (cần host ở 54275) rồi commit tệp JSON.
//   3) SWAGGER_URL=... pnpm gen  → đọc trực tiếp từ host (dùng khi đang phát triển API).
import fs from "node:fs";
import path from "node:path";
import openapiTS, { astToString } from "openapi-typescript";

const here = import.meta.dirname;
const snapshot = path.join(here, "..", "openapi.v1.json");
const out = path.join(here, "..", "src", "schema.d.ts");
const url = process.env.SWAGGER_URL;

async function loadSpec() {
  if (url) {
    const res = await fetch(url);
    if (!res.ok) {
      throw new Error(`Không tải được swagger tại ${url}: HTTP ${res.status}`);
    }
    return await res.json();
  }

  if (!fs.existsSync(snapshot)) {
    throw new Error(
      `Thiếu snapshot ${snapshot}. Chạy: pnpm --filter @ecommerce/api-client spec:snapshot (cần host 54275) ` +
        `hoặc SWAGGER_URL=http://localhost:54275/swagger/v1/swagger.json pnpm gen`,
    );
  }

  return JSON.parse(fs.readFileSync(snapshot, "utf8"));
}

const spec = await loadSpec();
const allPaths = Object.keys(spec.paths ?? {});
const v1Paths = allPaths.filter((p) => p.startsWith("/api/v1"));

if (v1Paths.length === 0) {
  throw new Error("Spec không có path /api/v1 nào — sai tệp?");
}

spec.paths = Object.fromEntries(Object.entries(spec.paths).filter(([p]) => p.startsWith("/api/v1")));

const ast = await openapiTS(spec);
fs.writeFileSync(out, astToString(ast), "utf8");

console.log(`Đã sinh ${path.relative(process.cwd(), out)} — ${v1Paths.length}/${allPaths.length} path (/api/v1).`);
