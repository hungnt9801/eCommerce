import { auth } from "./auth";
import { API_BASE_URL } from "./config";
import { docLoiApi } from "./api-error";

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    /**
     * Mã lỗi ổn định (`eCommerce:Inventory:0010`) nếu body là JSON lỗi của API.
     * 📌 Có mã thì UI mới hiển thị được **lý do** thay vì "403" — xem `lib/api-error.ts`.
     */
    public readonly code: string | null = null,
  ) {
    super(message);
  }
}

/** Đọc body lỗi (JSON hoặc text) thành `ApiError` — không ném lỗi parse thêm lần nữa. */
async function taoApiError(res: Response): Promise<ApiError> {
  const raw = await res.text();

  try {
    const failure = docLoiApi(JSON.parse(raw), res.status);
    return new ApiError(res.status, failure.message, failure.code);
  } catch {
    return new ApiError(res.status, raw || `HTTP ${res.status}`);
  }
}

/** Gọi API phía server (RSC / server action) — tự gắn Bearer từ session. */
export async function apiGet<T>(path: string): Promise<T> {
  const session = await auth();

  if (!session?.accessToken) {
    throw new ApiError(401, "Chưa đăng nhập.");
  }

  const res = await fetch(`${API_BASE_URL}${path}`, {
    headers: { Authorization: `Bearer ${session.accessToken}` },
    cache: "no-store",
  });

  if (!res.ok) {
    throw await taoApiError(res);
  }

  return (await res.json()) as T;
}

/** Như `apiGet` nhưng trả `fallback` thay vì ném — dùng cho dashboard (không được sập vì 1 ô). */
export async function apiGetSafe<T>(path: string, fallback: T): Promise<T> {
  try {
    return await apiGet<T>(path);
  } catch {
    return fallback;
  }
}

/** Hỏi server quyền nào đang được cấp trong danh sách — dùng để lọc menu (không đoán từ token). */
export async function getGrantedPermissions(names: string[]): Promise<Set<string>> {
  if (names.length === 0) {
    return new Set();
  }

  const query = encodeURIComponent(names.join(","));
  const result = await apiGetSafe<{ granted: string[] }>(`/api/v1/system/permissions?names=${query}`, { granted: [] });
  return new Set(result.granted);
}
