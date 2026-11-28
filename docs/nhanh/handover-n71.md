# Handover N71 — WP-16 / hàng `W16-1` (F0–F2b) XONG

> Phiên `N71` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W16-1` · WP-16 · mã `PRC-002`
> · nhánh `feat/gd3-wp16-bang-gia` (cắt từ `origin/tich-hop/gd3` @ `e481094`) · 25/11/2026.
> Phạm vi phiên: **F0 → F2b** (đọc SRS, ARCH thiết kế, DOCREV, G1/G2 theo M2). Không code, không migration.

## 1. Đã xong

- **F0 — chuẩn bị:** nhánh cắt từ đỉnh `origin/tich-hop/gd3`; đọc SRS `prc-001`/`prc-002` theo mục
  (§0, §15, §16–§21 "còn lại", §20.3 guard tổng âm) + `07` §WP-15+ + `D-216`.
- **Cổng tài liệu đỏ oan → xanh (`D-217`):** `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-16` ban đầu **exit 1**
  ("PRC-002: KHONG thay dong trong bien ban tam duyet") vì probe `Code` chỉ đọc `docs/bien-ban-tam-duyet-gd3-*.md`,
  trong khi `PRC-002` đã được **người duyệt** ở `Q5` (`docs/bien-ban-duyet-gd1-2026-10-29.md:50`). DEC `D-217` chọn
  **tệp gương** `docs/bien-ban-tam-duyet-gd3-guong-q5-2026-11-25` (1 dòng `PRC-002`, cột `Kết luận DEC` bắt đầu
  `Tạm duyệt`, **không** cột `Người ký`, **không** `Đã duyệt`). Sau khi thêm: **exit 0**. Cổng `MergeMain` **vẫn ĐỎ**
  có chủ ý; nợ QA `D-217` ④ sửa probe đọc biên bản GĐ1 rồi xoá tệp gương.
- **F2 ARCH:** `docs/features/pricing/PRC-002-arch.md` (**466 dòng**) — vị trí `PricingSettingDefinitionProvider`
  (`Pricing.Domain`), 4 khoá Setting + mặc định + phạm vi `Application | Tenant`, `ChinhSachSnapshot` mở rộng additive,
  overload `TinhTienDon` (D-219), mã lỗi `0010`/`0011` append, **0 migration**, **không FE**, đường lùi từng hạng mục,
  9 ca test + 4 negative control, 4 tên index NND đầy đủ cho `phaiCo`.
- **F2b DOCREV:** vòng 1 `DOC-CHANGES` (**1 P1 · 3 P2 · 4 P3** — P1 là tự mâu thuẫn provider ↔ test "assembly không
  tham chiếu `Volo.Abp.Settings`"); ARCH sửa đủ 8 mục; vòng 2 **`DOC-OK` · 0 P1 · 0 P2**.
- **G2 (`D-220`):** decider tạm duyệt **thiết kế** `PRC-002-arch.md` cho W16-2 code trên nhánh, `reviewBefore = merge main`;
  loại trừ rõ 6 thứ (giá trị setting thật, migration/schema, đổi DTO/route/`schema.d.ts`, quyền mới, `AllowNegative`/`none`,
  quyết định tiền/VAT — M4 kế toán).
- **DEC `D-217`…`D-224`** (8 dòng `TẠM CHỐT (DEC)`), **0 câu `CHUYEN_NGUOI`**.
- **`P2 #4` của DOCREV — ORCH quyết:** `-arch.md` là tài liệu bổ trợ, **ngoài** mẫu 16 mục/BR/"Đối chiếu mô hình Nhanh"
  của `check-feature-doc -WP14` (công cụ áp cho SRS; `kiem-tat-ca.ps1` không gọi). Đã ghi thành blockquote đầu tài liệu.

| Lệnh | Exit | Số đo |
|---|---|---|
| `dotnet build eCommerce.slnx --no-incremental` (đầu phiên & đo lại cuối) | 0 | **0 error · 9 warning** (= baseline) · 112,97 s |
| `dotnet test test/eCommerce.Tests --no-build` | 0 | **1966/1966** (không đổi — phiên không chạm `.cs`) |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49 pass** |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | **PASS** · **235** tệp `.md` · **930** link · **1447** đường dẫn backtick · **0 chết** |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-16` | **0** | XANH (trước `D-217`: **exit 1**) |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/PRC-002-arch-docreview.md` | 0 | **DAT** · `verdict=DOC-OK` · P1=0 · P2=0 |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | 0 | **DAT** · **108** dòng |
| `docker compose ps` | — | `ecommerce-postgres` (5433) + `ecommerce-redis` (6380) **healthy** |

