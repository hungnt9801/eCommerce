# Handover `N40` — WP-14: `P-08` chuẩn bị hồ sơ duyệt đợt A

> Phiên `N40` (lệnh `/chay-phien`, 22/11/2026). Nhánh `docs/n22-wp14-dot-a`. WP thuần tài liệu (`D-71`): 0 `.cs`, 0 migration; build/test bỏ qua có lý do "WP thuần tài liệu".

## 1. Đã xong — việc + số đo (kèm lệnh kiểm)

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/agents/check-feature-doc.ps1 -Path <tệp> -WP14` (9 tài liệu đợt A) | 0 ×9 | `KET QUA: DAT` cả 9; mục đối chiếu Nhanh có; 0 TODO; dòng `Open`: ADDR-001 12 · ADDR-002 8 · ADDR-003 12 · SHP-001 4 · SHP-002 6 · SHP-003 8 · SHP-004 5 · REC-003 12 · SAL-006 14 |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 0 tham chiếu chết |
| `npm --prefix tools run check:mermaid` | 0 | 211 khối, 0 lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | 38/38 pass |

Việc làm: đối chiếu 9 dòng của `docs/agents/nhap-bien-ban-gd3-a.md` với 9 `docs/reviews/<MÃ>-docreview.md` (verdict, số P1/P2/P3 khớp), thêm mục "Tóm tắt đợt A" (9/9 `DOC-OK`, 0 P1, 15 P2 mở, phân loại tạm duyệt / danh sách đỏ / có điều kiện, nợ chéo) và bảng "câu còn chờ người của đợt A" (10 dòng: R3-1…R3-5, Q2 `D-93`, Q17, Q10 SHP-002, lô 26 `D-xx` tạm chốt, việc BA sửa P2). Không gọi vai (không cần). Không có `D-xx` mới.

## 2. Chưa xong / cắt giữa

Không có việc dở của `P-08` (đạt DoD). Việc còn mở nằm ở bảng "Chờ người" của nháp và các P2 của BA (mục 5).

## 3. Migration đã áp

Không có (WP thuần tài liệu).

## 4. Quyết định mới

Không có `D-xx` mới trong phiên này.

## 5. Nợ / rủi ro

| # | Nợ | Chủ |
|---|---|---|
| 1 | 15 P2 của 9 tài liệu (SHP-001 #45; SHP-002 #1/#2; SHP-003 #1/#2; SHP-004 #1/#2; REC-003 #1/#2; ADDR-003 #1/#2; SAL-006 #1/#2; ADDR-001/002 mỗi bài 2 P2 đã sửa v0.5 nhưng DOCREV chưa kiểm lại) | BA từng tài liệu |
| 2 | Hook báo sai "thiếu khối JSON cuối" cho `ba`/`docrev`/`decider` (từ N39) | chủ dự án (`.claude/hooks`) |
| 3 | SQL/CHECK/trigger các tài liệu chưa đo — máy không có `psql`/docker | WP code |

**Chờ người** (chi tiết trong `docs/agents/nhap-bien-ban-gd3-a.md`, bảng cuối): R3-1 liên hệ GHTK · R3-2 sandbox GHN · R3-3 kế toán duyệt lô M4 · R3-4 file bảng kê COD · R3-5 pháp chế (`D-101`) · Q2 `D-93` · Q17 SAL-006 · Q10 SHP-002. Không chặn phiên kế.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: HOÀN THÀNH `P-08` → làm `P-09`** (duyệt đợt A — hàng của người/uỷ quyền `duyet-dec`; xem `PH next`).

**Khối B — mở phiên mới:**

1. Mở cửa sổ mới, chạy một phiên một lúc.

```text
/chay-phien
```

## 7. Trạng thái nhánh

- `docs/n22-wp14-dot-a`; SHA lấy từ `git log -1`; xem câu trả lời cuối về việc đã push chưa. Chưa PR.

---

**Hàng rào agent**

| Vai | Số lượt gọi | Ghi chú |
|---|---|---|
| ORCH | phiên hội thoại | — |
| `ba` / `docrev` / `decider` | 0 | P-08 chỉ tổng hợp, không cần vai |
