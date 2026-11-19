# Handover `N28` — WP-14: phiên uỷ quyền `U-02` xong (hồ sơ HIỆU LỰC, `D-114` + `D-115`, 36 dòng `D-xx` ĐÃ CHỐT)

> Phiên `N28` · 19/11/2026 · nhánh `docs/n22-wp14-dot-a` · `main` = `db82e4d`. Hàng `U-02` của `docs/agents/bang-phien-wp14.md`, chủ dự án ngồi cùng (`/mo-phien u-02`).
> WP thuần tài liệu (`D-71`): 0 `.cs`, 0 migration; build/test bỏ qua có lý do "WP thuần tài liệu".

## 1. Đã xong — việc + số đo (kèm lệnh kiểm)

| Lệnh | Exit | Số đo |
|---|---|---|
| `kiem-link-tai-lieu.ps1` (mở phiên) | 0 | PASS — 0 tham chiếu chết |
| `npm --prefix tools run check:mermaid` (mở phiên) | 0 | 212 khối, 0 lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | 34/34 pass |
| `phien.ps1 check` | 0 | bảng khớp registry |
| `.cs` / migration đổi | — | **0** / **0** |

Việc làm (người trả lời bằng `AskUserQuestion`, 7 lượt, mọi câu chọn đúng khuyến nghị):

1. **M1–M6 + V1–V3:** M1 đồng ý · M2 có (trừ danh sách đỏ) · M3 có (`main` chỉ người) · M4 DEC tạm chốt + kế toán duyệt lô · M5 sandbox được, production cấm · M6 không bật credits · V1 công nhận Pilot-D · V2 danh sách mã · V3 cấm `dotnet ef database update`.
2. **R1:** 16 chính sách P1–P16 đều (a); P10 (`[AllowAnonymous]`) = vùng cấm, người chốt "không mở tới khi có rate limit + người duyệt".
3. **R2:** 30 câu §15 + 11 dòng `D-xx` + 24 dòng R0 đồng ý, **không ngoại lệ**.
4. **R3:** chủ dự án tự lo cả 8 việc (GHTK, sandbox GHN, kế toán, file COD, pháp chế, kênh 2, file ĐVHC, kho file production).
5. **Ghi:** `docs/agents/uy-quyen-chu-du-an.md` §1–§5 (**HIỆU LỰC** từ 19/11/2026) · `decisions.md` thêm `D-114`, `D-115`, đổi **36** dòng sang `ĐÃ CHỐT (người, phiên uỷ quyền 19/11/2026)` · `hang-doi-xem-lai.md` chuyển 36 dòng sang "Đã xử lý" (còn `D-101`).

## 2. Chưa xong / cắt giữa

- `P-03` (`SHP-001`, `DOC-OK` từ `N25`): hết bị chặn bởi `U-02` ⇒ `phien.ps1 start -Id P-03 -Session N25` rồi `done` (`-Evidence` = số đo `handover-n25.md` §1, `-Handover docs/nhanh/handover-n25.md`). Không làm lại.
- `P-04…P-07` (`SHP-002/004`, `SHP-003`, `REC-003`, `SAL-006`) còn ⬜. `SHP-003`, `REC-003`, `SAL-006` thuộc **danh sách đỏ** ⇒ docreview xong nhưng không được decider tạm duyệt.
- 3 câu §15 cũ (`ADDR-001-Q7`, `REP-001-Q2`, `REP-002-Q5`) + `PRO-002-Q3`, `REP-001-Q1`, `SHP-004-Q6`, `SHP-001-Q8`/`REP-002-Q6`: BA sửa thành Closed khi mở các tài liệu đó. 30 câu R2 BA áp ở các phiên `/doc-gate` sau.

## 3. Migration đã áp

Không có.

## 4. Quyết định mới

Do **người** chốt (`ĐÃ CHỐT (người, phiên uỷ quyền 19/11/2026)`): `D-114` (luật chơi + chính sách + R2 + R3) · `D-115` (bổ sung `D-71`, M2) · 36 dòng chuyển từ `TẠM CHỐT`/`ĐỀ XUẤT`: `D-72…D-92`, `D-95…D-100`, `D-102…D-104`, `D-108…D-113`. Còn `TẠM CHỐT`: `D-101` (chờ pháp chế, R3-5). **Không có `D-xx` do decider** trong phiên này.

## 5. Nợ / rủi ro

| # | Nợ | Chủ |
|---|---|---|
| 1 | Cổng máy "merge `main` khi biên bản chưa ký ⇒ đỏ" chưa có (`D-115` ⑤): phải dựng **trước** khi code dựa trên tạm duyệt chạm `main` | OPS (vai dựng ở B5) / chủ dự án |
| 2 | Nhánh `tich-hop/<giai-doan>` + branch protection + CI + cầu dao "CI đỏ ⇒ dừng" (M3) chưa có | chủ dự án tạo nhánh + protection; OPS dựng CI |
| 3 | Sandbox GHN/GHTK + secret (M5, R3-1/2): chưa có; chặn code adapter thật | chủ dự án |
| 4 | Release GĐ3 bị chặn tới khi kế toán điền Setting P1–P3 và chủ dự án duyệt P13 | chủ dự án |
| 5 | License `ClosedXML`/`MiniExcel`/`EPPlus` (`REC-003-Q5`) chưa tra web | người kiểm lại trước khi thêm gói |
| 6 | `SHP-001-Q7` bị cắt 300 ký tự trong tệp gom — phần "`IShipmentAppService` dùng chung một class" chưa xét | BA khi mở `SHP-001`/`SHP-002` |
| 7 | Ca `Extension_btree_gist_ton_tai` (`PostgreSqlSchemaTests.cs:92`) chưa chạy lại | OPS khi có Docker |
| 8 | Mẫu `de-xuat-uy-quyen…` §4.3 ghi "rẻ nhất để lùi = T8", đúng là **T6** | ORCH sửa khi chạm tệp đó |

**Chờ người:** không có câu mới. Việc của người đã ghi hồ sơ §5 (R3, hạn theo bảng) và nợ #2, #3.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: DỞ DANG WP-14 → mở lại WP-14** (`U-01`, `U-02` ✅; đợt A còn đóng `P-03`, rồi `P-04…P-07`).

1. Đóng `P-03` (nợ §2), rồi `P-04` (`/doc-gate SHP-002 SHP-004`).
2. Hàng duyệt đợt `P-09`/`P-13` đã chuyển cho decider tạm duyệt (`phien.ps1 uyquyen`); `P-19` giữ cho người.

```text
/chay-phien
```

## 7. Trạng thái nhánh

- `docs/n22-wp14-dot-a` @ `6f0a22e` + commit của phiên này (SHA ngắn lấy từ `git log -1`; đã push nếu push thành công). `main` = `db82e4d`. Chưa PR.

---

**Hàng rào agent** (chỉ lượt `N28`)

| Vai | Số lượt gọi | Ghi chú |
|---|---|---|
| ORCH | phiên hội thoại | hỏi người bằng `AskUserQuestion` (đúng ngoại lệ `thao-luan`) |
| `decider` / `ba` / `docrev` | 0 | — |
