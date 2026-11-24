# Bảng phiên chạy WP-14 (session runner)

> Cổng tài liệu GĐ3 (`D-71`, [07 §WP-14](../nhanh/07-ke-hoach-trien-khai.md)) — 18 tài liệu, 3 đợt, 3 lần duyệt của chủ dự án.
> Khuôn theo §14 "Bảng phiên chạy" của dự án SelfLearningAI (`ke-hoach-xay-dung-cms.md`), làm cho eCommerce:
> **một lệnh dán cho mọi phiên**, phiên tự chọn việc, tự cập nhật bảng, tự commit.

## 0. Chạy tự động khi không ngồi máy (khuyến nghị)

Mở **một** cửa sổ PowerShell tại repo, chạy rồi để máy bật (tắt chế độ ngủ của Windows):

```powershell
pwsh -File tools/agents/chay-tu-dong.ps1
```

Bộ chạy lặp: chọn hàng kế (`phien.ps1 next`) → mở **một phiên Claude Code mới** (`claude -p "/chay-phien"`, Sonnet, auto mode, không chờ ai duyệt)
→ chờ phiên xong (bảng + handover + commit) → hàng kế. Hết hạn mức 5 giờ ⇒ tự ngủ tới giờ hồi rồi chạy tiếp đúng hàng đó.
Tự dừng khi: hết việc của agent · tới hàng 🗣/👤 (cần anh/chị) · hết hạn mức **tuần** · 2 phiên liền không tiến triển (cầu dao) ·
có tệp phanh `artifacts/agents/tu-dong/DUNG` (tạo tệp đó để dừng sau phiên đang chạy). Nhật ký: `artifacts/agents/tu-dong/nhat-ky.md`.

⚠️ **Một phiên một lúc.** Không mở thêm cửa sổ `/chay-phien` khi bộ chạy đang chạy, và không mở hai cửa sổ `/chay-phien` cùng lúc:
mọi phiên dùng chung thư mục, nhánh và các tệp `decisions.md`, nháp biên bản, bảng này (đêm 19–20/11 ba phiên song song đã trùng số `N30`,
commit lẫn quyết định của nhau, mất `handover-n30`). Cột Phụ thuộc chỉ nói về **nội dung**; việc chạy luôn tuần tự.

## 1. Cách dùng — chủ dự án chỉ làm 3 việc

> **Một lần, trước đợt còn lại — phiên uỷ quyền.** `U-01` (agent tự gom câu hỏi, anh/chị không cần ngồi) rồi `U-02`: anh/chị **ngồi cùng
> ~1–2 giờ** thảo luận với `decider` — chốt luật chơi M1–M6, chính sách theo nhóm câu, ngoại lệ. Kết quả là
> [hồ sơ uỷ quyền](uy-quyen-chu-du-an.md) **HIỆU LỰC**: từ phiên sau, `decider` quyết thay anh/chị theo hồ sơ; bảng "Chờ người" cuối phiên
> chỉ còn vùng cấm (merge `main`, dữ liệu thật, tiền thật, secret, NCC/hợp đồng) và việc phụ thuộc bên ngoài. Mọi hàng tài liệu ⬜ phụ thuộc `U-02`.

1. Mở phiên mới tại repo (`claude`, rồi `/model sonnet`) và dán **đúng một dòng**:

   ```text
   /chay-phien
   ```

   Phiên tự chọn hàng kế tiếp (`tools/agents/phien.ps1 next`), tự mở phiên (baseline), chạy dây chuyền `/doc-gate`, đóng phiên (handover),
   **cập nhật bảng dưới**, rồi commit + push nhánh WP-14. Không hỏi giữa chừng: câu cần người được gom thành **một bảng ở cuối phiên**.
   Muốn chạy đúng một hàng: dán ô **Prompt để dán** của hàng đó (ví dụ `/chay-phien P-05`).
2. Cuối mỗi phiên: đọc bảng "Chờ người" trong câu trả lời cuối — trả lời ở **đầu phiên sau** (dán kèm sau `/chay-phien`), hoặc để đó: các hàng khác vẫn chạy.
3. Hàng 👤 **Chờ người** (duyệt đợt, mở PR): anh/chị tự làm, rồi đánh dấu xong bằng một lệnh (hoặc nói với phiên kế "P-09 đã duyệt, biên bản ở …"):

   ```powershell
   pwsh -File tools/agents/phien.ps1 done -Id P-09 -Evidence "biên bản docs/bien-ban-duyet-gd3-a-<ngày>.md: 9/9 duyệt"
   ```

