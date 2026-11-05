/**
 * Seed **đơn hàng** cho luồng ⑥ (đổi trạng thái) bằng SQL.
 *
 * 🚨 **VÌ SAO KHÔNG QUA API:** hiện **chưa có** endpoint tạo đơn dùng được — `POST
 * /api/app/don-hang/tao-tu-checkout` đòi **sổ giải trình giá** (`SAL-002`) + **lượt giữ chỗ** (`INV-002`);
 * đó là chuỗi của storefront/cổng thanh toán, không phải việc của `WP-09`. 📌 Cùng cách `tools/probe-don-hang.ps1`
 * đã làm (`.NOTES` của probe), và **đã ghi ở `handover-n10` §2/§5** kèm nhận chủ (`WP-05` P1 — màn *"tạo đơn"*).
 *
 * 📌 Bước chuyển được đo ở luồng ⑥ (`Mới → Chờ xác nhận`) **không** sinh bút toán kho (`D-39`: chỉ
 * `Confirmed`/`Returned` mới đụng sổ cái) ⇒ đơn seed **không** cần lượt giữ chỗ. Cố ý chọn bước không đụng
 * kho: luồng này đo **state machine + màn chi tiết**, còn tác động kho đã có probe `N6` đo riêng.
 */
import { psql } from "./psql";

export type DonSeed = { id: string; number: string };

export function gieoDonHang(sku: string, runId: string, soLuong = 1, donGia = 15000): DonSeed {
  const id = crypto.randomUUID();
  const number = `E2E-DH-${runId}`;

  // ⚠️ `OrdOrderLines` là `FullAuditedEntity` — **KHÔNG** có `ExtraProperties`/`ConcurrencyStamp`
  // (khác `OrdOrders`). Thêm hai cột đó ⇒ insert đỏ và đơn không có dòng (`Order:0011`).
  psql(`
insert into "OrdOrders" ("Id","TenantId","Number","ChannelCode","CustomerId","Status","PaymentStatus","Currency",
 "Subtotal","DiscountTotal","TaxTotal","ShippingTotal","GrandTotal","Note","ExtraProperties","ConcurrencyStamp",
 "CreationTime","IsDeleted","CheckoutIdempotencyKey","ClosureVersion","FulfillmentStatus","FulfillmentVersion",
 "IsCancelled","PaymentVersion","ReconciliationStatus","ReconciliationVersion","ReturnStatus","RowVersion",
 "Source","Type","WarehouseId","CodAmount","WeightGram")
values
 ('${id}',null,'${number}','web',null,0,0,'VND',${donGia * soLuong},0,0,0,${donGia * soLuong},
  'E2E WP-09 (seed SQL — xem handover-n10 §2)','{}','e2e-${runId}',now(),false,'e2e-${runId}',0,0,0,false,0,1,0,0,0,1,1,null,null,null);

insert into "OrdOrderLines" ("Id","TenantId","OrderId","Sku","Quantity","UnitPrice","DiscountAmount","TaxAmount",
 "ReturnedQuantity","CreationTime","IsDeleted")
values
 (gen_random_uuid(),null,'${id}','${sku}',${soLuong},${donGia},0,0,0,now(),false);
`);

  const soDong = Number(
    psql(`select count(*) from "OrdOrderLines" where "OrderId" = '${id}';`).split(/\r?\n/)[0] ?? "0",
  );

  if (soDong !== 1) {
    throw new Error(
      `Seed đơn hàng KHÔNG vào (đếm được ${soDong} dòng thay vì 1) — psql không báo lỗi nên rất dễ bỏ qua.`,
    );
  }

  return { id, number };
}
