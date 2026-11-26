# Handover N91 — WP-26 / hàng `W26-4` (F4 QA + F5 REV) XONG

> Phiên `N91` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W26-4` · WP-26 · mã `REP-001` · module `Reporting` · nhánh
> `feat/gd3-wp26-read-model` (base `origin/tich-hop/gd3`; HEAD trước phiên `1b90b5b`; HEAD code `57cf660`) · 27/11/2026.
> Phạm vi phiên: **F4 QA** (testplan + phủ BR + negative control) → **F5 REV** vòng 1 `CHANGES · P1=1` ⇒ `be` vá → `qa` +5 ca →
> REV vòng 2 **`PASS · P1=0`**. `main` không đụng; **chưa** vào `tich-hop/gd3` (thuộc `W26-5` khi G6 xanh).

## 1. Đã xong

- **F4 QA** — `qa` ×2:
  - Dựng harness test-only `test/eCommerce.Tests/Reporting/HaTang/HaTangBaoCao.cs` (khuôn `D-229`/`D-248`, repo giả in-memory +
    khoá/đồng hồ/tenant/user giả); `RepoBaoCaoGia` có `SoLanChen/SoLanSua/SoLanXoa/SoLanGoiXoa`.
  - Vòng 1: **+29 ca unit** (`RepVanHanhAppServiceTests` 13 · `RepBatBienTests` 16) + **+17 ca integration** PG thật
    (`PostgreSqlWp26ReadModelNndTests`: 8 index `NULLS NOT DISTINCT`, 8 cột `TenantId` nullable, 1 ca hai tenant).
  - Vòng 2 (sau vá P1): **+5 ca** — 3 ca cổng `0011` ở đường ghi (`ChayKhoangAsync`/`ChayAsync` ném `0011`, **0 lời gọi
    xoá / 0 dòng chèn**, ca đối chứng kỳ **không** phủ ngày vẫn chạy), 1 ca `StartRebuild` active-run ⇒ `AlreadyRequested`
    không chèn, 1 ca đối chiếu `TonKho` phát `MismatchKind.AnhTonKho`(3) với `Delta = read-model − nguồn`.
  - `docs/tests/REP-001-testplan.md`: **14/15 BR** phủ; **BR-008/UoW** ghi trung thực **"chưa kiểm được"** (thiếu harness
    `DoWorkAsync` thật — nợ `N90` §6), cùng NFR định lượng + API 401/403 runtime.
- **F5 REV vòng 1** — `CHANGES · P1=1, P2=2, P3=3`: cổng `0011` "ghi vào kỳ `DaChot`" **chỉ** kiểm lúc nhận yêu cầu
  (`StartRebuildAsync`), **không** kiểm ở đường ghi thật ⇒ rebuild/aggregate/resume có thể ghi đè số liệu **đã chốt** im lặng.
- **Vá P1 (be)** — cổng tập trung `TongHopBaoCaoService.ChanNeuGiaoKyDaChotAsync(ngay)` (dependency `IRepository<RepPeriodClose, Guid>`
  **bắt buộc**, **fail-closed**), kiểm `Status == DaChot && FromDate <= ngay && ToDate >= ngay` **trước `DELETE`** ở **3** đường ghi:
  `ChayAsync`, `ChayKhoangAsync`, `ChupAnhTonKhoAsync`; resume đi qua `ChayKhoangAsync`/`ChupTonKhoAsync` nên được phủ. Ném `0011`
  ⇒ UoW ngày rollback ⇒ **0 dòng thay đổi**, run `PartiallySucceeded`/`Failed` có `LastErrorCode`.
  Kèm **P2 #1** (pre-check `Queued/Running` cùng `(Rebuild, projection)` ⇒ `AlreadyRequested`, không còn `23505`→500),
  **P2 #2** (đối chiếu `TonKho` phát đúng `MismatchKind.AnhTonKho`), **P3 #5** (`ProducesResponseType` 202→200 khớp runtime).
- **F5 REV vòng 2** — **`PASS · P1=0 · P2=0 · P3=4`** (R1 TOCTOU hẹp, R2 đua double-submit→500, R3 nghĩa `AlreadyRequested`,
  + nợ `br-002` có chủ `ba`).
- **Negative control**: NC-1/2/3 (ORCH chạy lại) + NC-4 (ORCH chạy trên chính cổng `TongHopBaoCaoService.cs:467`,
  `Status ==` → `!=`) = **4 đỏ → xanh**; `qa` cũng có NC-4 riêng ở `RepPeriodClose.Chot()` (tiền đề trạng thái).

| Lệnh | Exit | Số đo |
|---|---|---|
| Baseline `dotnet build eCommerce.slnx --no-incremental` (mở phiên) | 0 | **0 error · 9 warning** |
| Baseline `dotnet test test/eCommerce.Tests --no-build` (mở phiên) | 0 | **2342/2342** · 0 skipped |
| Baseline `dotnet test test/eCommerce.IntegrationTests --no-build` | 0 | **122/122** |
| Baseline `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | **PASS** · 279 tệp · 964 link · 1692 backtick · 0 chết |
| Baseline `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** pass |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-26` | 0 | **XANH** |
| Sau F4: `dotnet build` / `dotnet test` / `dotnet test Integration` | 0 | 0 error · 9 warning · **2371** (+29) · **139** (+17) |
| `check-agent-artifact -Kind testplan -Path docs/tests/REP-001-testplan.md -Srs …` | 0 | **DAT** · BR trong SRS=17 · thiếu=0 |
| `negative-control.ps1` NC-1/2/3 (ORCH) | 0 | 3/3 **đỏ (exit 1) → xanh (exit 0)** |
| Vá P1: `dotnet build` / `dotnet test` / `dotnet test Integration` | 0 | 0 error · 9 warning · **2376** (+5) · **139** |
| `negative-control.ps1` NC-4 (ORCH, cổng `TongHopBaoCaoService.cs:467`) | 0 | **đỏ → xanh** (`20261127-034726-*`) |
| `check-agent-artifact -Kind review -Path docs/reviews/REP-001-review.md` | 0 | **DAT** · `verdict=PASS · P1=0` |
| **`pwsh -File tools/kiem-tat-ca.ps1` (G3)** | **0** | **10/10 ĐẠT · 564,6 s** |
| ├ build (mốc nhị phân) / unit / integration | 0 | 0 error · 9 warning · **2376** · **139** · 118,9 s / 43,2 s / 47,7 s |
| ├ mermaid / cổng tài liệu | 0 | 74,9 s · 3,6 s |
| ├ FE typecheck / Vitest / `gen:api` | 0 | 0 lỗi · **45/45** · `schema.d.ts` **KHÔNG đổi** (59/59 path) |
| └ DB / test cổng | 0 | **25/25** · **60/60** · 11,5 s / 210,9 s |

