---
name: mo-phien
description: Mở một phiên làm việc eCommerce đúng docs/nhanh/08-session-protocol.md §4 — xác định WP từ handover mới nhất và D-xx mới hơn, tạo nhánh của WP, chạy baseline (bản đầy đủ cho WP có code, bản tài liệu cho WP thuần tài liệu), kiểm hàng rào hook, in bảng số đo để đưa vào handover §1. Gọi tay — /mo-phien <WP> <mã phiên>, ví dụ /mo-phien WP-14 N22.
argument-hint: "<WP-xx> <Nxx>"
disable-model-invocation: true
---

# /mo-phien — mở phiên

Tham số: `$ARGUMENTS` (WP, mã phiên). Thiếu ⇒ suy từ handover mới nhất + `D-xx` mới hơn nó; vẫn không rõ ⇒ gửi `decider` (CLAUDE.md §5).

## 1. Điểm vào (CLAUDE.md §1 — đọc theo mục, không toàn văn)

1. `docs/nhanh/handover-n<số lớn nhất>.md` — chỉ §2 (chưa xong), §5 (nợ), §6 (việc đầu tiên). Hook `session-context.mjs` đã báo tên tệp.
2. `docs/nhanh/decisions.md` — các dòng `D-xx` **mới hơn** handover (chúng thắng §6 của handover; ví dụ `D-71` thay câu mở phiên của `N21`).
3. `docs/nhanh/07-ke-hoach-trien-khai.md` — đúng mục `§<WP>` (phạm vi, DoD, ⛔ không thuộc WP).
4. Không đọc tệp nhãn LỊCH SỬ (CLAUDE.md §2).

## 2. Nhánh

Một WP một nhánh (CLAUDE.md §4). `git status --short` phải sạch hoặc chỉ có `docs/**` ⇒ `git switch -c <loại>/<nx>-<wp>-<mô tả ngắn ASCII>`
(ví dụ `docs/n22-wp14-dot-a`) hoặc `git switch` nhánh đã có của WP (biến thể A). Cây bẩn ngoài `docs/**` ⇒ dừng, báo người — không stash hộ.

## 3. Baseline — ghi **số**, không ghi "ổn"

Lệnh shell của Claude Code trên máy này chạy qua **Git Bash** ⇒ đường dẫn viết bằng `/` (Bash nuốt `\`: `.\tools\x.ps1` thành `.toolsx.ps1`).

| WP | Lệnh |
|---|---|
| **Có code** (`08` §4) | `docker compose up -d` · `dotnet build eCommerce.slnx --no-incremental` (0 error, ghi số warning) · `dotnet test test/eCommerce.Tests --no-build` (ghi số ca) · `pwsh -File tools/kiem-link-tai-lieu.ps1` |
| **Thuần tài liệu** (WP-14, `D-71`: 0 `.cs`) | `pwsh -File tools/kiem-link-tai-lieu.ps1` · `npm --prefix tools run check:mermaid` · build/test **bỏ qua có lý do** "WP thuần tài liệu" (build đang đỏ `MSB3491` trên máy dev — nhận chủ WP-15) |
| **Luôn chạy** | `node --test .claude/hooks/test/hooks.test.mjs` (hàng rào hook còn đúng) · `pwsh -File tools/test-cong-kiem.ps1 -ChiKiemMoc` khi WP sửa `tools/` |

Lệnh đỏ ở baseline mà **không** do phiên này gây ra ⇒ ghi số đo + chủ nợ; WP thuần tài liệu không bị chặn bởi build đỏ.

## 4. In ra (đưa vào handover §1 lúc đóng phiên)

```text
Phiên <Nx> · <WP> · nhánh <tên> · main=<sha ngắn>
| Lệnh | Exit | Số đo |
| kiem-link-tai-lieu.ps1 | 0 | PASS — <n> link, <m> đường dẫn |
| check:mermaid | 0 | 0 lỗi |
| hooks.test.mjs | 0 | <n>/<n> pass |
```

Rồi làm đúng WP: tài liệu ⇒ `/doc-gate <đợt|MÃ…>`; không mở rộng phạm vi.
