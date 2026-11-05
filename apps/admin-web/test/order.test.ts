import { describe, expect, it } from "vitest";
import {
  FULFILLMENT_LABEL,
  fulfillmentLabel,
  sortTransitions,
  toPage,
  toneOf,
  type FulfillmentStatus,
} from "../lib/order";

/**
 * **Hợp đồng dữ liệu của màn đơn hàng** (`WP-05` / `WP-09`).
 *
 * 🚨 Hai lỗi im lặng đã có tiền lệ trong repo, được chặn ở đây:
 * ① **quên `totalCount`** (ABP trả `{items,totalCount}`, kho trả `{items,total}`) ⇒ `total = 0` và màn hình
 *   hiện *"0 / 0"* dù có dữ liệu;
 * ② **sắp xếp nút chuyển trạng thái theo alphabet/insertion** ⇒ thu ngân thấy nút *"Đã hoàn"* trước
 *   *"Đã xác nhận"*; thứ tự phải theo **số enum** (đúng thứ tự vòng đời).
 */
describe("toPage — nhận CẢ HAI kiểu trang", () => {
  it("ABP (`totalCount`) và kho (`total`) cho cùng kết quả", () => {
    expect(toPage<{ id: string }>({ items: [{ id: "a" }], totalCount: 42 }).total).toBe(42);
    expect(toPage<{ id: string }>({ items: [{ id: "a" }], total: 42 }).total).toBe(42);
    expect(toPage<{ id: string }>(null)).toEqual({ items: [], total: 0, page: 1, pageSize: 0 });
  });
});

describe("nhãn & màu trạng thái", () => {
  it("18 trạng thái có nhãn; giá trị lạ KHÔNG bị đoán bừa", () => {
    expect(fulfillmentLabel(0)).toBe("Mới");
    expect(fulfillmentLabel(5)).toBe("Thành công");
    expect(fulfillmentLabel(17)).toBe("Xác nhận hoàn");
    expect(fulfillmentLabel(null)).toBe("—");

    // 📌 Số lạ (dữ liệu nhập từ nguồn khác) ⇒ hiện đúng số, không gán vào "Mới" (gán bừa = nói dối).
    expect(fulfillmentLabel(99)).toBe("99");
    expect(Object.keys(FULFILLMENT_LABEL)).toHaveLength(18);
  });

  it("màu suy từ trạng thái THỰC HIỆN, và đơn huỷ luôn đỏ", () => {
    expect(toneOf(5, false)).toBe("success");
    expect(toneOf(6, false)).toBe("danger");
    expect(toneOf(10, false)).toBe("info");
    expect(toneOf(0, false)).toBe("muted");
    // 🚨 huỷ thắng mọi trạng thái khác (một đơn vừa "Thành công" vừa `IsCancelled` phải hiện đỏ)
    expect(toneOf(5, true)).toBe("danger");
  });
});

describe("sortTransitions", () => {
  it("sắp theo SỐ ENUM (thứ tự vòng đời), không theo thứ tự server trả", () => {
    const tuServer: FulfillmentStatus[] = [12, 1, 9, 11];
    expect(sortTransitions(tuServer)).toEqual([1, 9, 11, 12]);
  });

  it("`null`/`undefined` ⇒ mảng rỗng (UI không sập khi đơn đã kết thúc)", () => {
    expect(sortTransitions(null)).toEqual([]);
    expect(sortTransitions(undefined)).toEqual([]);
  });
});
