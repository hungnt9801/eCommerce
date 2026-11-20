# Hướng dẫn thiết lập chạy tự động từng phiên (WP-14)

> Mục đích: chủ dự án **không cần ngồi máy**. Bộ chạy `tools/agents/chay-tu-dong.ps1` tự mở phiên → chờ phiên xong → mở phiên kế,
> mỗi lần **một** phiên, cho tới khi hết việc của agent hoặc tới việc cần người.
> Bảng việc: [bang-phien-wp14.md](bang-phien-wp14.md) · Thiết kế: [de-xuat-uy-quyen-va-chay-tu-dong.md](de-xuat-uy-quyen-va-chay-tu-dong.md) §7 (phương án B).
> Cập nhật 20/11/2026.

## 1. Bộ chạy làm gì

```text
lặp:
  cây làm việc có thay đổi ngoài docs/** và agents/state/**? → dừng, in danh sách tệp + lệnh git add gợi ý (phiên sẽ từ chối chạy)
  có tệp phanh artifacts/agents/tu-dong/DUNG?              → dừng
  phien.ps1 next                                            → hết việc / chỉ còn việc của người ⇒ dừng
  hàng kế là phiên ngồi cùng (🗣)?                          → dừng, báo người
  claude -p "/chay-phien" --model sonnet --permission-mode auto --permission-prompts none --output-format json
      (một phiên Claude Code MỚI, ngữ cảnh sạch: mở phiên → /doc-gate → handover → cập nhật bảng → commit + push nhánh WP)
  hết hạn mức 5 giờ?                                        → ngủ tới giờ hồi + 3 phút, chạy lại đúng hàng đó
  hết hạn mức tuần?                                         → dừng
  2 phiên liền không tiến triển (bảng + handover không đổi)? → dừng (cầu dao, khỏi đốt hạn mức)
```

Mỗi phiên dùng **Sonnet** cho phiên chính; các vai phụ giữ model riêng (`decider`, `docrev`: Opus · `ba`: Sonnet).
Quyết định trong phiên do `decider` lấy theo [hồ sơ uỷ quyền](uy-quyen-chu-du-an.md); phiên không hỏi người.

## 2. Điều kiện trước (kiểm một lần)

| # | Điều kiện | Kiểm bằng |
|---|---|---|
| 1 | PowerShell 7 | `pwsh --version` ⇒ `7.x` |
| 2 | Claude Code trong PATH, đã đăng nhập gói Pro | `claude --version` (≥ 2.1.259 — cần cờ `--permission-prompts`) |
| 3 | Đã cài gói agent mới nhất | `pwsh -File artifacts/cai-dat-agent/cai-dat.ps1` ⇒ dòng cuối `node --test exit = 0` |
| 4 | Đủ vai `ba`, `docrev`, `decider` (frontmatter hợp lệ) | trong `cai-dat.ps1` ca `F1` phải pass |
| 5 | Cây làm việc sạch, đang ở nhánh WP-14 | `git status --short` rỗng (hoặc chỉ `docs/**`) · `git branch --show-current` = `docs/n22-wp14-dot-a` |
| 6 | `git push` không hỏi mật khẩu | `git push` một lần bằng tay — đã lưu thông tin đăng nhập |
| 7 | Không còn cửa sổ `/chay-phien` nào đang chạy | bảng không có hàng 🟨 của phiên tay đang mở |
| 8 | Máy không tự ngủ khi cắm điện | `powercfg /change standby-timeout-ac 0` (chạy một lần) |

## 3. Thiết lập lần đầu

1. Cài gói agent và commit phần hệ thống (chỉ khi `git status` thấy các tệp này thay đổi):

   ```powershell
   cd D:\Projects\Tools\eCommerce
   pwsh -File artifacts/cai-dat-agent/cai-dat.ps1
   git add -- tools/agents/phien.ps1 tools/agents/test-phien.ps1 tools/agents/chay-tu-dong.ps1 tools/agents/test-chay-tu-dong.ps1 docs/agents/bang-phien-wp14.md docs/agents/huong-dan-chay-tu-dong.md .claude/skills/chay-phien/SKILL.md .claude/settings.json
   git commit -m "chore(agents): bo chay tu dong + cap so phien bang script"
   ```

2. Kiểm ca thử của bộ chạy (không gọi Claude thật, ~1 phút):

   ```powershell
   pwsh -File tools/agents/test-chay-tu-dong.ps1    # TONG: 12 dat · 0 truot
   pwsh -File tools/agents/test-phien.ps1           # TONG: 38 dat · 0 truot
   ```