## 2. Chưa xong / cắt giữa

**Không có việc dở trong hàng `W16-1`.** WP-16 còn 4 hàng **khác** của chuỗi (không phải cắt giữa):

1. **`W16-2` (F3 BE) — làm ngay theo `D-218`/`D-219`/`D-220`:** code 4 hạng mục (Setting + snapshot; ngưỡng bậc
   `0011`; guard tổng âm `Reject` mặc định + `Clamp`; 4 tên index NND vào `phaiCo`); **BA sửa `prc-002` cùng PR**:
   §20.3 (D-219), §12 (mã "chồng khung" = `0013`, **không** `0012`), §9 (đúng 4 khoá của `D-218`), §6.1 (hoãn
   `IRoundingPolicySettingAppService` + permission — `D-222`).
2. **`W16-3` (F3 FE):** đóng với bằng chứng **"WP không có FE"** — `apps/admin-web` không có route Pricing/tier-price
   (grep = 0); không đổi DTO ⇒ `schema.d.ts` không đổi.
3. **`W16-4` (F4–F5):** 9 ca §6.3 + 4 negative control; **phép đo audit** bắt buộc của `D-222` (đường SettingManagement
   có để lại vết ai/khi nào/cũ-mới đủ `XC-001` BR-004 không; đạt ⇒ ghi bằng chứng, không đạt ⇒ nợ chặn release).
4. **`W16-5` (F6–G6):** `-DayDu` ⇒ đẩy fast-forward vào `tich-hop/gd3`.

## 3. Migration đã áp

- **Không có** — phiên 0 `.cs`, 0 migration, **không** chạm DB, **không** cần `pg_dump`.
- `PRC-002-arch.md` §4 chốt thiết kế **0 migration** (4 Setting là định nghĩa trong code; bảng `AbpSettings` đã có).

## 4. Quyết định mới

Tất cả **TẠM CHỐT (DEC, uỷ quyền `D-114`)**, đã có dòng trong `docs/agents/hang-doi-xem-lai.md`:

| `D-xx` | Nội dung |
|---|---|
| `D-217` | Cổng tài liệu đỏ oan với tài liệu đã duyệt `Q5` ⇒ **tệp gương** (không sửa probe trong WP-16); nợ **QA**: probe đọc `docs/bien-ban-duyet-gd1-*.md` cho cả `Code`/`MergeMain` + ca `-TuKiem` + ca ngược, **cùng commit xoá tệp gương**; hạn trước `W17-*` đầu tiên và trước PR `tich-hop/gd3` → `main` |
| `D-218` | Phạm vi "PRC-002 phần còn thiếu": **4 hạng mục VÀO** WP-16; **8 hạng mục RA** kèm chủ (B4→`PRO-*`; `CR-P1` ngoài WP-16, làm cùng WP-25; phân trang sổ; lọc `ChannelPrice`; giá vốn→WP-17; job lãi gộp→WP-32; menu `/GiaVon`→`WEB-001`; "giá shop vs bậc thang" giữ nguyên hành vi) |
| `D-219` | Guard tổng đơn âm: Setting `Pricing:OrderTotalNegativePolicy`, enum `Reject=0` (**mặc định, NÉM `0010`**)/`Clamp=1`; **không** mở `AllowNegative`; thay tripwire bằng **3 ca** (số ca tăng) |
| `D-220` | **G2** tạm duyệt thiết kế `PRC-002-arch.md`; 6 điểm **không** tạm duyệt (xem §1); `reviewBefore = merge main` |
| `D-221` | `RoundingPolicyCode` **không** cho `none`; giá trị lạ ⇒ mặc định + `hopLe=false` + log, **không ném** |
| `D-222` | **Hoãn** `IRoundingPolicySettingAppService` + permission; đường ghi = ABP SettingManagement; W16-4 **phải đo** dấu vết audit, không đạt ⇒ nợ chặn release (M4) |
| `D-223` | `Clamp`: mang `BiKep/SoKep` qua `KetQuaTinhTien` + log ở app service (diễn giải `D-219`, hàm thuần giữ nguyên) |
| `D-224` | `MaxTiersPerSet` = 0/âm/không phải số ⇒ dùng **20** + `hopLe=false`; không có công tắc tắt bằng 0 |

