# Handover N106 — WP-23 / hàng `W23-1` (F0–F2b `NOT-002`) XONG — `NOT-002-arch.md` `DOC-OK` + G2 tạm duyệt

> Phiên `N106` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W23-1` · WP-23 · mã `NOT-002` · module `Notification` ·
> nhánh `feat/gd3-wp23-kenh-gui` (base `origin/tich-hop/gd3` @ `c29940f`; commit N105 `88d5eb2`) · 28/11/2026.
> Phạm vi: **F0–F2b** — thiết kế tầng adapter **trung lập NCC** (`INotificationChannelSender` + registry + `fake` sender +
> fail-closed `0003`). **0 `.cs`, 0 migration, 0 sửa SRS.**

## 1. Đã xong

| Bước | Vai | Kết quả |
|---|---|---|
| Baseline mở phiên | ORCH | build **0 error · 9 warning** (có sẵn) · unit **2619/2619** · `kiem-link` **PASS** (307 tệp · 1015 link · 1879 backtick) · `hooks.test.mjs` **49/49** · Docker `ecommerce-postgres` + `ecommerce-redis` **up**. |
| F0 — đọc SRS theo mục + reviewer (K-08) | ORCH | SRS `NOT-002` (1074 dòng): §0 ADR, §0.1 as-built, §3, §5.5, §6.1, §7.3, §8.1, §9, §12, §14, §15. Reviewer K-08: **Kiến trúc trưởng · Vận hành · Security · người liên hệ NCC (`Q1`/`Q3`) · kế toán**; G1/G2 áp **M2**. |
| F2 — ARCH thiết kế | `arch` | Viết mới `docs/features/notifications/NOT-002-arch.md` (**702 dòng**, 17 mục theo khuôn `NOT-001-arch.md`): as-built đo bằng đồ thị · hợp đồng `INotificationChannelSender`/`SendCommand`/`SendOutcome`/`NotificationFailureKind` · registry `INotificationChannelFactory` · cổng config `INotificationChannelConfigProvider` (stub `null`) · fail-closed `0003` **7 điều kiện** · **0 bảng/0 migration/0 endpoint/0 metric mới** · 8 `openQuestions` (`AQ1–AQ8`) · 9 `docDeviation`. |
| F2b — DOCREV vòng 1 | `docrev` | **`DOC-CHANGES` · 1 P1 · 8 P2 · 9 P3** (`docs/reviews/NOT-002-arch-docreview.md`). P1 = credential không có đường tới adapter (`SendCommand` thiếu ciphertext mà §10 cấm, adapter lại là nơi giải mã). |
| F2b — ARCH vòng 2 (sửa) | `arch` | Đóng **18/18**: P1 = một đường `SendCommand.CredentialEncrypted` (ciphertext) → adapter giải mã một lần (`AQ8`); `Unauthenticated` ⇒ **NÉM** `0003` nhất quán + `docDeviation #8`; sửa dẫn `file:dòng`; §8.2 thành **phụ lục ngoài phạm vi** đủ cột D-202/D-207 + NND bắt buộc (114→115); TC mới có nhãn "tạo ở `W23-4`" + map. |
| F2b — DOCREV vòng 2 | `docrev` | **`DOC-OK` · 0 P1 · 0 P2 · 0 P3** — 18/18 đóng đúng bản chất, 0 phát hiện mới. |
| G2 — decider (M2) | `decider` | **`D-313`** = G2 **TẠM DUYỆT** `NOT-002-arch.md` cho code WP-23 **chỉ trên nhánh WP**; **`D-314`** = `AQ1` **0 migration/0 bảng/0 `DbSet`/0 endpoint** (kiểm `0003` bằng provider stub `null`); **`D-315`** = `AQ2–AQ8` (hoãn adapter SMTP thật; chốt shape; `CorrelationId` additive; tên hợp đồng callback; xoá `ChuaCoKenhGuiSender` thay `NotificationSender`; dùng lại metric `0003`; credential ciphertext trong `SendCommand`). |

**DoD của hàng — ORCH tự chạy (không tin số vai báo):**