Phiên kết thúc khi hàng đạt DoD (✅) **hoặc** chạm điểm dừng (hạn mức 5 giờ, context lớn, 3 vòng cùng lỗi) — khi đó hàng thành 🔁 **Dở dang**
và lần `/chay-phien` sau làm tiếp đúng phần còn lại theo handover. Chạm hạn mức giữa chừng: đợi hạn mức hồi rồi dán lại `/chay-phien`.

## 2. Luật chung (skill `/chay-phien` thi hành)

1. Cột **Trạng thái** là view của `agents/state/phien-wp14.json`; chỉ `tools/agents/phien.ps1` được đổi — **không sửa tay** (`phien.ps1 check` báo lệch).
2. `start` bắt buộc trước khi làm; `done` bắt buộc **bằng chứng** (exit code, verdict, D-xx) + handover; thiếu bằng chứng thì không phải hoàn thành.
3. Một lúc **một** hàng 🟨. Hàng có phụ thuộc chưa ✅ không chạy. Hàng 👤 không bao giờ do agent đánh dấu xong.
4. Quyết định: qua `decider` (tuần tự), chỉ vùng cấm mới chờ người (CLAUDE.md §5); sau `U-02`, decider quyết theo
   [hồ sơ uỷ quyền](uy-quyen-chu-du-an.md) — kể cả tạm duyệt đợt nếu M2 = có (`phien.ps1 uyquyen` chuyển hàng 👤 sang decider; `P-21` merge không bao giờ). Không merge `main`, không mở PR — đó là hàng P-21 của người.
5. Phiên trước chạy theo cách cũ (`/mo-phien` + `/doc-gate` tay, không cập nhật bảng): `/chay-phien` **đối soát** handover mới nhất trước khi chọn việc.

## 3. Bảng

**Trạng thái:** ⬜ Chưa bắt đầu · 🗣 Chờ ngồi cùng (phiên uỷ quyền) · 🟨 Đang làm · 🔁 Dở dang (phiên sau làm tiếp) · ✅ Hoàn thành · ⛔ Bị chặn · 👤 Chờ người.

