# Doc review — `NOT-001` Template và hàng đợi thông báo (bổ sung luật che `D-269`)

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/notifications/not-001-template-hang-doi-thong-bao.md` (v0.8) |
| Ngày · vòng | 26/11/2026 · **vòng 2** (kiểm nhanh 6 chỗ BA sửa sau báo cáo vòng 1; header `v0.7 → v0.8`, ghi chú "Luật che" §5.2/§5.4/§5.5, một dòng §15.4) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | vòng 1: toàn văn dòng 1–1347 + `git diff`. Vòng 2: đọc lại **vùng sửa**: header dòng 6, §0 (dòng 44–45), câu mở §5 (dòng 326), `TC-14` (dòng 1031), §14.4 (dòng 1217), §15.2 F1 (dòng 1253) + grep toàn tệp xác nhận không còn câu "chưa có trong code"/"0 dòng code" cho module |
| Máy | `check-feature-doc.ps1 -Path <tệp> -WP14` → `KET QUA: DAT` (16/16 heading, 25/25 BR có test, đối chiếu Nhanh dòng 1304, 0 lỗi) |

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | vòng 2 đọc lại câu mở §5 (dòng 326 "module `Notification` **đã có** trong code"), §0 "Hiện trạng thật" (dòng 44–45 ✅ as-built), §14.4 (dòng 1217 module **đã có**), §15.2 F1 (dòng 1253 🟢 **Đã đóng**) ⇄ ghi chú as-built §5.4; đối chiếu `TC-14` (dòng 1031) ⇄ luật mask §5.4 (ii) | **0 phát hiện mở** — mâu thuẫn "chưa có code" ⇄ as-built đã hết; `TC-14` nay khớp luật (chỉ che **SĐT di động**, giữ `024…`/`1900…`) |
| D2 chéo tài liệu / code / D-xx | `codegraph_explore` + `Read` đúng dòng 3 tệp code vòng 1; vòng 2 kiểm 3 đường dẫn as-built **mới** ở §0/§14.4: `NotificationDomainModule.cs:14` (`public class NotificationDomainModule : AbpModule`), `NotificationErrorCodes.cs:8` (`public static class NotificationErrorCodes`), migration `20261126132231_Added_Notification_Template_Queue.cs` (tồn tại) | **0 phát hiện** — dẫn chiếu as-built mới đều có thật; luật (i)–(vi) khớp `D-269` ①(2); `D-269` (4) là điều khoản cho phép ghi chú luật vào SRS §5.2 |
| D3 cơ chế thực thi | BR-010 "`Rendered*`/`LastReasonMasked` phải đã che" ⇒ cái gì đỏ: `NotificationMaskerTests.TC14_bon_cot_da_che_chi_duoc_gan_tu_CheBiMat_trong_Domain` (quét tầng Domain: 4 cột chỉ gán từ `CheBiMat`) + `NotificationQueueAppenderTests.TC14_appender_mask_than_va_fingerprint_tren_gia_tri_da_luu`; `TC-14` mới yêu cầu ca số bàn `024…` **giữ nguyên** — test `TC14_sdt_di_dong_*` + `TC14_tien_ngay_madon_hotline_so_ban_giu_nguyen` đã có | **0 phát hiện** |
| D4 SQL chạy được | grep `^\`\`\`(sql\|powershell\|bash)` trên tệp | **không có** khối `sql`/`powershell`/`bash` (0 phát hiện) |
| D5 test/probe/tệp có thật | `NotificationMasker.cs:70`; `NotificationQueueAppender.cs:119-121`; `NotificationQueueEntities.cs:197-198,305,333,364,472`; **mới vòng 2**: `NotificationDomainModule.cs:14`, `NotificationErrorCodes.cs:8`, `NotificationTemplate.cs:8`, `NotificationQueueEntities.cs:144,451`, migration `20261126132231_Added_Notification_Template_Queue`; `test/.../NotificationMaskerTests.cs`, `test/.../NotificationQueueAppenderTests.cs` | **0 phát hiện** — mọi tên/dòng được nhắc đều có thật |
| D6 nhãn tin cậy | header dòng 6 nay "✅ **ĐÃ CÓ** (as-built `W22-2`/`W22-3`)"; §0 dòng 44–45 ✅; §15.2 F1 🟢 **Đã đóng**; header phiên bản v0.8 as-built; §15.4 v0.8 ghi vòng DOCREV 1 (1 P2 + 1 P3) | **0 phát hiện** — nhãn "chưa có trong code" lỗi thời (P2 vòng 1) đã sửa khớp "Trạng thái triển khai" |
| D7 14 quy tắc + đối chiếu Nhanh | `check-feature-doc.ps1 -WP14` → DAT; L10 (code thật kèm `file:dòng`) đạt; mục "Đối chiếu mô hình Nhanh" dòng 1304 còn nguyên; L3 (`TC-14` nêu input/expected đo được) đạt | **0 phát hiện** |

