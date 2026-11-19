# Đề xuất: uỷ quyền một lần cho `decider` + dây chuyền agent tự chạy đến hết việc

> **Trạng thái:** Đề xuất — chờ chủ dự án duyệt · **Ngày:** 17/11/2026
> **Bối cảnh:** chủ dự án yêu cầu (17/11): *"rất nhiều lần process phải dừng lại hỏi ý kiến … muốn thảo luận một lần, lưu thông tin
> cho DEC thay tôi quyết định, log lại để tôi xem sau; sau đó các vai tự chạy theo workflow tới khi hoàn thành"*.
> **Quan hệ:** bổ sung cho [ke-hoach-v2-claude-code-pro.md](ke-hoach-v2-claude-code-pro.md) (hàng rào, vai, skill) và
> [ke-hoach-he-thong-agent.md](ke-hoach-he-thong-agent.md) §13 (agent `decider`). Đề xuất này **đảo một điểm** của v2: C11 (hoãn
> hàng việc máy đọc) — vì muốn tự chạy thì bắt buộc có hàng việc (§6).
> **Chưa làm gì ngoài tệp này** (và một dòng trỏ tới đây trong v2). Không có `D-xx` mới.

---

## 0. Tóm tắt trong 10 dòng

1. Khảo sát tìm ra **6 loại điểm dừng**, không chỉ "câu hỏi quyết định": hết phiên phải dán câu mở phiên (21 lần, `N1…N21`), hỏi
   quyền công cụ, cổng duyệt tài liệu/merge, hết hạn mức, lỗi môi trường, và câu hỏi quyết định (§1).
2. Riêng 18 tài liệu WP-14 đang có **159 câu hỏi `Open`** trong §15. Phân loại theo cột "Cần ai": ~38 kỹ thuật (DEC quyết được),
   ~88 chính sách kinh doanh/kế toán (cần chủ dự án — nhưng **không cần hỏi từng câu**), ~27 phụ thuộc NCC (không ai trả lời được
   nếu chưa gọi thử sandbox/liên hệ hãng), 3 pháp lý.
3. Ý tưởng chính: **"Phiên uỷ quyền" một lần** — agent gom trước mọi câu sắp gặp, DEC soạn sẵn đáp án khuyến nghị, chủ dự án chỉ
   trả lời **~20 câu chính sách** + duyệt bảng đáp án **theo ngoại lệ** ("đồng ý tất cả, trừ dòng…"). Kết quả lưu thành
   **Hồ sơ uỷ quyền** — tiêu chí số 0 của DEC.
4. DEC thay người cho mọi việc đảo ngược được; việc **không** đảo ngược được vẫn chuyển người nhưng **không chặn dây chuyền**: chỉ
   chặn đúng một mục trong hàng việc, các mục khác chạy tiếp.
5. Tự chạy = **hàng việc máy đọc** + **bộ chạy** (`claude -p` mỗi đơn vị việc một phiên mới, `--permission-mode auto`) + **Stop hook**
   không cho phiên kết thúc trước khi đạt DoD hoặc đã ghi `blocked` + tự chờ khi hết hạn mức 5 giờ.
6. Người chỉ còn xuất hiện ở 3 chỗ: **phiên uỷ quyền** (một lần, cập nhật khi cần) · **duyệt lô** quyết định tạm (khi rảnh, không chặn)
   · **merge vào `main`/phát hành/tiền thật/hợp đồng** (vùng cấm, giữ nguyên).
7. Mọi quyết định có **log + cách lùi + "xem lại trước khi"** (đã có ở `decision-policy`), thêm **báo cáo chạy** mỗi ngày và lệnh
   `/duyet-lo` để chủ dự án duyệt nhanh.
8. Giới hạn nói thẳng: câu NCC không ai quyết thay được; quyết định tạm sai **tốn công làm lại**; gói Pro giới hạn **lượng việc mỗi
   tuần** — tự chạy bỏ được thời gian chờ người, **không** tăng hạn mức.
9. Lộ trình 7 bước U0–U6, gắn vào B0–B7 của v2; **phiên uỷ quyền** là bước U3, sau khi hàng rào và vai tài liệu đã có.
10. Cần chủ dự án chốt **6 câu "luật chơi"** (§11) — đề xuất đưa luôn 6 câu này + Q3/Q7/Q15 của v2 vào phiên uỷ quyền.

---

## 1. Khảo sát: dây chuyền đang dừng ở đâu

### 1.1. Sáu loại điểm dừng