| Cổng | Lệnh | Số đo |
|---|---|---|
| DOCREV | `check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/NOT-002-arch-docreview.md` | **exit 0 · `DAT`** — `DOC-OK` · P1=0 · P2=0 |
| DEC | `check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | **exit 0 · `DAT`** — 117 dòng; `D-313/314/315` ghi |
| Cổng `Code` WP-23 (điều kiện vào) | `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-23` | **XANH (exit 0)** — "mọi mã của WP-23 đều Tạm duyệt" |
| Link · mermaid | `kiem-link-tai-lieu.ps1` · `npm --prefix tools run check:mermaid` | **PASS** (309 tệp · 1016 link · 1881 backtick · 0 chết) · **214/0** |
| Hàng rào | `node --test .claude/hooks/test/hooks.test.mjs` | **49/49** |
| G3 (F0–F2b, 0 `.cs`) | build + unit đã đo ở baseline | **0 error/9 warning · 2619/2619**; đóng phiên **bỏ qua có lý do** "hàng F0–F2b, 0 `.cs`" (tiền lệ N83) |
| Điểm xanh | `git status --porcelain` | 4 tệp sửa (`agents/state`, `bang-phien`, `hang-doi-xem-lai`, `decisions`) + 2 tệp mới (`NOT-002-arch.md`, `NOT-002-arch-docreview.md`) — đúng vùng ghi |

## 2. Chưa xong / cắt giữa

**Không có việc dở của `W23-1`** — DoD của hàng đã đạt (DOCREV `DOC-OK` 0 P1/P2, G2 tạm duyệt `D-313`, probe Code WP-23 XANH). Các mục dưới là **cắt có quyết định**, đã có chủ + `D-xx`:

1. **P2 as-built + đồng bộ SRS `NOT-002`** — `D-312` ③ + `NOT-002-arch` `docDeviation #1–#9`: `ba` sửa **cùng PR code WP-23** (hàng `W23-2`): §0.1/§14.4/§15.2 (17 module · `Notification` 28 tệp · bỏ "0 dòng code miền thông báo") + bump header/§15.4; §6.1 theo canonical `D-264`; §1 E3/§12 (credential ⇒ NÉM `0003`); §3/§12 (`0006` ⇒ `Loi`); §11/§14.3 (0 bảng ở WP-23). **Chủ `ba`, không chặn `W23-1`.**
2. **Nợ N1** — `INotificationChannelConfigProvider.FindAsync(ChannelKind)` chưa nhận `SenderIdentity`/`ChannelAccountId`; increment đầy đủ (sau `R3-6`) phải **mở rộng chữ ký** và khi đó `NotificationChannelAccounts` cần unique NND (114 → 115 index). Chủ: increment `NOT-002` đầy đủ.
3. **Rủi ro R2** — hai đường đăng ký `INotificationSender` (`NotificationApplicationModule.cs:24` + `ITransientDependency`). `AQ6` đã chốt **xoá/thay** ở `W23-2`; nếu `W23-2` quên, test `TC-DI-01/02` phải đỏ.

## 3. Migration đã áp

**Không có.** Hàng F0–F2b: 0 `.cs`, 0 migration, 0 `pg_dump` (`AQ1`/`D-314` chốt **0 bảng** ở WP-23).

## 4. Quyết định mới

- **`D-313`** (TẠM CHỐT DEC, uỷ quyền M2) — **G2 tạm duyệt** `docs/features/notifications/NOT-002-arch.md` cho code WP-23 **chỉ trên nhánh WP**; điều kiện DOCREV `DOC-OK` 0 P1 đã đạt; merge `main` vẫn cần **biên bản người ký**; `Q1`/`Q3`/HMAC production **ngoài** tạm duyệt (`R3-6`).
- **`D-314`** (TẠM CHỐT DEC) — `AQ1`: WP-23 **0 migration/0 bảng/0 `DbSet`/0 endpoint**; fail-closed `0003` kiểm qua `INotificationChannelConfigProvider` + stub `ChuaCauHinhKenhProvider` trả `null`; bảng `NotificationChannelAccounts` hoãn tới increment đầy đủ sau `R3-6`.
- **`D-315`** (TẠM CHỐT DEC) — `AQ2–AQ8`: hoãn adapter SMTP thật (chỉ `fake` sender); chốt shape `SendOutcome`/`NotificationFailureKind`; `CorrelationId` additive default `null` vào canonical request; tên hợp đồng callback `INotificationCallbackParser` (0 endpoint, `D-202`); xoá `ChuaCoKenhGuiSender` thay `NotificationSender` (giữ kỳ vọng test `D-263`); dùng lại metric `notification_queue_failed_total{reason=…:0003}`; credential **ciphertext** trong `SendCommand`.
- Cả 3 đã ghi `docs/nhanh/decisions.md` + 3 dòng `docs/agents/hang-doi-xem-lai.md`; `check-agent-artifact -Kind decision` **exit 0**.

