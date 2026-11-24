# Khung `docs/ops/<MÃ>-deploy.md` (OPS — bước F6)

```markdown
# Triển khai `<MÃ>` — <ngày>

## 1. Phạm vi
- Nhánh/PR: `<nhánh>` · hợp đồng API: `<thay đổi>`
- Migration: `<tên migration>` (expand-only · đã `pg_dump` ở `<tệp>` nếu DB có dữ liệu)

## 2. Biến môi trường
| Tên | Mặc định dev | Bắt buộc production | Ghi chú |
|---|---|---|---|
| … | … | … | … |

## 3. Thứ tự triển khai
1. Áp migration bằng `DbMigrator` (binary mới build).
2. Deploy BE (API).
3. Deploy FE (sau khi API đã xanh).
4. Bật job/worker theo cấu hình (mặc định **tắt** nếu gọi dịch vụ ngoài).

## 4. Đường lùi (bắt buộc)
- Cờ tính năng: `<tên cờ>` (tắt ⇒ hành vi cũ).
- Migration: `<migration mới>` hoặc khôi phục từ `pg_dump <tệp>`.
- Rollback code: `git revert <sha>` (không revert migration đã áp lên DB có dữ liệu).

## 5. Cổng
- `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` → exit 0
- PR xanh 2 required check · `docker-build` xanh
- Kiểm sau deploy: `/api/health` 200 · 1 luồng nghiệp vụ chính
```

> Không ghi "đã ổn": ghi **lệnh + số đo** (exit code, số ca, thời gian).