## 2. Chưa xong / cắt giữa

**Không có việc dở của `W26-4`** — DoD của hàng (F4 ⇒ G4, F5 ⇒ G5 `PASS`/`P1=0`) đã đạt. Các mục **chưa kiểm được** (ghi trong
testplan §6, **không** đánh dấu phủ):
- **BR-008 / TC-09 / TC-10 / TC-05b / TC-22(runtime resume)** — cần harness `DoWorkAsync` + `PeriodicBackgroundWorkerContext` +
  UoW/tenant thật (nợ `N90` §6); `D-248` cho phép dựng test-only nhưng chưa dựng trong lượt này.
- **API `/api/v1` 401/403 + 200 runtime** — mới kiểm metadata `[Authorize]`/quyền hẹp + logic app service.
- **NFR-001/002/003/006/009/010** — chưa có ca đo định lượng; NFR-004/007/008 mới một phần (logic/DB, chưa runtime worker + metric).
- **UI/E2E/OpenAPI drift** — N/A (`D-276`: 0 dòng FE; contract có chủ).

## 3. Migration đã áp

- **Không có** — phiên này **0 migration, 0 `pg_dump`** (chỉ sửa 3 tệp code + test + docs).

## 4. Quyết định mới

- **Không có `D-xx` mới** trong phiên (không gọi `decider`; P1 là lỗi so với hợp đồng SRS đã có, vá theo `REP-001` §3.3/§8.2#7/§12).

## 5. Nợ / rủi ro

- 🟡 **P3 R1** — khe TOCTOU hẹp: đọc cổng và `DELETE` cùng UoW nhưng hai khoá khác nhau (rebuild vs chốt kỳ, SRS §8.2#7);
  cần đóng kỳ commit đúng lúc giữa cổng-đọc và `DELETE` ⇒ chấp nhận, ghi nợ.
- 🟡 **P3 R2** — double-submit `StartRebuild` **đồng thời** vẫn có thể `23505` → 500 (chỉ lỗi HTTP, không hỏng dữ liệu).
- 🟡 **P3 R3** — `AlreadyRequested` gộp nghĩa "cùng yêu cầu" với "một rebuild **khác** đang chạy".
- 🟡 **P3 nợ `ba`** — BR-002 allow-list chưa nêu ngoại lệ "đối chiếu được đọc nguồn"; `TC02` chỉ quét `ReportAppService.cs`.
  Chủ: `ba`, cùng hàng contract/`WEB-*` của `D-276`.
- 🟡 **BR-008/UoW chưa có test thật** — chủ: phiên dựng harness (khuôn `D-248`).
- 🟡 **Cosmetic**: testplan có **hai dòng `NC-4`** ở hai bảng khác nhau (một của `qa` ở `RepPeriodClose.Chot`, một của ORCH ở
  `TongHopBaoCaoService.cs:467` do script tự nối vào bảng cuối) — cả hai đúng nội dung; đổi mã dòng ORCH thành `NC-5` khi có
  dịp (vùng `docs/tests` của `qa`).
- **Cảnh báo hook**: `rev` bị báo "thiếu khối JSON" **cả 2 vòng** — false positive đã biết (báo cáo thực **có** khối JSON; harness
  `last_assistant_message` rỗng ở lượt resume). Không có lần chặn dừng nào khác.
- **Chờ người (vùng cấm §6 hồ sơ uỷ quyền)**: (a) duyệt lô `D-270…D-276`; (b) kế toán R3-3 ký ngưỡng lý do `D-274`;
  (c) merge `main`. Ngoài ra người **xếp hàng** contract + WP màn hình `WEB-*` (`D-276` ⑤).

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `W26-4` → mở `W26-5` (WP-26, F6 OPS + G6 — ĐÓNG WP-26).**

- Hàng kế của chuỗi `gd3-1` = **`W26-5`** (loại `dong-wp`): `ops` viết tài liệu triển khai REP-001 (tệp docs/ops/REP-001-deploy.md,
  chưa tồn tại) có **đường lùi**; chạy
  `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` (**không** chạy song song build/test — nợ bộ nhớ N69; dự kiến > 10 phút ⇒ chạy nền
  theo cảnh báo `N90` §5); `07` §13 thêm dòng; xanh ⇒ commit + push nhánh WP rồi **fast-forward** `feat/gd3-wp26-read-model` →
  `tich-hop/gd3` (chỉ fast-forward; bị từ chối ⇒ **không** force, `PH dodang`).
- Bằng chứng sẵn có để `ops` dựa vào: migration `20261126181842_Wp26_VanHanh_BaoCao` (đã áp dev `N89`, `pg_dump` 497 829 B);
  G3 **10/10 · 564,6 s**; G4 **DAT**; G5 **DAT** (PASS/P1=0); 4 NC đỏ→xanh; unit **2376** · integration **139**.

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp26-read-model`; base `origin/tich-hop/gd3`; HEAD code **`57cf660`** (`docs(reporting): N91 - W26-4 F5 REV vong 2 PASS P1=0`);
  handover commit theo sau.
- Đã **push** nhánh WP (các mốc `608915d` → `7f6c8f4` → `4a964e6` → `a0ade44` → `57cf660`).
- **Chưa** đưa vào `tich-hop/gd3` — thuộc hàng `dong-wp` (`W26-5`) khi G6 xanh. **Không** mở PR (`D-216` ③).

---

**Hàng rào agent** (phiên `N91`): `qa` ×2 (vòng 1 bị trần 60 lượt giữa việc, resume hoàn tất) · `be` ×2 (vá P1 + bỏ fail-open) ·
`rev` ×2 (`CHANGES` → `PASS`). **0** lần chặn dừng; cảnh báo "thiếu khối JSON" của `rev` là false positive đã biết.
`git status` cuối phiên: sạch sau commit handover (`docs/**` + `agents/state/**`).
