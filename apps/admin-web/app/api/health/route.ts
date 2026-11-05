import { API_BASE_URL, HEALTH_TIMEOUT_MS } from "@/lib/config";

/**
 * **`GET /api/health` — sức khoẻ của BFF *và* của Host (`WP-10`).**
 *
 * 🔑 **VÌ SAO PHẢI HỎI HOST CHỨ KHÔNG CHỈ TRẢ `200`:**
 * một endpoint health chỉ tự nói *"tôi còn chạy"* là **lời nói dối hữu ích nhất trong hệ thống** — Next vẫn
 * trả `200` khi API chết, khi DB chết, khi token không xin được. Load balancer/`docker compose`/người trực
 * ca đều dựa vào đúng endpoint này để quyết định *"hệ thống còn phục vụ được không"*.
 * ⇒ Trạng thái trả về = **trạng thái của Host** (`/health-status`), không phải của tiến trình Next.
 *
 * 🔒 **HỢP ĐỒNG (đo được, dùng ở `docker-compose.yml` healthcheck + `tools/kiem-ops-wp10.ps1`):**
 *
 * | Host | HTTP | body |
 * |---|---|---|
 * | sống (`/health-status` = 200) | **200** | `{"status":"Healthy","checks":{"host":"Healthy"}}` |
 * | trả lỗi (503 của chính nó, DB hỏng) | **503** | `{"status":"Unhealthy","checks":{"host":"Unhealthy"}}` |
 * | không kết nối được / treo quá `HEALTH_TIMEOUT_MS` | **503** | `{"status":"Unhealthy","checks":{"host":"Unreachable"}}` |
 *
 * 🚨 **KHÔNG lộ chi tiết** (`XC-005` BR-012, cùng luật với `/health-status` của Host): endpoint này
 * **`AllowAnonymous`** (phải gọi được trước khi đăng nhập) ⇒ không được trả tên bảng, chuỗi kết nối,
 * message exception hay stack. Chỉ **3 giá trị** ở trên. 📌 Ai đó thêm `error.message` vào đây là biến một
 * endpoint công khai thành bản đồ hạ tầng.
 */

/** `Unreachable` = không nói chuyện được với Host (khác `Unhealthy` = Host trả lời nhưng báo lỗi). */
type KetQuaHost = "Healthy" | "Unhealthy" | "Unreachable";

async function kiemHost(): Promise<KetQuaHost> {
  try {
    const res = await fetch(`${API_BASE_URL}/health-status`, {
      // 📌 `no-store`: health mà bị cache thì nó báo trạng thái của **lần trước** — đúng lúc cần nhất.
      cache: "no-store",
      // 📌 Có trần: Host **treo** (nhận kết nối, không trả lời) khác Host **chết** (refused ngay).
      signal: AbortSignal.timeout(HEALTH_TIMEOUT_MS),
    });

    // 📌 Host tự trả 503 khi `DatabaseHealthCheck` hỏng ⇒ `res.ok` mới là "sống", không phải "có trả lời".
    return res.ok ? "Healthy" : "Unhealthy";
  } catch {
    // 📌 Lỗi mạng/timeout **không** được ném ra ngoài: health phải trả lời, kể cả khi câu trả lời là "chết".
    return "Unreachable";
  }
}

// 📌 Route đọc trạng thái sống ⇒ tuyệt đối không được render tĩnh lúc build.
export const dynamic = "force-dynamic";

export async function GET() {
  const host = await kiemHost();
  const song = host === "Healthy";

  return Response.json(
    { status: song ? "Healthy" : "Unhealthy", checks: { host } },
    {
      status: song ? 200 : 503,
      headers: { "Cache-Control": "no-store" },
    },
  );
}