<!-- BANG-PHIEN:BAT-DAU -->
| # | Đợt | Nội dung | Vai | Phụ thuộc | Prompt để dán | Phiên | Trạng thái | Bằng chứng / ghi chú |
|---|---|---|---|---|---|---|---|---|
| `P-01` | A | `ADDR-001` tra cứu ĐVHC 2 cấp + `ADDR-002` đồng bộ mã địa chỉ theo hãng | ORCH · ba · docrev · decider | — | `/chay-phien P-01` | N22 | ✅ Hoàn thành | check-feature-doc -WP14 exit 0 ×2 · DOC-OK 0 P1 ×2 · D-77…D-94 · `docs/nhanh/handover-n22.md` |
| `P-02` | A | `ADDR-003` chuẩn hoá địa chỉ 3 cấp ↔ 2 cấp | ORCH · ba · docrev · decider | — | `/chay-phien P-02` | N23, N24 | ✅ Hoàn thành | DOC-OK vong 4 (0 P1, 2 P2, 37 P3); check-feature-doc exit 0 (17 BR, 47 TC); D-105..D-107 + D-111; 2 P2 sua truoc WP-15/WP code; ghi chu addr-004 6.1 con lai · `docs/nhanh/handover-n26.md` |
| `U-01` | U | Chuẩn bị phiên uỷ quyền: gom câu mở §15 của 18 tài liệu + hàng đợi `D-xx` chờ người + M1–M6; decider soạn khuyến nghị, xếp 4 rổ R0–R3 ⇒ `docs/agents/phien-uy-quyen-cau-hoi.md` | ORCH · decider | — | `/chay-phien U-01` | N27 | ✅ Hoàn thành | phien-uy-quyen-cau-hoi.md: 166 cau §15 + 37 dong D-xx xep ro (R0 50+24, R1 58→16 chinh sach, R2 30+11, R3 28+2) · decider 5 luot · kiem-link PASS · `docs/nhanh/handover-n27.md` |
| `U-02` | U | **Phiên uỷ quyền — chủ dự án thảo luận với decider** (~1–2 giờ, một lần): chốt M1–M6, chính sách R1, ngoại lệ R2, ai lo R3 ⇒ `docs/agents/uy-quyen-chu-du-an.md` có hiệu lực; từ đây decider quyết thay | Chủ dự án · ORCH · decider | U-01 | `/chay-phien U-02` (ngồi cùng) | N28 | ✅ Hoàn thành | AskUserQuestion 7 luot: M1-M6,V1-V3,P1-P16,R2,R3; ho so HIEU LUC; D-114,D-115; 36 dong D-xx DA CHOT; kiem-link PASS, mermaid 0 loi, hooks 34/34 · `docs/nhanh/handover-n28.md` |
| `P-03` | A | `SHP-001` adapter GHN, tính phí, vận đơn (131 KB ⇒ một mình) | ORCH · ba · docrev · decider | U-02 | `/chay-phien P-03` | N29 | ✅ Hoàn thành | SHP-001 DOC-OK vong kiem nhanh (0 P1, 0 P2, 17 P3); check-feature-doc -WP14 exit 0 (22 BR, 52 TC); check-agent-artifact -Kind docreview exit 0; nhap-bien-ban-gd3-a co dong SHP-001; D-108..D-110 TAM CHOT · `docs/nhanh/handover-n25.md` |
| `P-04` | A | `SHP-002` webhook trạng thái vận đơn + `SHP-004` adapter GHTK | ORCH · ba · docrev · decider | U-02 | `/chay-phien P-04` | N29, N30, N32, N34, N35, N36, N37 | ✅ Hoàn thành | check-feature-doc SHP-004 -WP14 exit 0; DOCREV vong 2 DOC-OK 0 P1/2 P2/15 P3; D-128..D-131; link PASS; mermaid 0 loi · `docs/nhanh/handover-n37.md` |
| `P-05` | A | `SHP-003` COD thu hộ / đổi COD (121 KB ⇒ một mình; 9 câu chờ hãng) | ORCH · ba · docrev · decider | U-02 | `/chay-phien P-05` | N30 | ✅ Hoàn thành | check-feature-doc -WP14 exit 0 (16/16, 22/22 BR); docreview DOC-OK vong 2 (0 P1/2 P2) exit 0; D-117 D-120; kiem-link PASS; mermaid 0 loi · `docs/nhanh/handover-n32.md` |
| `P-06` | A | `REC-003` đối soát COD import Excel | ORCH · ba · docrev · decider | P-05, U-02 | `/chay-phien P-06` | N33 | ✅ Hoàn thành | check-feature-doc -WP14 exit 0 (16/16 muc, 16/16 BR, 44 TC); DOCREV REC-003 DOC-OK vong 2 (0 P1, 2 P2, 9 P3) + check-agent-artifact exit 0; D-121..D-127 (check decision DAT); D3 nhap bien ban; link PASS, mermaid 0 loi · `docs/nhanh/handover-n33.md` |
| `P-07` | A | `SAL-006` trả hàng / RMA / hoàn tiền (303 KB, danh sách đỏ ⇒ một mình) | ORCH · ba · docrev · decider | U-02 | `/chay-phien P-07` | N38, N39 | ✅ Hoàn thành | check-feature-doc -WP14 exit 0 (16/16, 23/23 BR); docreview vong 3 DOC-OK P1=0 P2=2 P3=5; check-agent-artifact docreview+decision exit 0; link PASS; mermaid 211/0; D-139,D-140 · `docs/nhanh/handover-n39.md` |
| `P-08` | A | Chuẩn bị hồ sơ duyệt đợt A: nháp biên bản đủ dòng mọi tài liệu của đợt, danh sách người review bắt buộc theo loại, bảng câu chờ người còn mở | ORCH | P-01, P-02, P-03, P-04, P-05, P-06, P-07 | `/chay-phien P-08` | N40 | ✅ Hoàn thành | check-feature-doc 9/9 DAT; nhap-bien-ban-gd3-a.md du 9 dong + tom tat + 10 cau cho nguoi; link PASS; mermaid 0 loi; hooks 38/38 · `docs/nhanh/handover-n40.md` |
| `P-09` | A | Duyệt đợt A: lập `docs/bien-ban-duyet-gd3-a-<ngày>.md` (9 tài liệu) + người đã liên hệ GHN/GHTK + kế toán (COD, hoàn tiền) | ORCH · decider (uỷ quyền D-115) | P-08 | `/chay-phien P-09` | N41 | ✅ Hoàn thành | bien-ban-tam-duyet-gd3-a-2026-11-22.md: 4/9 tam duyet (ADDR-001/002, SHP-001/002), 5/9 chua; D-141; kiem-link PASS · `docs/nhanh/handover-n41.md` |
| `P-10` | B | `PRO-001` voucher / coupon (143 KB ⇒ một mình) | ORCH · ba · docrev · decider | U-02 | `/chay-phien P-10` | N42, N43, N44, N45 | ✅ Hoàn thành | check-feature-doc -WP14 DAT 16/16 muc 36/36 BR; DOCREV DOC-OK vong 3 P1=0 P2=1; kiem-link PASS; mermaid 0 loi; D-146..D-150 · `docs/nhanh/handover-n45.md` |
| `P-11` | B | `PRO-002` engine điều kiện khuyến mại + `LOY-001` điểm thưởng | ORCH · ba · docrev · decider | U-02 | `/chay-phien P-11` | N46, N47, N48, N49, N50, N51, N52, N53 | ✅ Hoàn thành | check-feature-doc -WP14 exit 0 (16/16, 24/24 BR, 0 Open); DOCREV vong 4 DOC-OK P1=0/P2=1; check-agent-artifact docreview+decision exit 0; kiem-link PASS; mermaid 211/0; hooks 40/40; D-168 + cap nhat D-167 · `docs/nhanh/handover-n53.md` |
| `P-12` | B | Chuẩn bị hồ sơ duyệt đợt B: nháp biên bản đủ dòng mọi tài liệu của đợt, danh sách người review bắt buộc theo loại, bảng câu chờ người còn mở | ORCH | P-10, P-11 | `/chay-phien P-12` | N54 | ✅ Hoàn thành | kiem-link PASS 0 chet (185 tep/846 link/1108 backtick); check:mermaid 211/0; hooks 40/40; PH check OK; nhap bien ban 3/3 dong + bang cau cho nguoi 13 cau/26 TAM CHOT/6 viec · `docs/nhanh/handover-n54.md` |
| `P-13` | B | Duyệt đợt B: lập `docs/bien-ban-duyet-gd3-b-<ngày>.md` (3 tài liệu) + review kiến trúc cho engine điều kiện | ORCH · decider (uỷ quyền D-115) | P-12 | `/chay-phien P-13` | N55 | ✅ Hoàn thành | check-feature-doc -WP14 3/3 DAT; D-169 (tam duyet 2/3, LOY-001 chua) + bien ban tam duyet 3 dong + 3 dong hang doi; check-agent-artifact decision DAT (54); kiem-link PASS 0 chet (188 tep/859 link/1136 backtick); mermaid 211/0; hooks 40/40; PH check OK · `docs/nhanh/handover-n55.md` |
| `P-14` | C | `REP-001` read-model + `REP-002` doanh thu, lãi gộp | ORCH · ba · docrev · decider | U-02 | `/chay-phien P-14` | N56, N57, N58 | ✅ Hoàn thành | check-feature-doc -WP14 DAT (16/16 heading, 18/18 BR, 0 Open); check-agent-artifact docreview DAT DOC-OK (P1=0 P2=0 P3=5); kiem-link PASS 0 chet (193 tep/880 link/1171 backtick); check:mermaid 212 khoi/0 loi; hooks 48/48; 0 D-xx moi · `docs/nhanh/handover-n58.md` |
| `P-15` | C | `REP-003` tồn kho, hiệu suất kênh + `ACC-001` xuất liệu kế toán | ORCH · ba · docrev · decider | U-02 | `/chay-phien P-15` | N59 | ✅ Hoàn thành | check-feature-doc -WP14 exit 0 x2 (REP-003 14/14 BR 0 Open; ACC-001 18/18 BR 1 Open=Q12); DOCREV DOC-OK P1=0/P2=0 x2; check-agent-artifact docreview x2 + decision DAT (81 dong); D-176..D-196 (21 TAM CHOT DEC); kiem-link PASS 196 tep/884 link/1259 backtick; mermaid 212/0; hooks 48/48 · `docs/nhanh/handover-n59.md` |
| `P-16` | C | `NOT-001` template, hàng đợi thông báo (133 KB ⇒ một mình) | ORCH · ba · docrev · decider | U-02 | `/chay-phien P-16` | N60 | 🟨 Đang làm |  |
| `P-17` | C | `NOT-002` kênh email / SMS / Zalo OA | ORCH · ba · docrev · decider | U-02 | `/chay-phien P-17` | — | ⬜ Chưa bắt đầu |  |
| `P-18` | C | Chuẩn bị hồ sơ duyệt đợt C: nháp biên bản đủ dòng mọi tài liệu của đợt, danh sách người review bắt buộc theo loại, bảng câu chờ người còn mở | ORCH | P-14, P-15, P-16, P-17 | `/chay-phien P-18` | — | ⬜ Chưa bắt đầu |  |
| `P-19` | C | Duyệt đợt C: lập `docs/bien-ban-duyet-gd3-c-<ngày>.md` (6 tài liệu) — **bắt buộc kế toán** cho `ACC-001`, `REP-002` | Chủ dự án | P-18 | — (việc của người) | — | 👤 Chờ người | U-02 có thể chuyển hàng này cho decider tạm duyệt (M2) — `phien.ps1 uyquyen` |
| `P-20` | — | Đóng WP-14: cập nhật `docs/features/README.md` §2 theo 3 biên bản; đề xuất thứ tự `§WP-15+` (07 §WP-14 ⑤, decider tạm chốt trong phạm vi D-70); 07 §13 | ORCH · decider | P-09, P-13, P-19 | `/chay-phien P-20` | — | ⬜ Chưa bắt đầu |  |
| `P-21` | — | Mở + merge PR: `chore/agents-b1-b2` trước, rồi `docs/n22-wp14-dot-a` (mô tả: `artifacts/pr/mo-ta-pr-wp14-dot-a.md`, dòng đầu `Refs: D-71`) | Chủ dự án | P-20 | — (việc của người) | — | 👤 Chờ người | vùng cấm (merge `main`) — không uỷ quyền được |
<!-- BANG-PHIEN:KET-THUC -->