3. **Chạy thử đúng một phiên khi còn ngồi máy** — để thấy chế độ không cửa sổ có bị chặn lệnh nào không (ví dụ `git push`):

   ```powershell
   pwsh -File tools/agents/chay-tu-dong.ps1 -ToiDa 1
   ```

   Xong, mở `artifacts/agents/tu-dong/nhat-ky.md` và tệp `.json` mới nhất cùng thư mục. Đạt khi: bảng đổi trạng thái hàng vừa chạy,
   có `docs/nhanh/handover-n<số mới>.md`, `git log -1` là commit của phiên, `git status` sạch. Bị chặn lệnh ⇒ xem §8.

## 4. Chạy hằng ngày

Mở **một** cửa sổ PowerShell tại repo, chạy rồi để đó:

```powershell
cd D:\Projects\Tools\eCommerce
pwsh -File tools/agents/chay-tu-dong.ps1
```

| Tham số | Mặc định | Ý nghĩa |
|---|---|---|
| `-ToiDa` | 30 | số phiên tối đa trong một lần chạy |
| `-Model` | `sonnet` | model của phiên chính (các vai phụ không đổi) |
| `-GioToiDaMotPhien` | 6 | quá giờ ⇒ dừng tiến trình phiên đó; hàng ở lại 🟨, phiên sau làm tiếp |
| `-ChoKhongRoGiay` | 1800 | hết hạn mức mà không đọc được giờ hồi ⇒ ngủ 30 phút rồi thử lại |

Muốn bắt đầu vào một giờ cố định (ví dụ 22:00 tối nay) mà không ngồi máy — tuỳ chọn, dùng Task Scheduler của Windows
(chỉ chạy khi anh/chị đang đăng nhập Windows, để `claude` dùng được phiên đăng nhập gói Pro):

```powershell
schtasks /Create /TN "eCommerce-chay-tu-dong" /SC ONCE /ST 22:00 /TR "pwsh -NoProfile -File D:\Projects\Tools\eCommerce\tools\agents\chay-tu-dong.ps1"
schtasks /Delete /TN "eCommerce-chay-tu-dong" /F    # xoá lịch khi không cần
```

## 5. Theo dõi

| Xem gì | Ở đâu |
|---|---|
| Mỗi phiên một dòng: hàng, giờ, kết quả tóm tắt, lần ngủ chờ hạn mức | `artifacts/agents/tu-dong/nhat-ky.md` |
| Kết quả đầy đủ của từng phiên (JSON của `claude -p`) | `artifacts/agents/tu-dong/<giờ>-<hàng>.json` (+ `.err`) |
| Trạng thái từng hàng, bằng chứng | [bang-phien-wp14.md](bang-phien-wp14.md) §3 · hoặc `pwsh -File tools/agents/phien.ps1 status` |
| Việc phiên đã làm, câu chờ người | handover mới nhất `docs/nhanh/handover-n<số lớn nhất>.md` §1, §5, §6 |
| Quyết định `decider` đã chốt thay | [hang-doi-xem-lai.md](hang-doi-xem-lai.md) — duyệt theo lô khi rảnh |
| Commit | `git log --oneline -10` |

## 6. Dừng và chạy lại

- **Dừng êm** (sau khi phiên đang chạy xong): `New-Item artifacts/agents/tu-dong/DUNG` từ cửa sổ khác.
  Chạy lại: xoá tệp đó (`Remove-Item artifacts/agents/tu-dong/DUNG`) rồi chạy lại lệnh §4.
- **Dừng ngay:** `Ctrl+C` ở cửa sổ bộ chạy. Phiên đang làm bị cắt ⇒ hàng ở lại 🟨; lần chạy sau tự làm tiếp từ cây làm việc
  (không làm lại bước đã có tệp kết quả). Nếu báo "bộ chạy khác đang chạy" mà thật ra không còn: xoá `artifacts/agents/tu-dong/chay-tu-dong.lock`.

## 7. Khi bộ chạy dừng — mã thoát và việc cần làm