| # | Điểm dừng | Bằng chứng (đo 17/11/2026) | Ai/cái gì gỡ được |
|---|---|---|---|
| **S1** | **Câu hỏi quyết định** (thiết kế, nghiệp vụ, thứ tự việc) | 159 câu `Open` trong §15 của 18 tài liệu WP-14 (§1.2). `D-69`, `D-70`, `D-71` (14/11) đều là "chủ dự án chốt", cả ba **đồng ý đúng khuyến nghị** của agent | Hồ sơ uỷ quyền + DEC (§4, §5) |
| **S2** | **Hết phiên ⇒ người dán câu mở phiên** | `08-session-protocol.md` §5.1: mỗi phiên kết thúc bằng khối A/B để **người** copy sang phiên mới — 21 phiên `N1…N21` = 21 lần. Bên `SelfLearningAI` cũng vậy: `bang-hang-muc.md` "Dán đúng một dòng" | Bộ chạy đọc khối A/B từ handover (§7) |
| **S3** | **Hỏi quyền công cụ** (permission prompt) | Chế độ mặc định hỏi trước lệnh chưa có trong allow-list; `claude -p` mặc định **từ chối** lệnh cần duyệt | Auto mode + allow/deny + hook (§7.3) |
| **S4** | **Cổng người**: duyệt tài liệu (G1), merge PR, phát hành | 27/55 tài liệu đã duyệt; `D-71`: GĐ3 **không được code** tới khi 18 tài liệu được duyệt; PR #1…#7 do người merge | Duyệt lô không chặn + (tuỳ chọn) tạm duyệt + nhánh tích hợp (§8) |
| **S5** | **Hết hạn mức** 5 giờ / tuần (gói Pro) | Hạn mức dùng chung chat + Claude Code | Tự chờ tới khi hạn mức hồi rồi chạy tiếp (§7.4) |
| **S6** | **Lỗi môi trường / bị chặn kỹ thuật** | `MSB3491` (quyền `obj\`), *dubious ownership*, cổng bận, Docker chưa lên | Ghi `blocked`, chuyển việc khác; chỉ dừng hẳn khi **không còn việc chạy được** (§7.5) |

> Không đếm được số lần **hỏi giữa phiên trong khung chat** — hội thoại không được lưu vào repo. Số đo S1 chỉ lấy từ tài liệu.

### 1.2. 159 câu hỏi mở của WP-14 — ai phải trả lời

Đo bằng script đọc bảng `### 15.1 Câu hỏi mở` (cột "Cần ai", "Trạng thái") của 18 tài liệu; phân loại theo từ khoá nên là **ước lượng**
🔶 — phiên chuẩn bị (U2) sẽ phân loại lại từng câu.

| Nhóm (theo cột "Cần ai") | Số câu | Ví dụ thật | Ai quyết được |
|---|---|---|---|
| Kỹ thuật thuần (Kiến trúc / Dev / Ops) | ~38 | `SHP-003` Q5: tiền tố khoá lock do module sinh hay `KeyPrefix` lo | **DEC** (tiền lệ + `tech-defaults.md`) |
| Nghiệp vụ / vận hành (PO, CSKH, kinh doanh; có thể kèm kỹ thuật) | ~46 | Trạng thái nào coi là "đã giao"; chính sách đổi trả | **Chủ dự án — qua chính sách**, DEC áp dụng |
| Kế toán (có thể kèm PO/kỹ thuật) | ~42 | `SHP-003` Q6: ngưỡng duyệt đổi COD, trần số lần/ngày | **Chủ dự án hoặc kế toán** (§11 M4) |
| Phụ thuộc NCC (GHN, GHTK, Zalo…) | ~27 | `SHP-003` Q2: endpoint nào của GHN thật sự bắt OTP — tài liệu ghi "cấm kết luận khi chưa gọi thử" | **Không ai quyết thay được** — cần gọi thử sandbox hoặc liên hệ hãng |
| Pháp lý | ~3 | — | Người (vùng cấm DEC) |
| Khác / chưa rõ | ~3 | — | phân loại lại ở U2 |

Phân theo tài liệu: 7–13 câu mỗi tài liệu; nhiều nhất `NOT-001` (13), `PRO-001` (11).

### 1.3. Đã có gì để dựa vào

- `decider` + `decision-policy`: ma trận **tin cậy × chi phí lùi**, **vùng cấm 7 mục**, 3 kết quả `TIEN_LE` / `TAM_CHOT` / `CHUYEN_NGUOI`,
  `continueWhileWaiting`, ghi `D-xx` + hàng đợi xem lại.
- Nguồn `SelfLearningAI`: ~103 mục quyết định, chỉ **6 mục tầng C** từng chặn việc (`C-01…C-06`: NCC SMS, staging, AI service, DPIA,
  cổng thanh toán, schema phá huỷ) ⇒ mô hình uỷ quyền theo tính đảo ngược **chạy được** ở quy mô 46 WP. Nhưng nguồn **không** giải quyết
  S2 (người vẫn mở từng phiên) và chưa có "phỏng vấn trước".
- Claude Code (tài liệu chính thức, 17/11): auto mode có trên **mọi gói kể cả Pro**; `/goal` giữ phiên chạy tới khi điều kiện đạt;
  `claude -p` chạy không tương tác, dùng được skill gọi tay và hook của `.claude/settings.json`; Stop hook quyết định phiên có được kết
  thúc hay không (nguồn §13).

---

## 2. Mục tiêu và nguyên tắc

**Mục tiêu đo được:** sau phiên uỷ quyền, dây chuyền chạy **hết WP-14 đợt A** mà **không** cần người can thiệp giữa chừng, trừ: (a) các
mục bị chặn bởi câu NCC / vùng cấm, (b) dừng khẩn do hàng rào. Chủ dự án xem báo cáo + duyệt lô **khi rảnh**.

| # | Nguyên tắc |
|---|---|
| N1 | **Hỏi trước, gom một lần**: mọi câu đoán trước được phải được hỏi ở phiên uỷ quyền, không hỏi lẻ giữa đường |
| N2 | **Không câu hỏi nào được dừng cả dây chuyền**: câu chưa có đáp án chỉ chặn đúng mục việc phụ thuộc nó |
| N3 | **Người duyệt theo ngoại lệ**: agent đưa đáp án khuyến nghị cho mọi câu; người chỉ sửa dòng không đồng ý |
| N4 | **Mọi quyết định thay người đều có log + cách lùi + hạn xem lại**; quyết định tạm **chặn phát hành** tới khi được duyệt |
| N5 | **Tự chạy không bỏ cổng máy**: G3/G4/G6 vẫn là exit code; REV/DOCREV vẫn ngữ cảnh sạch |
| N6 | **Vùng cấm giữ nguyên**: merge `main`, dữ liệu thật, tiền thật, secret, hợp đồng/NCC, phạm vi ngoài lộ trình |
| N7 | **Luôn dừng được ngay**: một tệp "phanh" hoặc Ctrl+C dừng bộ chạy ở điểm xanh gần nhất |

---

## 3. Kiến trúc: năm thành phần

```text
                 ┌────────────────── Chủ dự án ───────────────────┐
                 │ ① Phiên uỷ quyền (1 lần)   ⑤ /duyet-lo (khi rảnh) │
                 └──────────┬─────────────────────────▲───────────┘
                            ▼                         │ báo cáo chạy
  K1 Hồ sơ uỷ quyền  ──►  K2 DEC v2  ──ghi──►  decisions.md · hang-doi-xem-lai.md
  (chính sách + đáp án     (tiêu chí số 0 =          │
   trả lời trước)           hồ sơ uỷ quyền)          │
                            ▲  câu hỏi               │
                            │                        │
  K3 Hàng việc  ──chọn mục chạy được──►  K4 Bộ chạy ─┴─► mỗi mục = 1 phiên `claude -p` mới
  (WP, tài liệu, feature,                 (auto mode, Stop hook giữ tới DoD,
   phụ thuộc, chờ D-xx)                    tự chờ khi hết hạn mức, phanh khẩn)
                            ▲                        │
                            └── cập nhật trạng thái ─┘   K5 Báo cáo + duyệt lô
```

---

## 4. K1 — Phiên uỷ quyền và Hồ sơ uỷ quyền

### 4.1. Chuẩn bị (agent làm, không cần người — bước U2)

1. **Gom câu hỏi** từ: §15 của mọi tài liệu trong phạm vi lộ trình đã duyệt (WP-14 trước, sau đó GĐ3 code) · vùng cấm `decision-policy`
   §3 · câu hỏi còn treo của v2 (Q3, Q7, Q15) · các mục "Chờ người" cũ. Đọc theo mục (chỉ §15), không đọc toàn văn.
2. **DEC soạn đáp án khuyến nghị cho từng câu** + độ tin cậy + chi phí lùi, rồi xếp vào 4 rổ:

| Rổ | Điều kiện | Hỏi người thế nào |
|---|---|---|
| **R0 — Không hỏi** | Có tiền lệ `D-xx` / `tech-defaults` (kết quả `TIEN_LE`), hoặc kỹ thuật thuần, lùi rẻ | Không hỏi; liệt kê trong bảng phụ lục để người **có thể** xem |
| **R1 — Hỏi chính sách** | Nhiều câu cùng một "khẩu vị" (ví dụ: mọi ngưỡng duyệt tiền, mọi câu "mặc định theo Nhanh hay theo KiotViet") | **Một câu chính sách** trả lời cho cả nhóm (§4.3) |
| **R2 — Duyệt theo ngoại lệ** | Câu nghiệp vụ/kế toán cụ thể, DEC đã có khuyến nghị tin cậy TRUNG BÌNH trở lên | Bảng "đồng ý tất cả, trừ dòng…" |
| **R3 — Không ai quyết được ngay** | Phụ thuộc NCC/pháp lý/hợp đồng | Không hỏi đáp án; chỉ hỏi **ai lo và khi nào**; dây chuyền ghi `continueWhileWaiting` (adapter + cấu hình + fail-closed) |

3. Sinh **bản câu hỏi** `docs/agents/<ngày>-phien-uy-quyen-cau-hoi` (một tệp, có số câu mỗi rổ).

### 4.2. Phiên với chủ dự án (bước U3, ~1–2 giờ, một lần)

- Chạy trong Claude Code bằng công cụ hỏi nhiều lựa chọn (4 câu/lượt, mỗi câu có khuyến nghị đứng đầu, luôn có ô tự nhập) ⇒ ~6–8 lượt cho R1;
  sau đó một bảng R2 để người đánh dấu ngoại lệ; cuối cùng R3 (ai lo, khi nào).
- Mọi câu trả lời được ghi **ngay trong phiên** thành `D-xx` trạng thái **`ĐÃ CHỐT (người, phiên uỷ quyền <ngày>)`** — DEC phiên sau gặp lại
  câu đó sẽ trả `TIEN_LE`, không hỏi lại.
- Kết thúc phiên: in **tóm tắt hồ sơ** (≤ 1 trang) để chủ dự án đọc lại và xác nhận một lần.

### 4.3. Bộ câu hỏi chính sách mẫu (R1 — bản nháp, U2 sẽ hoàn thiện theo câu thật)

| Nhóm | Câu hỏi chính sách | Lựa chọn gợi ý |
|---|---|---|
| Ưu tiên | Khi tiến độ trễ, cắt gì trước? | cắt tính năng phụ trong WP · lùi WP sau · giữ phạm vi, chấp nhận trễ |
| Ưu tiên | Đúng-chắc hay nhanh khi hai phương án ngang nhau? | luôn chọn rẻ nhất để lùi (hiện là tiêu chí T8) · chọn nhanh hơn nếu lùi < 1 phiên |
| Mô hình nghiệp vụ | Khi tài liệu mơ hồ, "chuẩn" là ai? | Nhanh.vn → KiotViet/Sapo → thông lệ (thứ tự hiện tại) · khác |
| Tiền | Ngưỡng tiền mặc định cho mọi luồng "cần duyệt" (đổi COD, hoàn tiền, điều chỉnh công nợ) khi chưa có số của kế toán | dùng một ngưỡng chung có cấu hình, chặn phát hành tới khi kế toán duyệt · để trống = luôn cần duyệt (fail-closed) |
| Tiền | Làm tròn, chiết khấu chồng nhau, thứ tự áp khuyến mại — theo `XC-001` + Nhanh, hay chờ kế toán? | theo `XC-001` + Nhanh, tạm chốt chặn phát hành · chờ kế toán |
| Kế toán | Câu hỏi kế toán (~42) do ai trả lời? | chủ dự án · chuyển kế toán (gom một lô) · DEC tạm chốt theo thông lệ, kế toán duyệt trước phát hành |
| NCC | Ai liên hệ GHN/GHTK/Zalo và khi nào? Agent có được gọi **sandbox** (không phải production) không? | được gọi sandbox với tài khoản test người cấp · không, chỉ đọc tài liệu |
| Duyệt tài liệu | Tài liệu `DOC-OK` của DOCREV có được **tạm duyệt** để bắt đầu code trên nhánh (không merge `main`)? | có, trừ danh sách đỏ · không, giữ `D-71`/§A.5 (người duyệt từng đợt) |
| Git | Agent có được merge vào **nhánh tích hợp** khi cổng máy xanh? | có (§8.2) · không, mọi merge do người |
| Thư viện | Agent có được thêm gói NuGet/npm mới? | có nếu license MIT/Apache + ghi `D-xx` · không |
| Phạm vi | Phát hiện việc thiếu trong lộ trình thì sao? | ghi vào hàng đợi, không làm (hiện tại) · làm nếu < 0,5 phiên và trong cùng module |
| Ngân sách | Bật usage credits khi hết hạn mức? Trần bao nhiêu/tháng? | không — chờ hạn mức hồi · có, trần X |
| Lịch chạy | Bộ chạy được chạy khi nào? | bất cứ lúc nào máy bật · chỉ ngoài giờ làm của chủ dự án (để hạn mức chat còn) |
| Báo cáo | Báo cáo bao lâu một lần, ở đâu? | tệp mỗi ngày · mỗi khi dừng · cả hai |
| Treo | Câu chuyển người quá bao lâu chưa trả lời thì DEC được tạm chốt? | không bao giờ (vùng cấm) · sau N ngày với việc đảo ngược được |
| v2 | Q3 (pilot), Q7 (danh sách đỏ), Q15 (cấm `dotnet ef database update`) | như v2 §13 |

### 4.4. Tệp kết quả

| Tệp | Nội dung | Ai được sửa |
|---|---|---|
| `.claude/skills/decision-policy/references/uy-quyen-chu-du-an.md` | **Hồ sơ uỷ quyền**: mục tiêu, thứ tự ưu tiên, khẩu vị rủi ro, câu trả lời chính sách R1, uỷ quyền thường trực (§11 M2–M6), những điều tuyệt đối không, ngày hiệu lực, lịch xem lại | **Chỉ người** (qua `/duyet-lo` hoặc phiên uỷ quyền bổ sung). Hook `guard-write-scope` chặn mọi vai — kể cả DEC |
| `docs/nhanh/decisions.md` | Mỗi câu R1/R2/R3 đã trả lời ⇒ một `D-xx` `ĐÃ CHỐT (người, phiên uỷ quyền)` | DEC thêm dòng; không sửa dòng cũ |
| `docs/agents/hang-doi-xem-lai.md` | Câu R3 + câu người chưa trả lời ⇒ "Chuyển người" kèm "ai lo, khi nào" | DEC thêm; người chuyển sang "Đã xử lý" |
| `.claude/skills/decision-policy/SKILL.md` | §5 thêm **T0 = Hồ sơ uỷ quyền** đứng trước T1 (tiền lệ) · §3 vùng cấm ghi rõ mục nào đã được uỷ quyền có điều kiện | Sửa một lần ở U3, người duyệt |

---

## 5. K2 — DEC v2

| Thay đổi | Chi tiết |
|---|---|
| Thứ tự đọc | Hồ sơ uỷ quyền **trước** `decisions.md`. Câu đã có trong hồ sơ hoặc `D-xx` của phiên uỷ quyền ⇒ `TIEN_LE`, **không** tạo `D-xx` mới |
| Gắn mục bị chặn | JSON trả về thêm `blocks: [<id mục hàng việc>]` khi `CHUYEN_NGUOI` ⇒ bộ chạy đánh dấu đúng các mục đó `waiting`, các mục khác chạy tiếp |
| Gom câu | Bộ chạy gom mọi câu của **một mục việc** gửi DEC **một lần** (nhiều câu trong một lượt gọi) — giảm số lượt Opus |
| Câu treo | Áp luật "Treo" của hồ sơ (§4.3): mặc định **không** tự chốt câu vùng cấm dù treo lâu |
| Tự kiểm | Mỗi `TAM_CHOT` có `confidence`; tuần đầu chủ dự án duyệt lô ⇒ đo **tỉ lệ bị đảo** (mục tiêu < 20%); vượt ⇒ hạ quyền DEC về "chỉ `TIEN_LE`" cho nhóm câu đó (ghi `D-xx`) |
| Không đổi | Vùng cấm, ma trận quyền, cách ghi `D-xx`, quyền ghi đúng 2 tệp |

---

## 6. K3 — Hàng việc máy đọc

`agents/state/hang-viec.json` (commit — chỉ **một** bộ chạy ghi, không cần claim/heartbeat như `wp.ps1` của nguồn):

```json
{
  "version": 1,
  "phanh": false,
  "items": [
    { "id": "WP14-A-ADDR-001", "wp": "WP-14", "loai": "doc-gate", "ma": "ADDR-001",
      "dependsOn": [], "waitingOn": [], "status": "ready", "attempts": 0,
      "dod": "check-feature-doc -WP14 exit 0; docreview DOC-OK, P1=0; kiem-link PASS",
      "danhSachDo": false },
    { "id": "WP14-A-SAL-006", "wp": "WP-14", "loai": "doc-gate", "ma": "SAL-006",
      "dependsOn": [], "waitingOn": ["D-7x"], "status": "waiting", "danhSachDo": true },
    { "id": "WP15-...", "wp": "WP-15", "loai": "feature", "dependsOn": ["WP14-*"], "status": "blocked-by-gate" }
  ]
}
```

- **Nguồn sinh:** bảng đợt của `07` §WP-14 (và `§WP-15+` khi có). Thêm/bớt mục ngoài lộ trình = vùng cấm (giữ luật hiện tại).
- **Trạng thái:** `ready` · `running` · `done` · `waiting` (chờ `D-xx` của người) · `blocked` (lỗi kỹ thuật, kèm lý do) · `blocked-by-gate`
  (chờ G1/merge của người) · `failed` (quá trần làm lại).
- **Chọn mục:** mục `ready` đầu tiên có `dependsOn` đã `done`; ưu tiên mục cùng WP đang dở.
- `07-ke-hoach-trien-khai.md` và handover vẫn là **nguồn sự thật cho người đọc**; hàng việc là bản **máy dùng**, bộ chạy đồng bộ ngược
  bảng tiến độ `07` §13 khi một WP xong.

---

## 7. K4 — Bộ chạy tự động

### 7.1. Ba phương án

| Phương án | Cách chạy | Được | Mất |
|---|---|---|---|
| **A — `/goal` trong một phiên** | Chủ dự án mở một phiên, bật auto mode, gõ `/goal hàng việc đợt A không còn mục ready … hoặc dừng sau N lượt` | Có sẵn, không viết script; tự chạy tiếp khi hạn mức hồi (phiên tương tác) | **Một** ngữ cảnh cho mọi tài liệu ⇒ nén ngữ cảnh, chất lượng giảm dần (bài học Y8 của nguồn: 4/5 lỗi ở 1/3 cuối phiên); trái luật "một phiên một WP" |
| **B — bộ chạy ngoài, mỗi mục một phiên** | `tools/agents/chay-tu-dong.ps1` lặp: chọn mục → `claude -p "/chay-mot-viec <id>" --permission-mode auto --permission-prompts none --output-format json` → đọc JSON + chạy script kiểm → cập nhật hàng việc → mục kế | Ngữ cảnh **sạch** mỗi mục; khớp `08` (một phiên một đơn vị); thay hẳn việc dán khối A/B (S2); dễ dừng/tiếp | Phải viết + kiểm script; `-p` không có người ⇒ mọi lệnh cần duyệt bị **từ chối** (phải có allow-list đủ) |
| **C — kết hợp (đề xuất)** | B làm khung; **trong** mỗi mục, skill `/chay-mot-viec` chạy dây chuyền (`/doc-gate` hoặc `/feature`) và một **Stop hook** không cho phiên kết thúc khi DoD chưa đạt mà cũng chưa ghi `blocked` (trần 3 lần chặn) | Có cả hai: ngữ cảnh sạch giữa các mục + không bỏ dở trong một mục | Nhiều thành phần nhất — bù lại mỗi phần nhỏ, kiểm riêng được |

**Đề xuất C.** Phương án A giữ làm **dự phòng** khi chủ dự án muốn ngồi xem một đợt ngắn.

### 7.2. Vòng lặp của bộ chạy (C)

```text
lặp:
  nếu tệp phanh `agents/state/DUNG` tồn tại hoặc hang-viec.phanh = true  → dừng ở đây, ghi báo cáo
  mục = mục ready đầu tiên                                               → không còn ⇒ báo cáo "hết việc chạy được", dừng
  đánh dấu running; ghi giờ bắt đầu
  chạy: claude -p "/chay-mot-viec <id>" --permission-mode auto --permission-prompts none
        --output-format json  (KHÔNG dùng --bare: chế độ đó bỏ đăng nhập gói Pro, cần API key)
  đọc JSON kết quả (khối trạng thái cuối của skill) + TỰ chạy script DoD của mục (không tin lời agent)
  ├─ DoD đạt                   → done; commit theo luật git của CLAUDE.md §8 trên nhánh của WP
  ├─ chờ người (DEC CHUYEN_NGUOI) → waiting + waitingOn; mục khác chạy tiếp
  ├─ lỗi kỹ thuật               → blocked + lý do; attempts+1; quá 2 ⇒ failed
  └─ hết hạn mức (lỗi giới hạn) → ngủ tới giờ hồi in trong thông báo, chạy lại đúng mục
  cầu dao: 2 mục liên tiếp blocked cùng nguyên nhân, hoặc 1 lần hàng rào báo ghi ngoài vùng ⇒ dừng hẳn, báo cáo
  ghi báo cáo chạy (K5)
```

### 7.3. Quyền công cụ khi không có người

- `--permission-mode auto`: bộ phân loại duyệt thay người; luật allow/deny và **hook PreToolUse** của ta xét **trước** bộ phân loại.
- `--permission-prompts none`: không gì chờ câu trả lời; lệnh cần duyệt mà không luật nào cho ⇒ bị từ chối, agent được báo không thử lại.
- `permissions.allow` liệt kê sẵn lệnh cổng (`pwsh -File tools/*.ps1`, `dotnet build/test`, `pnpm lint/typecheck/test/build`,
  `npm --prefix tools run check:mermaid`, `git` đọc + `git add <đường dẫn>`/`commit` trên nhánh WP) để bộ phân loại không chặn nhầm.
- Hàng rào của v2 §7 (vùng ghi theo vai, shell chỉ đọc cho REV/DOCREV/DEC, `neverWrite`) **bắt buộc có trước** khi chạy tự động — đây là
  lý do U0 đứng đầu lộ trình.

### 7.4. Hết hạn mức gói Pro

- Bộ chạy B/C: khi phiên `-p` kết thúc vì giới hạn 5 giờ ⇒ đọc giờ hồi từ thông báo lỗi, ngủ, chạy lại đúng mục (mục chưa `done` nên an toàn).
  🔶 Cần đo ở U4: định dạng thông báo lỗi giới hạn trong JSON của `-p` trên phiên bản đang cài.
- Phương án A: phiên tương tác có chế độ **chờ hạn mức hồi rồi tự làm tiếp**; `/goal` tạm dừng và chạy tiếp khi hạn mức hồi.
- Hạn mức **tuần** là trần cứng của lượng việc: bộ chạy ghi % đã dùng vào báo cáo; còn < 10% hạn mức tuần ⇒ chỉ chạy mục `danhSachDo = false`.

### 7.5. Trong một mục: skill `/chay-mot-viec <id>`

1. Đọc mục trong hàng việc + handover gần nhất + hồ sơ uỷ quyền (tóm tắt, không toàn văn).
2. Gọi đúng dây chuyền: `loai = doc-gate` ⇒ quy trình v2 §9.1; `loai = feature` ⇒ v2 §9.2.
3. Mọi câu hỏi phát sinh ⇒ **gom** ⇒ gọi DEC **một lần** ⇒ áp dụng ⇒ chạy tiếp. Không bao giờ gọi công cụ hỏi người (đã bị gỡ ở `-p` bằng
   `--permission-prompts none`).
4. Kết thúc bằng **handover tự động** (7 mục của `08` §5) + khối trạng thái máy đọc (`{id, status, dodExit, waitingOn, blockedReason}`)
   thay cho khối A/B dán tay.
5. **Stop hook** `giu-den-dod.mjs`: nếu chưa có khối trạng thái cuối ⇒ chặn kết thúc, nhắc chạy script DoD hoặc ghi `blocked`; tối đa 3 lần
   (tránh vòng lặp — tài liệu Claude Code cũng có trần chặn Stop hook).

---

## 8. K5 — Cổng người không chặn, báo cáo và duyệt lô

### 8.1. Ba cổng người còn lại

| Cổng | Hiện tại | Đề xuất |
|---|---|---|
| **G1 duyệt tài liệu** | `D-71`: người duyệt theo đợt, GĐ3 không code trước khi duyệt | **Giữ** cho danh sách đỏ. Còn lại: **tuỳ chủ dự án chọn** (§11 M2) — (a) giữ nguyên: mục code ở trạng thái `blocked-by-gate`, bộ chạy làm việc khác; (b) **tạm duyệt**: `DOC-OK` + DEC `TAM_DUYET` ⇒ được code trên nhánh tích hợp, **chặn merge `main`** tới khi người ký biên bản |
| **Merge PR** | người merge vào `main` | `main` **giữ** cho người (vùng cấm). Đề xuất (§11 M3) **nhánh tích hợp** `tich-hop/<giai-doan>`: agent merge PR của từng WP vào đó khi G3–G6 xanh; người merge `tich-hop` → `main` theo lô |
| **Phát hành** | người | Giữ. Chặn khi `hang-doi-xem-lai.md` còn dòng `Xem lại trước = release` (luật hiện có) |

### 8.2. Vì sao cần nhánh tích hợp

Không có nó, WP sau phải chờ người merge WP trước (hoặc chồng nhánh lên nhánh chưa merge — dễ lệch, khó review). Nhánh tích hợp giữ được
**`main` chỉ người merge**, mà dây chuyền vẫn chạy nối tiếp. Rủi ro: lỗi tích lũy trên `tich-hop` ⇒ chặn bằng: CI chạy trên `tich-hop`
như trên `main`, và cầu dao "CI đỏ trên `tich-hop` ⇒ dừng bộ chạy".

### 8.3. Báo cáo chạy và `/duyet-lo`

- **Báo cáo chạy** `docs/agents/bao-cao-chay/<ngày>` (một tệp mỗi ngày, bộ chạy ghi): mục đã xong + số đo DoD · `D-xx` DEC đã ghi (tạm chốt /
  chuyển người) · mục `waiting`/`blocked` + lý do · % hạn mức 5 giờ / tuần đã dùng · lần hàng rào chặn · câu hỏi cho người (gom).
- **`/duyet-lo`** (skill gọi tay của chủ dự án): hiện bảng quyết định tạm chưa duyệt, sắp theo "xem lại trước"; người trả lời theo ngoại lệ
  ("đồng ý hết trừ D-75, D-81: …") ⇒ agent đổi trạng thái `ĐÃ CHỐT`, tạo `D-yy (thay thế D-xx)` cho dòng bị đảo, thêm mục **làm lại** vào hàng
  việc theo `revert` đã ghi, cập nhật hồ sơ uỷ quyền nếu người muốn biến câu trả lời thành chính sách.

---

## 9. An toàn và giới hạn

| Rủi ro | Chặn bằng |
|---|---|
| Agent tự chạy làm hỏng ngoài vùng | Hàng rào v2 §7 bắt buộc trước U4; cầu dao dừng hẳn khi hàng rào báo ghi ngoài vùng |
| Quyết định tạm sai lan rộng | Mỗi `TAM_CHOT` có `revert`; duyệt lô tuần đầu; tỉ lệ đảo > 20% ⇒ thu hẹp quyền DEC cho nhóm câu đó |
| Câu NCC (~27) không ai trả lời | Không giả vờ trả lời: thiết kế adapter + cấu hình + fail-closed (`continueWhileWaiting`), mục phụ thuộc ở `waiting`; hồ sơ ghi "ai lo, khi nào" |
| Bộ phân loại auto mode chặn nhầm lệnh hợp lệ | `permissions.allow` cho lệnh cổng; auto mode tự tạm dừng sau 3 lần chặn liên tiếp / 20 lần ⇒ trong `-p` thành `blocked`, báo cáo |
| Chạy mãi không xong | Trần 2 lần làm lại mỗi vai, 2 lần mỗi mục; Stop hook trần 3; cầu dao 2 mục cùng lỗi |
| Hạn mức tuần cạn giữa đợt | Báo cáo %; < 10% ⇒ chỉ chạy mục không thuộc danh sách đỏ; tiếp tục khi tuần mới |
| Máy tắt / Docker chưa lên | Mục kế `blocked` (S6) ⇒ bộ chạy dừng sau cầu dao; chạy lại an toàn vì mục chưa `done` |
| Hồ sơ uỷ quyền cũ dần | Ngày hiệu lực + lịch xem lại; `/duyet-lo` có tuỳ chọn "nâng câu trả lời thành chính sách" |

**Nói thẳng ba điều:**

1. **Không thể "không bao giờ hỏi"**: merge `main`, tiền thật, hợp đồng, kết luận pháp lý, câu NCC vẫn cần người hoặc bên thứ ba — đề xuất này
   chỉ làm cho chúng **không chặn phần còn lại**.
2. **Tự chạy không tăng lượng việc trên gói Pro** — nó bỏ thời gian chờ người. Lượng việc mỗi tuần vẫn do hạn mức quyết; số đo U5 sẽ cho biết
   một tuần làm được bao nhiêu tài liệu/WP.
3. **Quyết định tạm có giá**: càng uỷ quyền rộng, phần làm lại khi người đảo càng lớn. Phiên uỷ quyền tốt (R1 đủ rộng) là cách giảm giá này.

---

## 10. Lộ trình (gắn vào B0–B7 của v2)

| Bước | Việc | DoD | Gắn với v2 |
|---|---|---|---|
| **U0** | Hàng rào: `settings.json`, `scopes.json`, hook vùng ghi/shell/SubagentStop + ca thử H1–H9; thêm `neverWrite` cho hồ sơ uỷ quyền | `node --test` xanh; ca thật REV bị chặn | B0 + B1 |
| **U1** | Vai tài liệu `ba`, `docrev`; DEC v2 (§5); skill `doc-gate`, `session-protocol` | `/agents` đủ; `test-cong-kiem.ps1` xanh | B2 |
| **U2** | **Chuẩn bị phiên uỷ quyền** (agent, không cần người): gom câu §15 của 18 tài liệu + vùng cấm + Q3/Q7/Q15; DEC đề xuất đáp án; xếp rổ R0–R3 | tệp câu hỏi có số câu mỗi rổ; mọi câu R2 có khuyến nghị | mới |
| **U3** | **Phiên uỷ quyền với chủ dự án** (~1–2 giờ, một lần) | hồ sơ uỷ quyền có chữ "người xác nhận <ngày>"; mỗi câu đã trả lời có `D-xx ĐÃ CHỐT`; R3 có người lo + hạn | mới |
| **U4** | Hàng việc + bộ chạy + skill `/chay-mot-viec` + Stop hook + báo cáo + `/duyet-lo`; **chạy khô** trên 1 mục giả (tài liệu thử trong thư mục tạm) | bộ chạy đi hết vòng: chọn → chạy → kiểm DoD → cập nhật → báo cáo; phanh dừng được; giả lập hết hạn mức chạy lại đúng mục | thay B3 thủ công |
| **U5** | **Chạy tự động WP-14 đợt A**: ngày đầu chủ dự án đọc báo cáo sau mỗi mục (L1); đạt ⇒ để chạy hết đợt (L2) | DoD WP-14 đợt A; báo cáo có số đo §12 của v2 + tỉ lệ câu không phải chờ người | B3 + B4 |
| **U6** | Vai code (v2 B5) + chạy tự động WP-15 với nhánh tích hợp (nếu M3 = có) | `-DayDu` 19/19 trên `tich-hop`; PR lô `tich-hop → main` chờ người | B5 + B6 |

**Ước lượng:** U0–U1 ≈ 2 phiên (đã có trong v2), U2 ≈ 1 phiên tự chạy, U3 ≈ 1–2 giờ của chủ dự án, U4 ≈ 1–2 phiên. 🔶 Chưa đo.

---

## 11. Sáu câu "luật chơi" cần chủ dự án chốt (đề xuất đưa vào đầu phiên uỷ quyền)

| # | Câu hỏi | Khuyến nghị |
|---|---|---|
| **M1** | Đồng ý mô hình **phiên uỷ quyền một lần + duyệt lô khi rảnh** thay cho hỏi lẻ giữa phiên? | Đồng ý |
| **M2** | Tài liệu `DOC-OK` **ngoài danh sách đỏ** có được DEC **tạm duyệt** để bắt đầu code (không merge `main` tới khi người ký)? Đây là **đổi luật** `D-71` / `implementation-plan.md` §A.5 | Có — nếu không, GĐ3 code sẽ chờ người duyệt từng đợt |
| **M3** | Agent có được merge PR vào **nhánh tích hợp** `tich-hop/<giai-doan>` khi cổng máy xanh? | Có; `main` vẫn chỉ người |
| **M4** | Câu hỏi kế toán (~42): anh/chị trả lời, chuyển kế toán theo lô, hay DEC tạm chốt theo thông lệ + kế toán duyệt trước phát hành? | DEC tạm chốt + kế toán duyệt lô trước phát hành |
| **M5** | Câu NCC (~27): ai liên hệ hãng; agent có được gọi **sandbox** bằng tài khoản test anh/chị cấp? | Có sandbox; production cấm |
| **M6** | Ngân sách: có bật usage credits khi hết hạn mức không; bộ chạy được chạy giờ nào? | Không bật credits; chạy bất cứ lúc nào máy bật, ưu tiên ngoài giờ anh/chị dùng chat |

---

## 12. Bảng đối chiếu: điểm dừng → cách gỡ → thành phần

| Điểm dừng | Trước | Sau |
|---|---|---|
| S1 câu hỏi | dừng phiên, hỏi người | hồ sơ uỷ quyền → DEC; câu vùng cấm chỉ chặn đúng mục |
| S2 hết phiên | người dán khối A/B | bộ chạy đọc khối trạng thái, mở phiên `-p` kế |
| S3 hỏi quyền | người bấm duyệt | auto mode + allow-list + hook |
| S4 cổng người | chờ duyệt mới làm tiếp | duyệt lô; (tuỳ M2/M3) tạm duyệt + nhánh tích hợp |
| S5 hạn mức | người quay lại gõ tiếp | bộ chạy ngủ tới giờ hồi |
| S6 lỗi môi trường | dừng | `blocked`, chuyển mục khác; cầu dao khi lặp |

---

## 13. Nguồn

- Claude Code — `/goal` (giữ phiên chạy tới khi điều kiện đạt; chạy được với `-p`; tạm dừng và chạy tiếp khi hạn mức hồi):
  <https://code.claude.com/docs/en/goal>
- Claude Code — Permission modes (auto mode có trên mọi gói kể cả Pro; `dontAsk`; khuyến nghị cho chạy không người):
  <https://code.claude.com/docs/en/permission-modes>
- Claude Code — Chạy không tương tác `claude -p` (`--permission-mode`, `--permission-prompts none`, `--output-format json`,
  `--bare` bỏ đăng nhập gói, skill gọi tay dùng được trong `-p`): <https://code.claude.com/docs/en/headless>
- Claude Code — Hooks (Stop/SubagentStop chặn được việc kết thúc; `agent_type`): <https://code.claude.com/docs/en/hooks>
- Claude Code — Manage costs (chờ hạn mức hồi rồi chạy tiếp; subagent tính vào hạn mức): <https://code.claude.com/docs/en/costs>
- Trong repo: [ke-hoach-v2-claude-code-pro.md](ke-hoach-v2-claude-code-pro.md) · [ke-hoach-he-thong-agent.md](ke-hoach-he-thong-agent.md) §13 ·
  [hang-doi-xem-lai.md](hang-doi-xem-lai.md) · [08-session-protocol.md](../nhanh/08-session-protocol.md) §5.1 ·
  [07-ke-hoach-trien-khai.md](../nhanh/07-ke-hoach-trien-khai.md) §WP-14 · [decisions.md](../nhanh/decisions.md) (`D-69`…`D-71`)
- Nguồn DeepSeek: `SelfLearningAI/docs/09-process/decisions.md` (mục "Chờ người xem lại", `C-01…C-06`), `bang-hang-muc.md`.
