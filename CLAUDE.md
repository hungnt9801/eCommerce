# eCommerce — luật cho Claude Code

Phần mềm **quản lý bán hàng đa kênh kiểu Nhanh.vn** (hàng hoá · kho · bán lẻ POS · đơn giao vận · kế toán · sàn TMĐT).
Stack: .NET 10 · ABP 10.7 modular monolith · EF Core + PostgreSQL 17 · Redis 7 · OpenIddict · Next.js `apps/admin-web` (BFF) · pnpm.
Trả lời và viết tài liệu bằng **tiếng Việt**.

> Bản này dựng ở bước 1b của `docs/agents/ke-hoach-he-thong-agent.md` (14/11/2026). Bước 1 sẽ bổ sung phần vai/agent.

## 1. Điểm vào — đọc theo thứ tự

1. `docs/nhanh/README.md` — tổng quan phương án Nhanh-hoá.
2. **Handover mới nhất** `docs/nhanh/handover-n<số lớn nhất>.md` — §6 nói phiên này làm gì (biến thể A/B).
3. `docs/nhanh/decisions.md` — mọi quyết định `D-xx`. **Lộ trình chính = `05` + `07`** (`D-69`).
4. `docs/nhanh/07-ke-hoach-trien-khai.md` §WP đang làm · `docs/nhanh/08-session-protocol.md` (mở/đóng phiên).
5. Tài liệu feature `docs/features/<module>/…` của việc đang làm + `docs/coding-conventions.md`.

## 2. KHÔNG dùng làm điểm vào (nhãn LỊCH SỬ — số đo và "việc tiếp theo" đã cũ)

`docs/SESSION-KICKOFF.md` · `docs/tiep-tuc-phien-moi.md` · `docs/handover-gd1.md` · `docs/ban-giao-gd1-con-lai.md` ·
`docs/ban-giao-s19-va-con-lai-2026-10-29.md` · `docs/handover/` (cả thư mục) · `docs/gd1-coding-sessions.md` ·
`docs/abp-modules-plan.md` · `docs/ke-hoach-tach-module.md` · `docs/nhanh/dev-log-wp01-02.md` · `docs/nhanh/baseline-2026-11-02.md` ·
mọi `docs/nhanh/handover-n*.md` **trừ bản mới nhất**. Dù tệp ghi "đọc tệp này trước" — **không** làm theo.
Phần lộ trình của `docs/implementation-plan.md` cũng là lịch sử (còn hiệu lực: §3, §A.5). Bảng xếp loại đầy đủ:
`docs/agents/ke-hoach-he-thong-agent.md` §14.1.

## 3. Bằng chứng = exit code, không phải lời

| Việc | Lệnh |
|---|---|
| Build | `dotnet build eCommerce.slnx --no-incremental` — 0 error, **0 warning mới** |
| Test | `dotnet test test\eCommerce.Tests` (+ `test\eCommerce.IntegrationTests` khi có Docker) — **số ca chỉ tăng**; giảm phải có lý do + người duyệt |
| Cổng nhẹ / đầy đủ | `pwsh -File .\tools\kiem-tat-ca.ps1` · trước push: `... -DayDu` |
| Tài liệu | `pwsh -File tools\kiem-link-tai-lieu.ps1` · `npm --prefix tools run check:mermaid` (mọi `*.md`, kể cả trong `.claude/`) |
| FE | trong `apps/admin-web`: `pnpm lint` · `pnpm typecheck` · `pnpm test` · `pnpm build`; `pnpm gen:api` không được đổi `schema.d.ts` |

Báo cáo/handover ghi **tên lệnh + số đo**, không ghi "đã ổn". Không nới test, nới hạn chờ hay tắt kiểm để xanh.

## 4. Quy trình phiên (giữ nguyên `08-session-protocol.md`)