**Truy vấn đồ thị (bắt buộc trước Grep):**
- `graph: codegraph_explore "NotificationMasker CheBiMat NotificationQueueAppender NotificationQueueEntities ReasonMasked RenderedBodyMasked"` ⇒ `CheBiMat` ở `NotificationMasker.cs:70`; 17 caller gồm `NotificationQueueEntities`, `NotificationQueueAppService`, `NotificationQueueAppender`; test: `NotificationMaskerTests`, `NotificationQueueAppenderTests`. Khớp `NotificationMasker.cs:70` (§5.4/§5.2) và "một cửa ghi".
- `Read` `NotificationQueueAppender.cs:114-151,210-267` ⇒ mask **một lần** ở 119–120, `ContentFingerprint.TinhMuc(...)` ở 121, rồi truyền `subjectMasked`/`bodyMasked`/`fingerprint` cho `TaoMuc` (162) và `GhiKhudupAsync` (149).
- `Read` `NotificationQueueEntities.cs:144-208,285-395,430-479` ⇒ `TaoMoi` mask `RenderedSubject`/`RenderedBodyMasked` (197–198); `DanhDauLoi` (305), `Huy` (333), `GhiNhanBiKhudup` (364) mask `LastReasonMasked`; `NotificationQueueEvent.TaoMoi` mask `ReasonMasked` (472).
- (Vòng 2) `Read` `NotificationDomainModule.cs:1-16` + `NotificationErrorCodes.cs:1-12` ⇒ `:14` và `:8` khớp dẫn chiếu as-built mới ở §0/§14.4.

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| — | — | — | Không còn phát hiện mở ở vòng 2 (0 P1 · 0 P2 · 0 P3); 2 phát hiện vòng 1 đã đóng — xem bảng "Vòng trước" | — |

## Không kiểm được

- **D4:** tài liệu **không có** khối `sql`/`powershell`/`bash` ⇒ không có gì để chạy (đã grep xác nhận 0 khối).
- **`TC-45` phần "server lọc `Marketing`":** vẫn đúng là **CHƯA KIỂM ĐƯỢC** (`GetTemplatesForBatch` không tồn tại — nợ BE §15.5 N1); không phải lỗi tài liệu của `W22-4`.
- **Cổng `dotnet test` / `pnpm *`:** chưa chạy trong lượt kiểm tài liệu; tôi chỉ xác nhận **sự tồn tại + nội dung** các test `TC-14` bằng `Read`/Grep, không chạy chúng.

## Vòng trước (vòng 1 → vòng 2)

| # vòng 1 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 | đã đóng | P2 |
| 2 | đã đóng | P3 |

Chi tiết đóng:
- **#1 (P2) — nhãn "chưa có trong code" lỗi thời:** header dòng 6 nay "✅ **ĐÃ CÓ** trong code (as-built `W22-2`/`W22-3`)"; §0 "Hiện trạng thật" (dòng 44–45) nay ✅ as-built + dẫn `NotificationDomainModule.cs:14` / migration `20261126132231_Added_Notification_Template_Queue` / `NotificationTemplate.cs:8` / `NotificationQueueEntities.cs:144,451` / `NotificationErrorCodes.cs:8`; câu mở §5 (dòng 326) nay "module `Notification` **đã có** trong code …; các chỗ còn ghi 'kế hoạch' nêu rõ tại chỗ"; §14.4 (dòng 1217) nay "module `modules/Notification/` **đã có** … **0 gói NuGet**"; §15.2 **F1** (dòng 1253) nay 🟢 **Đã đóng** + giữ nguyên văn phát hiện gốc (v0.1). Grep xác nhận không còn "module chưa tồn tại"/"0 dòng code" cho module. 3 đường dẫn as-built mới đã kiểm có thật (`Read`).
- **#2 (P3) — `TC-14` ngữ nghĩa "số điện thoại":** dòng 1031 nay input "email + **SĐT di động** VN (`0912 345 678`); kèm số bàn/hotline `024…`/`1900…` như nội dung hợp lệ", expected "`RenderedBodyMasked` **không** chứa giá trị gốc của email/SĐT di động; số bàn/hotline **giữ nguyên** (chủ ý — `D-269` (ii))" — khớp luật mask §5.4 (ii) + test `TC14_tien_ngay_madon_hotline_so_ban_giu_nguyen`.