## 4. Lệnh của registry

| Lệnh | Khi nào | Kết quả |
|---|---|---|
| `pwsh -File tools/agents/phien.ps1 next` | đầu phiên | Id hàng phải chạy (🟨 > 🔁 > ⬜ đủ phụ thuộc); exit 3 = chỉ còn việc của người; exit 5 = hết việc |
| `... start -Id P-xx -Session Nxx` | trước khi làm | 🟨 |
| `... done -Id P-xx -Evidence "..." -Handover docs/nhanh/handover-n<x>.md` | đạt DoD | ✅ |
| `... dodang -Id P-xx -Reason "..." -Handover ...` | hết phiên, chưa đạt DoD | 🔁 |
| `... blocked -Id P-xx -Reason "..."` | bị chặn bên ngoài | ⛔ |
| `... release -Id P-xx` | huỷ nhận | ⬜ |
| `... uyquyen -Id P-xx -Evidence D-xx` | `U-02` xong, M2 = có | hàng 👤 duyệt đợt ⇒ decider tạm duyệt (biên bản `docs/bien-ban-tam-duyet-gd3-<đợt>-<ngày>.md`) |
| `... sync` / `... check` | sau khi sửa registry / trước khi commit | bảng sinh lại / kiểm bảng khớp |

Ước lượng: bảng chia theo luật `/doc-gate` (> 120 KB một tài liệu/phiên, `SAL-006` riêng). Thực tế đo được: `ADDR-001…002` một phiên,
`ADDR-003` ba phiên ⇒ 14 tài liệu còn lại khoảng **11–18 phiên**. Kế hoạch dựng hệ: [ke-hoach-v2-claude-code-pro.md](ke-hoach-v2-claude-code-pro.md) §16.