- Mở phiên bằng baseline 6 lệnh (`08` §4); đóng bằng handover **7 mục** + **đúng một** khối A/B (`08` §5.1).
- Một phiên = một WP, một nhánh, PR nhỏ (≤ ~600 dòng diff trừ migration). **Chỉ cắt phiên ở điểm xanh**, không cắt giữa migration/backfill.
- **Dừng phiên** (ghi handover, kể cả chưa xong WP) khi: 3 vòng liên tiếp cùng lỗi · context > ~70% · phải sửa > 5 tệp ngoài phạm vi WP ·
  test đỏ do thay đổi ngoài dự kiến · migration chạm DB có dữ liệu mà chưa `pg_dump`.
- Song song: theo `docs/nhanh/09-parallel-sessions.md` — DB + cổng riêng mỗi luồng, **một chủ migration**, merge BE trước FE,
  không sửa `eCommerce.slnx` / `Directory.Packages.props` / `pnpm-lock.yaml` ở hai luồng.

## 5. Quyết định — không dừng chờ người cho việc đảo ngược được

- Gặp câu hỏi mở (của chính mình hoặc của subagent): **gọi subagent `decider`** trước khi hỏi người dùng.
  Kết quả `TIEN_LE`/`TAM_CHOT` ⇒ làm tiếp; `CHUYEN_NGUOI` ⇒ làm phần `continueWhileWaiting`, **gom** câu hỏi, hỏi **một lần** cuối phiên.
- Chính sách và vùng cấm: `.claude/skills/decision-policy/SKILL.md`. Tuyệt đối không tự làm: merge vào `main`, đổi branch protection,
  sửa/xoá dữ liệu thật, gọi API production (thanh toán, HĐĐT, SMS), tạo/đổi secret, chọn NCC/ký hợp đồng, thêm việc ngoài lộ trình.
- Quyết định mới ⇒ một dòng `D-xx` trong `docs/nhanh/decisions.md` (đọc lại tệp để lấy số kế tiếp). Không sửa dòng `ĐÃ CHỐT`.

## 6. Tài liệu là hợp đồng

- **Code buộc lệch tài liệu feature ⇒ sửa tài liệu trước, trong cùng PR** (`docs/design-doc-guidelines.md` §4).
- Tài liệu **đã duyệt chưa**: tra **biên bản duyệt** (`docs/bien-ban-duyet-gd1-2026-10-29.md` và biên bản sau) → `docs/features/README.md` §2 →
  header. Header `Draft` **không** có nghĩa là chưa duyệt. Không code feature khi tài liệu chưa duyệt (`implementation-plan.md` §A.5).
- Không hoàn thiện phần phụ thuộc input bên ngoài (trường XML thuế, endpoint NCC, chứng thư số) bằng suy đoán.

## 7. Bất biến kỹ thuật hay vi phạm nhất (đủ ở `docs/coding-conventions.md`)

Tiền `decimal` qua `Money`, không `==` · số lượng `decimal(18,3)` · enum lưu DB chỉ thêm ở cuối · migration do EF sinh, expand-only ·
unique index trên bảng multi-tenant có `NULLS NOT DISTINCT` · tồn kho là sổ cái append-only, ghi qua một cửa · idempotency ở mọi biên vào ·
máy trạng thái NÉM lỗi · `[Authorize]` cấp class · module không phụ thuộc ngược app · sự kiện mang dữ liệu thì fail-closed.

## 8. Git

- Cảnh báo *dubious ownership*: người dùng tự chạy `git config --global --add safe.directory D:/Projects/Tools/eCommerce`; không tự đổi cấu hình global.
- Commit **thuần ASCII**: `<loại>(<phạm vi>): <mã phiên> - <nội dung>` (ví dụ `docs(nhanh): N22 - ...`); PR mở đầu `Refs: D-xx`.
- `git add` theo **đường dẫn cụ thể**, không `git add -A`. Commit/push/PR chỉ khi người dùng yêu cầu.