| Exit | Dòng cuối nhật ký | Nghĩa | Anh/chị làm gì |
|---|---|---|---|
| 0 | `het viec` / `tep phanh` / `da chay du N phien` | xong đúng ý | xem bảng; chạy lại nếu còn việc |
| 3 | `chi con viec cua nguoi` / `can nguoi ngoi cung` | tới hàng 👤 / 🗣 hoặc hàng ⛔ | làm việc của người (duyệt đợt, mở PR, phiên ngồi cùng), đánh dấu xong (bảng §1 bước 3), chạy lại |
| 2 | `het han muc TUAN` | trần hạn mức tuần | chạy lại sau giờ hồi ghi trong nhật ký |
| 1 | `cau dao` | 2 phiên liền không tiến triển | mở 2 tệp `.json` mới nhất xem lỗi (§8), sửa rồi chạy lại |
| 4 | `bo chay khac dang chay` | đã có một bộ chạy | không chạy thêm; hoặc xoá khoá nếu nó đã chết (§6) |

Trong WP-14, bộ chạy đi hết đợt A, B, C (kể cả hàng duyệt đợt A, B do `decider` tạm duyệt theo `D-115`) và **dừng ở `P-19`** —
duyệt đợt C cần kế toán; sau đó `P-20` (đóng WP) và `P-21` (mở + merge PR — luôn là việc của người).

## 8. Xử lý sự cố

| Hiện tượng | Nguyên nhân thường gặp | Cách xử lý |
|---|---|---|
| Bộ chạy dừng ngay, nhật ký "cay lam viec co thay doi ngoai docs/**" | tệp hệ thống (`.claude/`, `tools/`) vừa cài hoặc sửa mà chưa commit | chạy đúng lệnh `git add -- …` bộ chạy in ra, `git commit`, rồi chạy lại |
| Phiên xong sau ~10 phút, kết quả "Agent BA vẫn đang chạy…", `.err` có "Background tasks still running … stopping them" | phiên chính gọi vai **chạy nền** rồi kết thúc lượt; `claude -p` chờ 10 phút rồi dừng vai (N34) | bộ chạy bản 20/11 tối đặt `CLAUDE_CODE_DISABLE_BACKGROUND_TASKS=1` (vai luôn chạy tiền cảnh) + `CLAUDE_CODE_PRINT_BG_WAIT_CEILING_MS=0`; skill dặn `run_in_background: false` |
| Chữ tiếng Việt trong cửa sổ thành `?` | console Windows dùng code page cũ | bộ chạy bản 20/11 tối đã tự đổi sang UTF-8; vẫn lỗi ⇒ dùng Windows Terminal |
| Phiên báo "Agent type 'ba' not found" / dừng ở bước 0 | frontmatter vai sai YAML (Claude Code bỏ qua im lặng) | `node --test .claude/hooks/test/hooks.test.mjs` — ca `F1` chỉ tệp + dòng; sửa trong gói cài, chạy lại `cai-dat.ps1` |
| `Unknown command: /chay-phien` | chưa cài gói, hoặc cài sau khi mở Claude | chạy `cai-dat.ps1`; bộ chạy luôn mở phiên mới nên lần sau tự nhận |
| JSON có `permission_denials` (ví dụ `git push`) | chế độ không cửa sổ không có ai duyệt — lệnh không có luật cho phép bị từ chối | commit vẫn ở máy, phiên ghi vào câu trả lời; push tay, hoặc báo để thêm luật `permissions.allow` trong gói cài |
| Cầu dao sau 2 phiên | phiên lỗi ngay đầu (thiếu vai, cây bẩn ngoài `docs/**`, nhánh sai) | đọc `.json` + `.err`; sửa điều kiện §2 |
| Ngủ mãi 30 phút một | không đọc được giờ hồi hạn mức | bình thường khi định dạng thông báo đổi; báo để cập nhật mẫu trong `chay-tu-dong.ps1` (`DocHanMuc`) |
| Hai phiên cùng số `N` / commit lẫn nhau | mở cửa sổ `/chay-phien` tay song song với bộ chạy | **không** chạy song song; phiên tay sẽ tự dừng nếu thấy khoá bộ chạy |

## 9. Luật không đổi khi chạy tự động

- **Một phiên một lúc** — mọi phiên dùng chung thư mục, nhánh, `decisions.md`, nháp biên bản, bảng phiên.
- Phiên chỉ commit `docs/**` + `agents/state/**`, chỉ push **nhánh WP**; không merge, không mở PR, không push `main`, không `--force`
  (`CLAUDE.md` §8, `D-112`). Tệp hệ thống (`tools/`, `.claude/`) anh/chị commit tay sau mỗi lần cài gói.
- Vùng cấm của `CLAUDE.md` §5 vẫn luôn chờ người, kể cả khi hồ sơ uỷ quyền có hiệu lực.
- Hạn mức gói Pro dùng chung với chat: chạy bộ chạy lúc anh/chị không dùng Claude để chat.
