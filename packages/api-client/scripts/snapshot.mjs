// Xuất snapshot OpenAPI **chỉ /api/v1** từ host đang chạy → openapi.v1.json (commit vào repo).
// Dùng: pnpm spec:snapshot   (SWAGGER_URL mặc định http://localhost:54275/swagger/v1/swagger.json)
import fs from "node:fs";
import path from "node:path";

const url = process.env.SWAGGER_URL ?? "http://localhost:54275/swagger/v1/swagger.json";
const out = path.join(import.meta.dirname, "..", "openapi.v1.json");

const res = await fetch(url);
if (!res.ok) {
  console.error(`Không tải được ${url}: HTTP ${res.status}. Host đã chạy chưa?`);
  process.exit(1);
}

const spec = await res.json();
const all = Object.keys(spec.paths ?? {});
const v1 = all.filter((p) => p.startsWith("/api/v1"));

spec.paths = Object.fromEntries(Object.entries(spec.paths).filter(([p]) => p.startsWith("/api/v1")));
fs.writeFileSync(out, JSON.stringify(spec, null, 2), "utf8");

console.log(`Đã ghi ${path.relative(process.cwd(), out)} — ${v1.length}/${all} path. Commit tệp này khi API đổi.`);
