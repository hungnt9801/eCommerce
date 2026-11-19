# Handover `N29` — WP-14: `P-03` (`SHP-001`) mở lại nhưng BỊ CHẶN — phiên không có vai `ba`/`docrev`

> Phiên `N29` · 19/11/2026 · nhánh `docs/n22-wp14-dot-a` · `main` = `db82e4d`. Lệnh `/chay-phien P-03`.
> WP thuần tài liệu (`D-71`): 0 `.cs`, 0 migration; build/test bỏ qua có lý do "WP thuần tài liệu".

## 1. Đã xong — việc + số đo (kèm lệnh kiểm)

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/agents/phien.ps1 check` | 0 | bảng khớp registry |
| `pwsh -File tools/agents/phien.ps1 doisoat -Id P-03` | 1 | `SHP-001` docreview `DOC-OK`, handover-n28 §2 còn nhắc mã |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 0 tham chiếu chết |
| `npm --prefix tools run check:mermaid` | 0 | 212 khối, 0 lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | 34/34 pass |

Việc đã làm: đối soát `P-03`, `phien.ps1 start -Id P-03 -Session N29`. Không sửa tài liệu feature nào.

## 2. Chưa xong / cắt giữa

- `P-03` (`SHP-001`, `DOC-OK` từ `N25`): còn **BA áp quyết định R2 của người** (`D-114`/`D-115`, `D-108…D-110` đã `ĐÃ CHỐT`) vào tài liệu: đóng `SHP-001-Q8`, xét đủ `SHP-001-Q7` (§5 #6 handover-n28), các câu R2 khác của `SHP-001`; sau đó DOCREV vòng kiểm nhanh các mục đổi + D3/D4.
- **Nguyên nhân dừng:** phiên này chỉ đăng ký subagent `decider` (+ loại chung); `ba` và `docrev` có tệp ở `.claude/agents/` nhưng gọi `Agent` báo "Agent type 'ba' not found". ORCH không được tự sửa tài liệu feature, và không dùng `general-purpose` thay vai (qua mặt vùng ghi `.claude/scopes.json`).
- `P-04…P-07` còn ⬜.

## 3. Migration đã áp

Không có.

## 4. Quyết định mới

Không có `D-xx` mới trong phiên.

## 5. Nợ / rủi ro

| # | Nợ | Chủ |
|---|---|---|
| 1 | Vai `ba`/`docrev` không nạp được ở phiên chạy từ `/chay-phien`: kiểm `.claude/agents/ba.md`, `docrev.md` (frontmatter), thử khởi động lại Claude Code để nạp lại danh sách agent; có thể do mới cài đặt (`artifacts/cai-dat-agent`) sau khi phiên mở | chủ dự án / OPS |
| 2 | Các nợ §5 của handover-n28 vẫn nguyên (cổng merge `main`, nhánh tích hợp, sandbox GHN/GHTK, Setting kế toán, license Excel, `Extension_btree_gist_ton_tai`, mẫu `de-xuat…` §4.3 T6) | như n28 |

**Chờ người:** 1 câu — xem §6 câu trả lời cuối (khởi động lại để nạp `ba`/`docrev`).

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: DỞ DANG WP-14 → mở lại WP-14** (`P-03` còn áp R2 + kiểm nhanh).

1. Khởi động lại Claude Code (mở cửa sổ mới) để nạp `ba`/`docrev`; đóng `P-03`, rồi `P-04`.

```text
/chay-phien
```

## 7. Trạng thái nhánh

- `docs/n22-wp14-dot-a`; SHA lấy từ `git log -1`; xem câu trả lời cuối về việc đã push chưa. `main` = `db82e4d`. Chưa PR.

---

**Hàng rào agent** (chỉ lượt `N29`)

| Vai | Số lượt gọi | Ghi chú |
|---|---|---|
| ORCH | phiên hội thoại | — |
| `decider` / `ba` / `docrev` | 0 | `ba` không có trong danh sách agent của phiên (lỗi nạp) |
