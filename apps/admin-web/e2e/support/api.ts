/**
 * **SETUP bằng API THẬT** cho bộ E2E — không mock, không chèn DB (trừ 2 ngoại lệ đã ghi ở `psql.ts`).
 *
 * 🔑 **VÌ SAO SETUP KHÔNG ĐI QUA UI:** luồng E2E phải **đo** đúng một đường (ví dụ: *"bấm Lưu ⇒ hoá đơn có
 * thật"*). Nếu dựng dữ liệu nền cũng bằng UI thì khi một bước UI hỏng, **cả setup lẫn phép đo** cùng đỏ và
 * không biết hỏng ở đâu. ⇒ Dữ liệu nền (quầy, tồn kho, ca) tạo bằng API; **đường đi của người dùng** mới là
 * thứ được đo.
 *
 * 📌 Trong test, các lời gọi **của kịch bản** đi qua **BFF** (`/api/proxy/...` bằng `page.request`) để kiểm
 * luôn cookie + token server-side (`D-12`). Helper ở đây dùng **token trực tiếp** vì nó là *hạ tầng*, không
 * phải phép đo.
 */
import { API, OAUTH_CLIENT_ID, OAUTH_CLIENT_SECRET, OAUTH_SCOPE, ADMIN_EMAIL, ADMIN_PASSWORD } from "./env";

let tokenCache: string | null = null;

export async function layToken(): Promise<string> {
  if (tokenCache) return tokenCache;

  const res = await fetch(`${API}/connect/token`, {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({
      grant_type: "password",
      username: ADMIN_EMAIL,
      password: ADMIN_PASSWORD,
      client_id: OAUTH_CLIENT_ID,
      client_secret: OAUTH_CLIENT_SECRET,
      scope: OAUTH_SCOPE,
    }),
  });

  if (!res.ok) {
    throw new Error(
      `Không lấy được token (HTTP ${res.status}). 📌 Nếu DB vừa được migrate, cờ ` +
        `"buộc đổi mật khẩu" của admin còn bật — \`global-setup.ts\` tắt cờ đó; kiểm tra psql/docker.`,
    );
  }

  tokenCache = ((await res.json()) as { access_token: string }).access_token;
  return tokenCache;
}

async function goi<T>(
  method: string,
  duongDan: string,
  body?: unknown,
  headers: Record<string, string> = {},
): Promise<T> {
  const res = await fetch(`${API}${duongDan}`, {
    method,
    headers: {
      Authorization: `Bearer ${await layToken()}`,
      ...(body === undefined ? {} : { "Content-Type": "application/json" }),
      ...headers,
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  const raw = await res.text();

  if (!res.ok) {
    throw new Error(`SETUP ${method} ${duongDan} → HTTP ${res.status}: ${raw.slice(0, 500)}`);
  }

  return (raw.length === 0 ? null : JSON.parse(raw)) as T;
}

/** Chờ Host lên (globalSetup chạy **trước** khi test, và Playwright có thể chưa kịp bật `webServer`). */
export async function choApi(soGiay = 180): Promise<void> {
  const hetHan = Date.now() + soGiay * 1000;
  let loiCuoi = "";

  while (Date.now() < hetHan) {
    try {
      const res = await fetch(`${API}/health-status`, { signal: AbortSignal.timeout(3000) });
      if (res.ok) return;
      loiCuoi = `HTTP ${res.status}`;
    } catch (e) {
      loiCuoi = e instanceof Error ? e.message : String(e);
    }

    await new Promise((r) => setTimeout(r, 1000));
  }

  throw new Error(`Host ${API} không lên sau ${soGiay}s (${loiCuoi}). Xem log của Host.`);
}

export type KhoDto = { id: string; code: string; name: string };

/** `POST /api/app/warehouse` — quầy/kho. Mỗi lượt chạy một kho riêng ⇒ không đụng dữ liệu cũ. */
export function taoKho(code: string, name: string): Promise<KhoDto> {
  return goi<KhoDto>("POST", "/api/app/warehouse", { code, name });
}

/**
 * Nhập kho **thật** (tạo phiếu + ghi sổ) — nền cho luồng bán lẻ.
 * 📌 Số phiếu phải **khác nhau từng lần** (DB có unique theo số phiếu); `idempotencyKey` cũng vậy.
 */
export async function nhapKho(input: {
  warehouseId: string;
  sku: string;
  quantity: number;
  unitPrice: number;
  number: string;
}): Promise<{ id: string; number: string }> {
  const phieu = await goi<{ id: string; number: string }>("POST", "/api/v1/stock/bills", {
    number: input.number,
    type: 0,
    mode: 5,
    warehouseId: input.warehouseId,
    idempotencyKey: input.number,
    lines: [{ sku: input.sku, quantity: input.quantity, unitPrice: input.unitPrice }],
  });

  await goi<unknown>("POST", `/api/v1/stock/bills/${phieu.id}/post`);

  return phieu;
}

export type DongTonKho = { sku: string; warehouseId: string; onHand: number; available: number };

/** `GET /api/v1/stock/balances?sku=` — đọc số của **một** SKU (dùng để khẳng định, không phải nguồn UI). */
export async function tonKho(sku: string): Promise<DongTonKho | null> {
  const trang = await goi<{ items?: DongTonKho[] | null }>(
    "GET",
    `/api/v1/stock/balances?sku=${encodeURIComponent(sku)}`,
  );

  return (trang.items ?? [])[0] ?? null;
}

export type HoaDonDto = {
  id: string;
  number: string;
  totalPayment: number;
  lines?: Array<{ sku: string; quantity: number }> | null;
};

/** Tra hoá đơn theo **số** (số do UI hiện sau khi Lưu) — để lấy `id` mà không đoán URL. */
export async function timHoaDon(number: string): Promise<HoaDonDto | null> {
  const trang = await goi<{ items?: HoaDonDto[] | null }>(
    "GET",
    `/api/v1/pos/bills?q=${encodeURIComponent(number)}&maxResultCount=10`,
  );

  return (trang.items ?? []).find((x) => x.number === number) ?? null;
}

export type KetQuaTongHop = {  tuNgay: string;
  denNgay: string;
  soNgay: number;
  rong: boolean;
  soDongSuKien: number;
  soDongTongHop: number;
  soAnhTonKho: number;
};

/**
 * `POST /api/v1/reports/aggregate` — chạy tổng hợp **cho hôm nay** (backfill thắng mốc, `D-47` ⑤).
 * 📌 Bộ E2E **không** đợi job 15 phút: báo cáo chỉ có số sau một lượt tổng hợp, và luồng ⑧ cần số thật.
 */
export function tongHopBaoCao(tuNgay: string, denNgay: string): Promise<KetQuaTongHop> {
  return goi<KetQuaTongHop>("POST", "/api/v1/reports/aggregate", { tuNgay, denNgay });
}

export type CaDto = { id: string; number: string; openingCash: number; status: string };

/**
 * Ca **đang mở** của một quầy (`null` khi chưa mở — API trả `204`, đó là trạng thái **bình thường**,
 * không phải lỗi).
 */
export async function caHienTai(warehouseId: string): Promise<CaDto | null> {
  const res = await fetch(`${API}/api/v1/pos/sessions/current?warehouseId=${warehouseId}`, {
    headers: { Authorization: `Bearer ${await layToken()}` },
  });

  if (res.status === 204 || res.status === 404) return null;

  if (!res.ok) {
    throw new Error(`SETUP GET /pos/sessions/current → HTTP ${res.status}: ${(await res.text()).slice(0, 300)}`);
  }

  return (await res.json()) as CaDto;
}
