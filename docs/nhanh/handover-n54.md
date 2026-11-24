# Handover N54 — WP-14 · hàng P-12 (nháp biên bản đợt B đủ dòng + bảng câu chờ người)

> Phiên `N54` · WP-14 (thuần tài liệu, `D-71`) · nhánh `docs/n22-wp14-dot-a` · chạy bằng `/chay-phien` (bộ chạy tự động). Nối tiếp `N53`.
> Hàng `P-12` (`tong-hop`) **đạt DoD và đóng**: `docs/agents/nhap-bien-ban-gd3-b.md` đủ 3/3 dòng tài liệu đợt B, cột người review bắt buộc theo loại, bảng câu còn chờ người.

## 1. Đã xong — việc + số đo

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 0 tham chiếu chết (185 tệp · 846 link · 1108 đường dẫn backtick) |
| `npm --prefix tools run check:mermaid` | 0 | 211 khối · 0 lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | 40/40 pass |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | 0 | DAT — 53 dòng DEC (không có `D-xx` mới trong phiên) |
| `pwsh -File tools/agents/phien.ps1 check` | 0 | OK: bảng khớp registry |
| `git status --porcelain` | — | chỉ `docs/**` + `agents/state/**` (WP-14: 0 tệp `.cs`, 0 migration) |

- **Baseline đầu phiên (N54):** link PASS (0 chết) · mermaid 211/0 · hooks 40/40 · `PH check` OK. Build/test **bỏ qua có lý do** "WP thuần tài liệu" (`D-71`; build máy dev đỏ `MSB3491` — chủ `WP-15`).
- **Sửa `docs/agents/nhap-bien-ban-gd3-b.md`:**
  - Gộp bảng về **một** bảng liền mạch — bản N53 có **dòng trống** giữa `PRO-002` và `LOY-001` làm hàng `LOY-001` **rơi khỏi bảng** khi render; nay 3/3 dòng `PRO-001` · `PRO-002` · `LOY-001` trong cùng bảng.
  - Cột **"Người review bắt buộc"** lấy đúng `design-doc-guidelines.md` §6: cả 3 tài liệu có **state machine** ⇒ Kiến trúc trưởng + BA; cả 3 **chạm tiền/VAT** ⇒ thêm **kế toán** (M4). `PRO-002` trước chỉ có "Kiến trúc + kế toán" ⇒ bổ sung **BA** (state machine `Draft → Approved`).
  - Thêm **§3 "Bảng câu còn chờ người của đợt B"**: §3.1 = 13 câu §15 còn mở/`Deferred` (gồm `PRO-002` Q8 🔴 chặn nhánh code địa chỉ — GHTK chưa xác minh 2 cấp); §3.2 = **26** dòng `TẠM CHỐT` của đợt (`D-142…D-144`, `D-146…D-168`; **không** gồm `D-145` — quyết định hệ agent) kèm cột "Xem lại trước"; §3.3 = 6 việc người duyệt sửa **cùng WP code** (2 P2 + 2 P3 + CR chéo + CR POS).
  - §2 tóm tắt lại theo `N54`: 3/3 `DOC-OK`, **0 P1**; P2 mở: `PRO-001` 1 · `PRO-002` 0 · `LOY-001` 1.
- **Không lập biên bản thật** (`docs/bien-ban-duyet-gd3-b-<ngày>.md`) — việc của **chủ dự án** ở hàng `P-13` (`07` §WP-14 ④).

## 2. Chưa xong / cắt giữa

Không có — hàng `P-12` đạt DoD. Việc còn lại **không chặn DoD**, đã ghi trong nháp biên bản §3.3:

1. **P2** `PRO-001` #20: định nghĩa `MaxUsagePerCustomer = NULL` ở bước 7/BR-04/§5.2 dòng 385 — sửa **trước** khi code nhánh lượt theo khách.
2. **P2** `LOY-001` §11: danh sách unique index thiếu `LoyRedemptions(TenantId, RetailBillId)` và `(TenantId, OrderId)` — sửa **trước** `migrations add` Loyalty.
3. **2 P3** (`PRO-002` §15.4 "DoD 54 test case" ⇒ 55; `LOY-001` §7.1 "6 biên vào" ⇒ 7 hàng, trích `PosDtos.cs:33,35,119,122`).
4. **CR chéo bắt buộc** (chủ tài liệu/module khác, không sửa ở WP-14): `D-168` POS · `D-167` `SAL-006` BR-023 · G8 `Customers` `Task<KetQuaDaoKhach>` · `D-166` `ACC-001` · `CR-11` `PRO-002` · `CR-12` `SAL-004` · `CR-P1` `PRC-002` · `CR-S1` `SAL-002` · `CR-S2` `Sales`+kế toán.

## 3. Migration đã áp

Không có.

## 4. Quyết định mới

- **Không có `D-xx` mới** trong phiên này (không gọi `decider`; không câu hỏi mở nào phát sinh — hàng `tong-hop` thuần tổng hợp).
- **`TIEN_LE`:** không có mới.

## 5. Nợ / rủi ro

- **P2/P3 tài liệu** (mục §2 trên) — người duyệt sửa cùng WP code; cả 2 P2 đều có mốc "trước `migrations add`/trước code" nên không được quên.
- **Nợ chéo** (mục §2 dòng 4) — chưa có CR thì `LOY-001`/`PRO-001` **không** được release ở môi trường có POS/trả hàng.
- **`PRO-002` Q8** (địa chỉ/vùng giao) là cờ đỏ **duy nhất** còn treo trong 3 tài liệu: chỉ chặn **nhánh code điều kiện địa chỉ**, không chặn duyệt tài liệu; cần GHTK xác nhận 2 cấp (R3-1).
- **Chờ người:** bảng đầy đủ ở `docs/agents/nhap-bien-ban-gd3-b.md` §3 (13 câu §15 + 26 `TẠM CHỐT` + 6 việc cùng WP code). Không câu nào `CHUYEN_NGUOI` mới phát sinh trong phiên.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `P-12` → mở `P-13`.**

- `P-13` (`duyet-dec`, uỷ quyền `D-115`): lập `docs/bien-ban-duyet-gd3-b-<ngày>.md` từ `docs/agents/nhap-bien-ban-gd3-b.md` + review kiến trúc cho engine điều kiện; ghi **"TẠM DUYỆT (DEC, uỷ quyền D-xx)"**, không phải biên bản của người.
- Chạy `pwsh -File tools/agents/phien.ps1 next` để xác nhận hàng; mở phiên bằng `/chay-phien` (script tự cấp `N55`).

**Khối B — mở phiên mới:**

```text
/chay-phien
```

## 7. Trạng thái nhánh

- `docs/n22-wp14-dot-a`; commit đóng phiên N54 = `3a0e7d3` (**đã push**). `main` = `04b7712` tại đầu phiên. Chưa PR.

---

**Hàng rào agent** (phiên N54): **0** lượt gọi vai (`ba`/`docrev`/`decider` — hàng `tong-hop` do ORCH làm, phạm vi `docs/agents/**`). **0** lần chặn dừng. **0** lần ghi ngoài vùng (`git status` chỉ `docs/**` + `agents/state/**`). Không gọi MCP đồ thị (việc thuần câu chữ, không đối chiếu code).