**Chờ người:** không có câu `CHUYEN_NGUOI` mới. Việc người còn lại là các mục **chặn release** đã có sẵn chủ:
kế toán duyệt lô M4 (`D-219`/`D-221`/`D-222`/`D-223`/`D-224`) · biên bản người ký trước merge `main` · bật required check (việc người).

## 5. Nợ / rủi ro

- **QA — `D-217` ④ (chặn trước `W17-*` đầu tiên và trước PR `tich-hop/gd3` → `main`):** sửa `tools/probe-cong-tai-lieu.ps1`
  đọc biên bản người GĐ1 (`docs/bien-ban-duyet-gd1-*.md`, có dòng `Hình thức: DUYỆT` + bảng cột `Mã`) làm nguồn ĐẠT cho
  **cả `Code` và `MergeMain`** + ca `-TuKiem` mới + **ca ngược** (mã không có trong biên bản nào ⇒ vẫn ĐỎ) + cập nhật `Y9`;
  **cùng commit xoá** `docs/bien-ban-tam-duyet-gd3-guong-q5-2026-11-25`. Nếu để quên: tệp gương tồn tại vĩnh viễn và
  cổng `MergeMain` của WP-16/17 không bao giờ xanh đúng nghĩa.
- **BA — cùng PR `W16-2`:** sửa `prc-002` §12/§9/§6.1/§20.3 theo `D-220` (nếu không, code theo SRS §9 sẽ dùng **sai khoá Setting**).
- **Release bị chặn tới khi kế toán duyệt lô M4** cho các `D-219`/`D-221`…`D-224` (đúng chính sách M4).
- **Hàng rào hook:** 2 cảnh báo **thiếu khối JSON cuối** (`arch` lần 1 và `docrev` lần 2) — cả hai **đã** trả JSON qua
  SubagentHandback; đây là giới hạn hook đã biết (`chanDungKhiThieuJson = false`). 0 lần ghi ngoài vùng, 0 chặn dừng.
- **Vận hành:** `decider` lần 1 chạm trần 40 lượt **sau khi đã ghi** `D-217`…`D-219` (không mất việc); `arch` lần 1 chạm
  trần 30 lượt **trước khi ghi tệp**, đã resume và hoàn tất. Không phải lỗi sản phẩm.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG hàng `W16-1` (WP-16 F0–F2b) → mở hàng `W16-2` (F3 BE, cùng WP-16).**

- Mở phiên bằng `/chay-phien-code chuoi=gd3-1` (bộ chạy tự chọn hàng kế `W16-2`).
- `W16-2` code **đúng 4 hạng mục** của `D-218`; BA sửa `prc-002` §12/§9/§6.1/§20.3 **cùng PR**; **0 migration**
  (nếu buộc có ⇒ dừng, hỏi decider — `D-220` không tạm duyệt schema).
- Nhắc: **không** xoá tệp gương trước khi QA sửa probe (`D-217`).

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp16-bang-gia`, base `origin/tich-hop/gd3` @ `e481094`.
- Commit chính: **`028d20d`** — 5 tệp, **+550** (`PRC-002-arch.md`, `PRC-002-arch-docreview.md`, tệp gương Q5,
  `decisions.md` +8 dòng, `hang-doi-xem-lai.md` +8 dòng). Commit tài liệu đóng phiên (handover này + `07` §13 +
  registry) đi **sau** commit chính trên cùng nhánh.
- Đã push `origin/feat/gd3-wp16-bang-gia` (không mở PR — `D-216` ③: PR xếp chồng mở cuối chuỗi).

---

**Hàng rào agent** (phiên `N71`): **3 vai / 6 lượt** (`decider` ×2, `arch` ×2, `docrev` ×2) · **0** lần chặn dừng ·
**0** tệp ghi ngoài vùng · **2** cảnh báo thiếu khối JSON (đã có JSON qua hand-back). ORCH tự chạy mọi cổng máy.
`/usage`: **chưa đo**.