## 5. Nợ / rủi ro

- 🟠 **P2 as-built + SRS đồng bộ** (`D-312` ③, `docDeviation #1–#9`) — chủ **`ba`**, **cổng cứng** trước `W23-2` code; thiếu ⇒ DOC-OK của WP-23 **không** được cấp.
- 🟠 **Ngoài tạm duyệt (`R3-6`)** — `Q1` chọn NCC email/SMS (`D-200`), `Q3` 5 thông số Zalo OA (`D-202`), khoá HMAC production (`D-204`) — **người**. Tới khi chốt: chỉ adapter trung lập + `fake` sender + fail-closed `0003`; **0** gói NuGet NCC.
- 🟡 **N1** — `FindAsync` phải mở rộng khi có bảng tài khoản kênh (chủ increment đầy đủ).
- 🟡 **R2** — hai `INotificationSender` (giải ở `W23-2` bằng `AQ6`; test DI bắt buộc).
- 🔴 **Merge `main`** vẫn cần **biên bản người ký** (`D-71`/§A.5; cổng `MergeMain` ĐỎ). PR `tich-hop/gd3 → main` mở cuối chuỗi.
- **Chờ người** (gom một lần ở câu trả lời cuối): (a) duyệt lô `D-277…D-315`; (b) chọn NCC email/SMS + 5 thông số Zalo OA + khoá HMAC production (`R3-6`); (c) merge `main` + PR nháp `tich-hop/gd3 → main` khi chuỗi xong.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG W23-1 → mở W23-2 (WP-23 F3 BE).**

- Hàng kế = **`W23-2`** (loại `code`, WP-23 `NOT-002`, bước **F3 BE**, vai `be`); cùng nhánh `feat/gd3-wp23-kenh-gui`.
- Điều kiện vào: `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-23` **exit 0** (đã **XANH** ở N106). Đọc `.claude/skills/feature-pipeline/SKILL.md`, chạy đúng **F3 BE**.
- Phạm vi F3 BE: hiện thực theo `NOT-002-arch.md` + `D-313/314/315` — `INotificationChannelSender`/`Factory`/`SendCommand`/`SendOutcome`/`NotificationFailureKind` + `NotificationSender` (canonical `D-264`) + `ChuaCauHinhKenhProvider` stub + `fake` sender trong test + fail-closed `0003`; **0 migration/0 bảng/0 endpoint**. Gọi `ba` sửa SRS (`docDeviation #1–#9` + P2 as-built `D-312`) **cùng nhánh/cùng PR** rồi `docrev` kiểm nhanh.

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp23-kenh-gui`; base `origin/tich-hop/gd3` @ `c29940f`; commit N105 `88d5eb2`.
- Commit phiên N106 (sau khi đóng phiên): `docs(...)` chứa `NOT-002-arch.md` (702 dòng) + `NOT-002-arch-docreview.md` (79 dòng) + `decisions.md`/`hang-doi-xem-lai.md` (`D-313/314/315`) + `07` §13 + tệp handover này + sổ registry (**SHA = `git log -1`**).
- **Đã push nhánh WP**. **KHÔNG** đẩy `tich-hop/gd3` (hàng `W23-1` là F0–F2b, chỉ FF ở hàng `dong-wp` khi G6 xanh). **Không** force/merge, **không** mở PR (`D-216` ③), **không** đụng `main`.

**Hàng rào agent** (phiên `N106`): `arch` ×2 (viết + sửa vòng 2) · `docrev` ×2 (vòng 1 + vòng 2 kiểm nhanh) · `decider` ×1. `canh-bao.jsonl` ghi 3 entry cho phiên (arch vòng 1, docrev vòng 1, arch vòng 2); **`ngoaiVung: []`** mọi entry · `lanChan: 0`. Cảnh báo "thiếu khối JSON cuối" là **lỗi đọc transcript đã biết** (hook không thấy `last_assistant_message`), báo cáo cuối vẫn có khối JSON; ORCH tự chạy mọi cổng máy.
