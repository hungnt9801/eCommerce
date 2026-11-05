# Bản đồ module – màn hình – quy trình nghiệp vụ: Nhanh.vn POS

> **Nguồn:** tài liệu chính thức tại <https://pos.manual.nhanh.vn/> (mục lục đầy đủ: <https://pos.manual.nhanh.vn/llms.txt>).
> Mọi trang GitBook đều có bản markdown sạch bằng cách thêm đuôi `.md`.
> **Phạm vi khảo sát:** ~45 trang `.md` ưu tiên (giới thiệu, sản phẩm, kho, kênh bán, đơn hàng, khách hàng, kế toán, báo cáo, khuyến mại, cài đặt, website).
> **Nguyên tắc ghi chép:** chỉ ghi cái quan sát được từ tài liệu; chỗ tài liệu **không mô tả** được đánh dấu rõ là *“tài liệu không nêu”*.

---

## 0. Định vị sản phẩm (bối cảnh)

Nhanh.vn tự mô tả là **phần mềm quản lý bán hàng đa kênh**, quản lý **không giới hạn** số cửa hàng, fanpage, website, gian hàng sàn TMĐT (Shopee, Lazada, Tiki, Sendo). Dữ liệu tồn kho – đơn hàng – khách hàng được **tập trung một nơi**, cập nhật liên tục và chia sẻ giữa hệ thống kho/cửa hàng offline với mọi kênh bán online. Tài liệu nêu “hơn 80.000 doanh nghiệp và chủ shop” đã dùng.
Nguồn: [Giới thiệu về Nhanh.vn](https://pos.manual.nhanh.vn/gioi-thieu-chung/gioi-thieu.md)

Các dấu hiệu đặc trưng phần mềm Việt Nam (quan sát được trong tài liệu):

| Đặc trưng Việt Nam | Bằng chứng trong tài liệu |
|---|---|
| Nhiều **loại sản phẩm** đặc thù | Cân đo (cân điện tử, mã vạch 13 số, số thập phân), IMEI, Gói sản phẩm, Combo, Dịch vụ, Theo lô, Nhiều ĐVT, Dụng cụ — [Các loại sản phẩm](https://pos.manual.nhanh.vn/pos/san-pham/khainiem-sp/loai-san-pham.md) |
| **Nhiều kho / cửa hàng**, chuyển kho nội bộ | [Danh sách chuyển kho](https://pos.manual.nhanh.vn/pos/kho-hang/chuyen-kho/ds-chuyen-kho.md) |
| **Đơn sàn TMĐT** + đối soát COD | [Đối soát sàn](https://pos.manual.nhanh.vn/pos/kenh-ban/shopee/doi-soat-cod.md) |
| **Hoá đơn điện tử** theo Nghị định/TT VN (mẫu 04/SS-HĐĐT, tờ khai 01/GTGT TT80/2021, BC26) | [Nhanh Invoice](https://pos.manual.nhanh.vn/nhanh-invoice/noi-dung-chung.md) – mục lục `llms.txt` |
| **Kế toán VN**: TK 111/112/131/331/511/632/641/33311, bảng cân đối B01-DN, BHXH/BHYT/BHTN | [Hạch toán tự động](https://pos.manual.nhanh.vn/pos/ke-toan/hach-toan-tu-dong.md), [Bảng cân đối B01-DN](https://pos.manual.nhanh.vn/pos/ke-toan/bang-can-doi-kt-b01-dn.md) |
| **Trả góp**, ví MoMo, TPBank/VPBank, Zalo ZBS/OA, Viettel Post/GHN/GHTK/VNPost/J&T/Best/SPX/Ninja Van/SuperShip/Grab | [Dịch vụ trả góp](https://pos.manual.nhanh.vn/pos/ke-toan/dich-vu-tra-gop.md), [Thanh toán](https://pos.manual.nhanh.vn/pos/cai-dat/thanh-toan.md), [Gửi Zalo ZBS](https://pos.manual.nhanh.vn/pos/cai-dat/gui-zalo-zns.md) |
| **In tem mã vạch**, Bartender, máy in bill K80/A5/A4, cân điện tử | [In tem sản phẩm](https://pos.manual.nhanh.vn/pos/san-pham/danh-sach-san-pham/in-tem-san-pham.md), [Đồng bộ Bartender](https://pos.manual.nhanh.vn/pos/cai-dat/dong-bo-bartender.md) |

---

## 1. Bản đồ menu / navigation (cấp 1 → cấp 2, theo đúng thứ tự tài liệu)

Thứ tự dưới đây lấy nguyên theo mục lục `llms.txt`. Ghi chú: dấu `·` là cấp 3 (trang con của cấp 2).

1. **Giới thiệu chung**
   - Giới thiệu về Nhanh.vn · Giới thiệu trang HDSD · Video HDSD · Các vấn đề cần lưu ý · **Các tính năng chung**
2. **Tài khoản**
3. **Cải thiện tốc độ truy cập**
4. **Sản phẩm**
   - Khái niệm sản phẩm
     - Thuộc tính sản phẩm → Các khái niệm
     - Các loại sản phẩm → Cân đo · Imei · Gói sản phẩm · Dịch vụ · Theo lô · Combo · Nhiều ĐVT · Dụng cụ
     - Giá vốn sản phẩm
   - Danh sách sản phẩm → Trang danh sách sản phẩm · Chi tiết sản phẩm · In tem sản phẩm
   - Thêm sản phẩm
   - Tồn kho
   - Thời gian lưu kho
   - Thương hiệu
   - Danh mục
   - Nhà cung cấp
   - *(Lô sản phẩm — có trong nghiệp vụ, xem mục Sản phẩm > Lô sản phẩm ở [bài sản phẩm theo lô](https://pos.manual.nhanh.vn/pos/san-pham/khainiem-sp/loai-san-pham/san-pham-theo-lo.md); không có trang riêng trong mục lục)*
5. **Kho hàng**
   - Xuất nhập kho → Danh sách xuất nhập · Thao tác xuất nhập
   - Chuyển kho → Danh sách chuyển kho · Thao tác chuyển kho
   - Kiểm kho
   - Phiếu nháp
   - Hạn mức tồn kho
   - Vị trí sản phẩm trong kho → Giới thiệu · Danh mục vị trí · Vị trí sản phẩm · Hoá đơn xuất nhập vị trí · Báo cáo diện tích lưu kho
   - Dự báo nhập hàng
   - Bán chéo doanh nghiệp
   - Tổng công ty
   - Hàng lỗi
   - Hạn sử dụng sản phẩm
   - Lịch sử XNK
6. **Kênh bán**
   - Cửa hàng → Bán lẻ · Xác nhận thanh toán hoá đơn · Kết ca · Bán sỉ · Hoá đơn nháp · Hoá đơn điện tử · Trả hàng · Nợ quà tặng · Tìm hoá đơn · Nhập máy cũ
   - Kết nối kênh bán → Thêm kênh bán chung · Đồng bộ SP combo lên sàn · Logic tải đơn hàng · In đơn sàn bằng Extension · FAQ · Lưu ý chung · Chuyển hạn gian sàn TMĐT
   - Shopee → Các bước đồng bộ cơ bản · **Đối soát sàn** · Các loại phí trên sàn · In phiếu nhặt hàng · FAQ
   - Tiktok → (Các bước đồng bộ · Các loại phí · In phiếu nhặt hàng · FAQ)
   - Lazada → (Các bước đồng bộ · Các loại phí · FAQ)
   - Tiki → (Các bước đồng bộ · Các loại phí · FAQ)
   - Sendo → (Các bước đồng bộ · Các loại phí)
   - Facebook Shop → Quy trình tạo · Live Shopping · Quản lý đơn trên Messenger · Quảng cáo Messenger · Quảng cáo Livestream · Tip · FAQ · Quy trình mở Adslive · Checklist
   - Website
   - Tiếp thị liên kết
   - Tài chính sàn TMĐT → Tổng hợp chi phí · Shopee · Tiktok · Tiền quảng cáo · Trả hàng hoàn tiền đơn sàn
   - Hóa đơn điện tử → Cài đặt HĐĐT · Sản phẩm xuất HĐĐT · Danh sách HĐĐT · Tạo HĐĐT
7. **Đơn hàng**
   - Danh sách đơn hàng → Danh sách · Trạng thái đơn hàng · Chi tiết đơn hàng · Nguyên nhân khách hủy · Logic gắn cửa hàng trên website · Vận chuyển · Xuất dữ liệu lấy PageID
   - Quy trình xử lý đơn hàng → Quy trình · Thêm đơn hàng · Sửa dữ liệu hàng loạt · Xác nhận đơn · Gửi đơn sang HVC · In và đóng gói · Tạo link thanh toán QR · Giao hàng một phần (cũ/mới) · Theo dõi xử lý vấn đề phát sinh · Đổi trả đơn hàng
   - Tính năng xuất kho đơn hàng
   - Đơn trùng
   - Đóng gói
   - Biên bản bàn giao
   - Chờ gửi vận chuyển
   - Gửi đơn đa điểm Grab
   - Khiếu nại
   - Thanh toán nợ cước
   - Đối soát → Đơn gửi qua Nhanh · Đơn tự kết nối
   - Nguồn đơn hàng
   - Lịch sử sửa xoá
8. **Khách hàng**
   - Khách hàng · Thêm khách hàng · Thẻ khách hàng
   - Chăm sóc khách hàng → CSKH · Hình thức chăm sóc · Lý do chăm sóc
   - Cấp độ khách hàng · Nhóm khách hàng
9. **Kế toán**
   - Kế toán · Khởi tạo tài khoản kế toán · Thu chi tiền mặt · Thu chi ngân hàng · Công nợ · Bút toán · Dịch vụ trả góp · Kết chuyển cuối kỳ năm tài chính · Kết chuyển cuối kỳ · Lập bảng cân đối kế toán · Bảng cân đối kế toán mẫu B01-DN · Hạch toán tiền lương/BHXH/BHYT/BHTN · Hạch toán đơn hàng · **Hạch toán kế toán tự động** · Nhật ký kế toán chung
10. **Báo cáo**
    - Tổng quan · Doanh thu (theo thời gian/NV/SP/KH) · Đơn hàng (tăng trưởng, theo kênh bán, đơn tạo, đơn thành công, theo trạng thái, thời gian xử lý, NV xử lý, hãng VC) · Bán lẻ (tổng quan, theo NV, theo CH) · Tồn kho (XNT theo SP, Tổng XNK) · Sản phẩm (bán chạy nhất, tốc độ bán) · Kế toán (KQHĐKD, BCĐKT, Sổ kế toán) · Khách hàng (tổng quan, chu kỳ mua hàng, tỉ lệ KH) · Khuyến mại (Affiliate)
11. **Website**
    - Website · Nội dung website · Cài đặt website · Menu · Tin tức (quản lý tin tức, chèn link nội bộ, menu liên kết, tạo mục lục) · Banner · Album · Liên hệ · Newsletters · Hiển thị SP trên nhiều danh mục · Quản lý mã nhúng (vchat, Google Analytics, chat Zalo, Google Webmaster, hiệu ứng) · Tên miền (xác thực tên miền, trỏ tên miền, đăng ký BCT, email ten@tenmien) · Bán hàng trên Facebook (quản lý comment, mã nhúng FB/Google Feed, link addcart) · Quảng cáo Google Shopping · Dịch nhiều ngôn ngữ · FAQ
12. **Bảo hành**
    - Quy trình bảo hành · Lý do bảo hành · Tiếp nhận bảo hành/sửa chữa · Trung tâm bảo hành · Danh mục bảo hành · Trạng thái sản phẩm/phiếu BH · Bảo hành mở rộng · Hạch toán kế toán trong bảo hành
13. **Khuyến mại**
    - Chiết khấu → Nội dung · Một số CTKM hay gặp
    - Chiết khấu V2 → Thêm mới CTKM · Cài đặt chiết khấu · CTKM cơ bản · CTKM nâng cao
    - Thiết lập bảng giá · Tích điểm · Mã giảm giá · Quà tặng · Quà tặng V2 (Thêm mới · Cài đặt · CT thường gặp) · Hoa hồng nhân viên · Chương trình Affiliate
14. **Cài đặt**
    - Nhân viên · Cài đặt chung · Bán hàng và XNK · Hoá đơn điện tử · Đơn hàng (Cài đặt đơn hàng · Cài đặt vận chuyển · Cài đặt phí ship báo khách · Kết nối dịch vụ vận chuyển) · Cài đặt nhãn · Mẫu in · Gửi Zalo ZBS (Giới thiệu · Khởi tạo OA · Xác thực OA · Các loại tin · Cài đặt gửi · Tạo mẫu tin · Chính sách · Báo cáo) · Gửi Email · Gửi SMS · Thanh toán (TPBank cá nhân · Ví MoMo DN · Cài đặt xác nhận thanh toán: Momo/TPBank/VPBank) · Chi nhánh · Đại lý · Hạn sử dụng · Thông báo · Driver máy in · Đồng bộ Bartender
15. **Nhanh Invoice** (phân hệ HĐĐT độc lập): Nội dung chung (Trang chủ, Thiết lập tài khoản, Thông tin đơn vị) · Hóa đơn bán ra (Danh sách, Xóa bỏ, Điều chỉnh, Thay thế, Mẫu 04/SS-HĐĐT, Hóa đơn chuyển đổi, Bảng tổng hợp dữ liệu, Lịch sử gửi mail) · Báo cáo (kiểm tra sai lệch, đối soát 04/SS, số lượng xuất HĐ, tờ khai 01/GTGT TT80, BC26) · Bảng kê (HĐ, theo thuế suất, theo SP, phần mềm kế toán) · Đồng bộ/Đối soát (đơn hàng, AMIS) · Cài đặt/Cấu hình (tờ khai 01/ĐKTĐ, mẫu HĐ, cấu hình tải HĐ TCT, chữ ký số, cấu hình HĐ, người dùng, vai trò quyền hạn, tích hợp Nhanh.vn, ký tự động, cảnh báo) · Danh mục (KH, SP)
16. **KOC Invoice**: Cài đặt · Pháp nhân · Thanh toán · Danh sách xuất hoá đơn
17. **Tính năng theo từng bộ phận**: Tổng quan · Giám đốc · Bán hàng · CSKH · Kế toán · Marketing · Kho
18. **Thiết bị bán hàng**: Giới thiệu · Máy hiện QR Code

**Nhận xét navigation:** điều hướng theo **vai trò nghiệp vụ** (module) chứ không theo đối tượng dữ liệu; các trang “danh mục phụ” (thuộc tính, lô, vị trí kho, nhãn, mẫu in) nằm rải trong module tương ứng chứ không gom vào một mục “Danh mục” chung. Có 3 phân hệ tài liệu tách rời (POS/Nhanh.vn, Nhanh Invoice, KOC Invoice) cho thấy sản phẩm được bán theo **gói/tier** ([Các vấn đề cần lưu ý](https://pos.manual.nhanh.vn/gioi-thieu-chung/vd-can-luu-y.md) ghi “module hiện ra dựa theo gói mà bạn đã đăng ký”).

---

## 2. Module: nền tảng dùng chung (“Các tính năng chung”)

Đây là **bộ khung UI toàn hệ thống** – quan trọng nhất nếu làm lại Web Admin.
Nguồn: [Các tính năng chung](https://pos.manual.nhanh.vn/gioi-thieu-chung/cac-tinh-nang-chung.md)

**Màn hình/khối UI:**
- **Bộ lọc cơ bản**: cửa hàng, ID, tên sản phẩm… “tùy module sẽ có thay đổi khác nhau”.
- **Bộ lọc nâng cao**: mở bằng nút mũi tên cạnh chữ Lọc.
- **Bộ lọc đã lưu**: lưu bộ lọc hiện tại (Tạo mới / Ghi đè bộ lọc cũ), xoá, và **kéo‑thả để sắp xếp thứ tự** bộ lọc đã lưu.
- **Cài đặt ẩn/hiện cột**: tick để hiện/ẩn; cột “icon xanh mờ” là mặc định không tắt được; có **“Quay về mặc định”**.
- **Phân trang**: mặc định 50 dòng/trang; thêm `&icpp=200` sau `storeId` để lên tối đa 200 dòng/trang.
- **Xuất dữ liệu** (Thao tác > Xuất dữ liệu): chọn *dữ liệu đã chọn* hoặc *tất cả các trang*; đổi tên bảng tính/trang tính/tên cột.
- **Xuất Google Sheets**: tạo file/trang tính mới hoặc dùng file có sẵn (ghi nối vào cuối file); kết nối nhiều tài khoản Google; **“Lưu cài đặt cho lần sau”** với 2 phạm vi: *Dùng riêng cho cá nhân* / *Dùng chung cho doanh nghiệp*. Lưu ý: chỉ lưu tên trang tính/bảng tính/cột, **không chia sẻ tài khoản Google** (mỗi người tự kết nối và cần quyền quản lý bảng tính).
- **Import từ Google Sheets** (ví dụ import đơn hàng): chọn Bảng tính → Trang tính → Kho hàng → Hãng vận chuyển → Lưu; hiển thị **kết quả từng dòng**.
- **Chọn nhiều sản phẩm**: tìm theo tên/mã/mã vạch, lọc, chọn nhiều; áp dụng ở **Bán lẻ, Bán sỉ, Đơn hàng, Xuất nhập tồn kho**.
- **Business Tools** (`/business/tool/index`) – 3 tab:
  1. **Tồn kho**: tính lại tồn hàng loạt (lọc theo thời gian tạo SP, XNK từ ngày–đến ngày); có nút tính lại từng sản phẩm. *Lưu ý tài liệu: combo, sản phẩm lô, cân đo vẫn hiển thị nhưng thực tế không có tồn/sai tồn.*
  2. **Đang chuyển, tạm giữ**: tìm sản phẩm treo tồn tạm giữ/đang chuyển, load lại.
  3. **Đơn hàng chưa XNK**: tìm đơn **Thành công** nhưng chưa sinh phiếu XNK → “Thêm tất cả XNK”. Ngoại lệ không sinh được phiếu: đơn Website chưa gắn kho; doanh nghiệp bật “Tạo phiếu xuất kho thủ công cho đơn hàng”.

**Quy tắc nghiệp vụ đáng chú ý:**
- Gợi ý lọc chỉ hiện khi gõ **tối thiểu 3 ký tự**.
- Có **2 kiểu tìm kiếm** (cài ở `nhanh.vn/setting/store/index`): *tuần tự* (phải đúng thứ tự từ) và *gần đúng* (chịu lỗi gõ).
- Xuất Excel log lại lịch sử; Google Sheets cũng log và thấy được “ai đã từng chỉnh sửa theo từng lần xuất”.

**Màn hình khác của “khung” hệ thống** ([Các vấn đề cần lưu ý](https://pos.manual.nhanh.vn/gioi-thieu-chung/vd-can-luu-y.md)):
- **(+) Thêm mới** trên topbar: thêm nhanh sản phẩm, bán lẻ, đơn hàng, in mã vạch, **Nhập từ excel** (`/store/import/index`).
- **Chuông thông báo hệ thống**; **logo → về trang tổng quan**; **hỗ trợ trực tuyến** ở góc phải dưới.
- **Sửa thông tin tài khoản**: đổi mật khẩu, cài đặt giao diện; **Cài đặt giao diện tuỳ ý**.
- **Hạn sử dụng**: `Cài đặt > Hạn sử dụng` (`/setting/expire/depot`) – xem hạn theo Cửa hàng / Website / Sàn TMĐT; sửa thông tin cửa hàng tại đây.
- **BusinessID**: bấm vào tên một sản phẩm bất kỳ để xem số định danh doanh nghiệp.
- **Quy ước màu**: chuỗi ký tự **màu xanh dương nhạt = có link**, click được (tên SP, tồn kho, ID).
- **Số điện thoại**: click 1 lần để **copy**, có menu thao tác (lọc đơn, tạo đơn, gọi điện).
- **Không có chức năng xoá toàn bộ dữ liệu doanh nghiệp** → phải tạo tài khoản mới và email xin chuyển hạn.
- Cảnh báo vận hành: **Cốc Cốc** chặn quảng cáo làm hỏng thao tác; **iOS ≥ 15.7 không chọn được khổ in A5**; lỗi chứng chỉ Let’s Encrypt; nên **tắt Autofill** của Chrome vì đè lên gợi ý khách hàng/địa chỉ.

---

## 3. Module Sản phẩm

### 3.1 Khái niệm & mô hình dữ liệu

**Sản phẩm cha – con – độc lập** ([Trang danh sách sản phẩm](https://pos.manual.nhanh.vn/pos/san-pham/danh-sach-san-pham/trang-dssp.md)):
- **Độc lập**: không chia thuộc tính, tồn tại duy nhất.
- **Cha**: SP quản lý của SP con; **không có tồn**, **không có XNK**, **không tìm thấy** khi tạo hoá đơn / nhập NCC / thêm đơn hàng.
- **Con**: chia theo thuộc tính (màu/size/chất liệu); **có tồn**, là SP hiện ra khi nhập kho/bán hàng.

**9 loại tồn kho** (bảng ngữ nghĩa đầy đủ – rất quan trọng cho mọi màn hình kho):

| Loại tồn | Định nghĩa (nguyên văn rút gọn) |
|---|---|
| Tồn có thể bán | Hàng chưa bị đặt trước, bán được cho KH mới. **= tồn trong kho − tạm giữ − lỗi** |
| Tạm giữ | Hàng đã có khách đặt trước, không bán cho KH mới |
| Đang chuyển kho | Hàng đang chuyển giữa các kho |
| Lỗi | Hàng lỗi, không bán được |
| Tồn trong kho | Tổng tồn tại cửa hàng **= Tồn − Đang chuyển** |
| Đang giao hàng (đang chuyển) | Đã giao cho HVC, không còn tại cửa hàng |
| Tồn | Tồn thực tế, lấy theo phiếu XNK cuối cùng |
| Tổng tồn | **= tổng tồn tất cả cửa hàng + đang chuyển kho** |
| Đặt trước | Tổng SL khách đặt trước (khi hết hàng) |
| Chờ nhập hàng | SL sắp về (SP đã duyệt ở phiếu yêu cầu XNK) |

**7 loại giá**: Giá nhập · Giá vốn (bình quân gia quyền **hoặc** đích danh cho IMEI/lô) · Giá bán · Giá cũ (dùng hiển thị website) · Giá buôn · Giá chi nhánh · VAT.
Quy tắc: **giá vốn không nhập/sửa/xoá trực tiếp** – phải sửa các lần nhập kho rồi “Tính lại giá vốn”.

**4 trạng thái sản phẩm** (enum):
| Trạng thái | Ý nghĩa |
|---|---|
| Mới | SP thường, đủ chức năng, **không hiển thị website** |
| Đang bán | SP thường, **có hiển thị website** |
| Hết hàng | Hiện ở danh sách + website nhưng **chặn bán hàng, tạo đơn, XNK** |
| Ngừng bán | **Ẩn khỏi danh sách**, không hiển thị web, chặn mọi chức năng |

Điều kiện hiển thị website: **có ảnh + trạng thái Đang bán + thuộc danh mục đang hoạt động** ([Thêm sản phẩm](https://pos.manual.nhanh.vn/pos/san-pham/them-sp.md)). Nếu import “Đang bán” mà SP không có ảnh → hệ thống tự chuyển về “Mới”.

### 3.2 Các loại sản phẩm (đặc thù VN)

| Loại | Đặc điểm & quy tắc | Nguồn |
|---|---|---|
| **Cân đo** | Đơn vị theo khối lượng/kích thước; **loại duy nhất hỗ trợ số thập phân**; mã vạch 13 số = 2 số đầu (đầu số cân điện tử) + 5 số mã SP + 5 số khối lượng (gram) + 1 số ngẫu nhiên. Bắt buộc: Mã vạch (5 số), Tên, Giá bán. Bật “bán hàng hỗ trợ quét mã vạch cân điện tử”; đầu số mã vạch cân **phải khác** đầu số mã vạch SP. **SL thập phân không gửi được qua HVC** (HVC chỉ nhận số nguyên). Không nên đổi qua lại loại khác (lệch tồn) | [SP cân đo](https://pos.manual.nhanh.vn/pos/san-pham/khainiem-sp/loai-san-pham/san-pham-can-do.md) |
| **IMEI** | Mỗi IMEI duy nhất 1 thiết bị; **không nhập tồn đầu** – SL = số IMEI nhập ở phiếu nhập NCC (mỗi IMEI 1 dòng). Giá vốn **đích danh** (bật cài đặt). Danh sách IMEI có tab: Có hạn BH / Sắp hết hạn (10 ngày) / Đã hết hạn / **Lịch sử IMEI** / **Lệch tồn IMEI** / **Đổi sản phẩm IMEI**. Màu giá: đen = giá riêng IMEI, đỏ = giá SP. Màu bảo hành: đen = còn BH, đỏ = hết BH. Thao tác: Xuất Excel, Kích hoạt IMEI đã chọn, In IMEI, **Import update IMEI** (giá bán, số tháng BH, ngày hết hạn, ghi chú) | [SP Imei](https://pos.manual.nhanh.vn/pos/san-pham/khainiem-sp/loai-san-pham/san-pham-imei.md) |
| **Combo** | Ghép nhiều SP thường; **không trừ tồn SP con**; tồn combo = **tồn SP con nhỏ nhất / số lượng quy đổi**; ngoài danh sách lấy tổng tồn 3 kho của SP con nhỏ hơn, trong chi tiết lấy **min theo từng kho** → **không so sánh được tổng**. Không nhập tồn đầu. Bán combo sinh **2 phiếu xuất kho** (1 thường + 1 loại combo) → lịch sử tồn hiện âm (hiểu là 0 − n). Báo cáo: Bán chạy nhất / Doanh thu / Đơn hàng / Tổng XNK **có** tính combo; **XNT theo sản phẩm không** tính combo | [SP combo](https://pos.manual.nhanh.vn/pos/san-pham/khainiem-sp/loai-san-pham/san-pham-combo.md) |
| **Gói sản phẩm** | SP thành phẩm **có tồn riêng**; tạo ở `Kho hàng > Gói sản phẩm` (không tạo ở danh sách SP). Tạo gói ⇒ **trừ tồn SP con + sinh phiếu nhập gói**. Có **Bung gói sản phẩm** để tách bán lẻ. Giá nhập gợi ý = Σ(SL × giá nhập SP con) | [Gói sản phẩm](https://pos.manual.nhanh.vn/pos/san-pham/khainiem-sp/loai-san-pham/goi-san-pham.md) |
| **Dịch vụ** | **Không có tồn** (bán được cả khi chặn bán âm), **không kiểm kho**. Có thể có giá nhập = giá vốn để tính lợi nhuận | [SP dịch vụ](https://pos.manual.nhanh.vn/pos/san-pham/khainiem-sp/loai-san-pham/san-pham-dich-vu.md) |
| **Theo lô** | Quản lý theo lô SX/lô nhập; bật “Bán hàng theo lô sản xuất”; tùy chọn giá vốn đích danh; tùy chọn **bán lấy lô hết hạn gần nhất** (mặc định: lô có ngày SX cũ nhất). **Không hỗ trợ đổi từ loại khác sang lô** – phải tạo SP mới. Lô tạo ở `Sản phẩm > Lô sản phẩm` (`/product/batch/index`) hoặc tạo nhanh trong phiếu nhập. Từ 7/9/2023 **tự động gắn lô cho đơn sàn** | [SP theo lô](https://pos.manual.nhanh.vn/pos/san-pham/khainiem-sp/loai-san-pham/san-pham-theo-lo.md) |
| **Nhiều ĐVT** | Bán theo 2+ ĐVT quy đổi (viên/vỉ/hộp/thùng); mỗi ĐVT giá riêng; **ĐV nhỏ nhất khai trước, tối thiểu = 1**; giá vốn bình quân gia quyền = giá nhập / SL quy đổi | [SP nhiều ĐVT](https://pos.manual.nhanh.vn/pos/san-pham/khainiem-sp/loai-san-pham/san-pham-nhieu-dvt.md) |
| **Dụng cụ** | Công cụ/dụng cụ không phải TSCĐ; **kiểm kho được**; chỉ 2 báo cáo tồn kho hiển thị (**Tổng XNK**, **XNK theo NCC**), các báo cáo tồn khác không | [SP dụng cụ](https://pos.manual.nhanh.vn/pos/san-pham/khainiem-sp/loai-san-pham/san-pham-dung-cu.md) |

**Quy tắc chung lặp lại ở mọi loại:** *“Không nên chuyển qua lại giữa các loại sản phẩm vì logic tồn khác nhau dẫn tới sai lệch tồn kho.”*

### 3.3 Danh sách sản phẩm (`/product/item/index`)

Nguồn: [Trang danh sách sản phẩm](https://pos.manual.nhanh.vn/pos/san-pham/danh-sach-san-pham/trang-dssp.md)

**Cột hiển thị (tài liệu nêu tên):** ID (kèm icon loại SP), Tên, Giá (nhập/bán), Tồn (nhiều loại tồn), Trạng thái, Danh mục, Thương hiệu, Nhà cung cấp. Tài liệu **không liệt kê đầy đủ** danh sách cột dạng bảng.

**Hành động:** Thêm mới (trực tiếp / Nhập từ Excel), Sửa, Xoá, **In mã vạch**, Xuất Excel (Thao tác).

**Nút đặc biệt (bài nêu rõ):**
- **Set lại SP hot** / **Set lại SP mới** → đẩy lên block “SP hot”/“SP mới” trên trang chủ website.
- **Set lại cha con** → rebuild lại quan hệ cha–con khi icon dưới ID sai.
- **Xem giá sản phẩm theo chi nhánh** → kiểm tra giá bán lẻ/buôn riêng từng cửa hàng.
- **Xem danh sách sản phẩm trùng lặp** → lọc SP trùng theo tên hoặc mã vạch.

**Bộ lọc cơ bản:** Tồn (Còn tồn có thể bán / Còn tồn).
**Bộ lọc nâng cao:** ID từ API · Giá (theo loại giá; theo khoảng giá) · Tồn (theo khoảng) · **Tồn theo sản phẩm cha** (đếm tổng tồn theo mã cha).

**Quy tắc xoá:** SP còn tồn hoặc có đơn hàng → phải xoá phiếu XNK và đơn hàng liên quan trước. Khuyến nghị: không xoá SP đã kinh doanh lâu, chuyển **Ngừng bán** thay thế.

### 3.4 Chi tiết sản phẩm

Nguồn: [Chi tiết sản phẩm](https://pos.manual.nhanh.vn/pos/san-pham/danh-sach-san-pham/chi-tiet-san-pham.md)

2 kiểu hiển thị: **pop-up** và **trang chi tiết**. 7 sheet:

| Sheet | Nội dung |
|---|---|
| Thông tin | Tên, mã, giá nhập, giá bán, khối lượng, danh mục… + phần **Tích hợp** (số lượng SP đã ghép với sàn; click để xem danh sách đã ghép) |
| Tồn kho | Tồn theo từng cửa hàng (kể cả cửa hàng NV không có quyền quản lý) |
| Ảnh | Upload/xoá ảnh |
| Khuyến mại | 2 loại: chiết khấu theo SP và quà tặng |
| Website | Thông tin hiển thị web (chỉ DN dùng website Nhanh) |
| Lịch sử sửa | Log sửa dữ liệu |
| XNK | Các lần xuất/nhập kho của SP |
| API | Lịch sử truyền dữ liệu API (DN dùng API hoặc đồng bộ tồn với sàn) |

**Nút Thao tác (6 nút – đây là các “hành động sửa lỗi dữ liệu” rất đặc trưng):**

| Nút | Ý nghĩa |
|---|---|
| Sửa thông tin hiển thị trên Website | Cho phép phân quyền NV chỉ sửa được mô tả/metadescription, không sửa giá |
| Chia thuộc tính | Bổ sung SP con còn thiếu (chỉ hiện với SP cha/độc lập) |
| **Tính lại số tồn** | Khi 2 người thao tác 1 đơn cùng lúc làm sai tồn |
| **Tính lại giá vốn** | Khi sửa giá nhập các lần trước mà chưa cập nhật |
| **Tính lại số đang chuyển kho** | Khi xác nhận phiếu chuyển kho bị ngắt giữa đường |
| **Tính lại số tạm giữ và đang chuyển** | Khi nhiều NV đổi trạng thái đơn cùng lúc → treo tồn |

### 3.5 Thêm / sửa / xoá sản phẩm, import

Nguồn: [Thêm sản phẩm](https://pos.manual.nhanh.vn/pos/san-pham/them-sp.md)

**Quy trình 4 bước:** tạo danh mục → kiểm tra có chia thuộc tính không → thêm mới → kiểm tra lại ở chi tiết SP.
**2 cách thêm:** trực tiếp hoặc **Import Excel**.
- Trường bắt buộc tối thiểu: **Tên sản phẩm + Loại sản phẩm**. Các trường có dấu `*` là bắt buộc.
- Chia thuộc tính: phải **thuộc danh mục mới** mới thêm được thuộc tính; chọn thuộc tính + giá trị → hệ thống **tự sinh tổ hợp SP con**; có ô **thứ tự** (đổi thứ tự làm đổi Tên mở rộng/Mã mở rộng); có “Copy ảnh sản phẩm cha cho sản phẩm con”; tự sinh mã vạch nếu bật cài đặt.
- Import Excel: 2 sheet (Ghi chú + Sản phẩm); **chọn ngành hàng** quyết định file mẫu — ngành **“Thời trang”** có cột `[M]`, `[S]`, `[M-S]`; ngành “Chung” không có. Cột `M`, `S` chỉ dùng để **cập nhật**, không dùng để thêm mới. Các loại SP import được: **Sản phẩm, cân đo, dịch vụ, IMEI** (các loại khác nên thêm trực tiếp).
- **Import ảnh**: đặt tên ảnh trùng tên file trên máy, hỗ trợ pattern `anh[1-10].png`; phải **upload ảnh trước** rồi mới import Excel.
- **Import cập nhật**: chọn kiểu “Cập nhật thông tin”, bắt buộc có Tên SP; nếu xuất Excel từ danh sách rồi import lại phải **đổi tên trang tính thành “Sản phẩm”**.
- Khối lượng mặc định **500 gram** nếu không điền (không cấu hình được mặc định khác).

### 3.6 Tồn kho & in tem

- **Tồn kho / Thời gian lưu kho / Thương hiệu / Danh mục / Nhà cung cấp**: các trang danh mục phụ. *Tài liệu không mô tả chi tiết cột/hành động của các trang này trong phạm vi đã đọc.*
- **In tem sản phẩm** ([nguồn](https://pos.manual.nhanh.vn/pos/san-pham/danh-sach-san-pham/in-tem-san-pham.md)): 2 phương pháp — in trực tiếp từ Nhanh, hoặc xuất Excel in bằng **Bartender**.
  - Luồng in trực tiếp 4 bước: nhập SP + số lượng cần in → **chọn thông tin hiển thị trên tem (tối đa 3/4: mã SP, tên DN, tên SP, giá)** → chọn khổ giấy + căn lề (khuyến nghị đặt lề 0 mm) → Xem và in.
  - Loại mã vạch: tự động là **EAN13**; hỗ trợ thêm **C128, C128A, C39**.
  - **In từ phiếu XNK** / **từ danh sách sản phẩm XNK**: in đúng số lượng theo từng lần nhập, không cần điền số lượng → dùng để đối chiếu số lượng thực nhập.
  - FAQ: **tem bị lệch → bỏ tuỳ chọn “Vừa với trang”**.

---

## 4. Module Kho hàng

### 4.1 Xuất nhập kho

**Danh sách** ([nguồn](https://pos.manual.nhanh.vn/pos/kho-hang/xuat-nhap-kho/ds-xnk.md)) — 2 khái niệm: **Phiếu XNK** (1 ID/phiếu) và **Sản phẩm XNK** (1 ID/sản phẩm trong phiếu) → 2 tab.

**Bộ lọc phiếu XNK:** Kho liên quan · **Đặc điểm** (Đơn từ đơn hàng | Có công nợ | Còn công nợ | Hết công nợ | Nhập máy cũ) · Nguồn khách hàng · Ghi chú · Nhãn.

**Cột danh sách phiếu:** checkbox chọn nhiều · **ID | Ngày** (hover xem giờ tạo; có **(Log sửa)**; hiển thị phiếu XNK gốc liên quan) · Kho hàng (kèm loại/kiểu XNK) · **SP** (tổng số SP) · **SL** · **Tổng tiền** · icon chiết khấu · Người tạo · Khách hàng · icon file chứng từ đính kèm · menu bánh răng.

**Quy tắc tính “Tổng tiền” theo loại phiếu** (bảng trong tài liệu – rất hữu ích để đặc tả):
| Loại phiếu | Công thức |
|---|---|
| Xuất/nhập NCC `[N]` | Giá nhập × SL − chiết khấu |
| Xuất `[G]`, `[L]`, `[B]` | Giá bán × SL − chiết khấu |
| Xuất/nhập chuyển kho | **Giá vốn** × SL |
| Nhập khác | Giá nhập × SL − chiết khấu |
| Xuất khác | Giá bán × SL − chiết khấu |

**Thao tác (hàng loạt):** Đổi ngày XNK (mặc định = ngày tạo; đổi về quá khứ; **tính năng không mở sẵn** – phải liên hệ CSKH) · Sửa phiếu (đánh dấu + lưu log) · **In mã vạch sản phẩm trong phiếu** · **Xuất Excel in mã vạch bằng Bartender** · Xoá phiếu (lưu log xoá).
**Tab Sản phẩm XNK:** bộ lọc thêm **Lợi nhuận** (>, <, =) và **Giá** (khoảng); cột thêm **Tiền** (trước chiết khấu); thao tác: Xoá sản phẩm XNK (xoá hết SP thì phiếu mới mất), Xuất Excel, In mã vạch.

**Thao tác xuất nhập** ([nguồn](https://pos.manual.nhanh.vn/pos/kho-hang/xuat-nhap-kho/thao-tac-xuat-nhap.md)):
- **Nhập nhà cung cấp** – 5 bước thực tế: nhập hàng về kho → kiểm SL/chất lượng → tạo phiếu → **in 2 bản, mỗi bên ký 1 bản** → **in tem dán lên SP**.
- Quy tắc: mặc định danh sách chỉ hiện phiếu **10 ngày gần nhất**; **chiết khấu theo SP được ưu tiên hơn chiết khấu cả hoá đơn**; **nên nhập tối đa ~200 SP/phiếu**; SP IMEI mỗi IMEI 1 dòng, số dòng = cột SL; nếu dùng module Kế toán phải điền đúng số tiền thanh toán + chọn quỹ, nếu không hệ thống hiểu là **phát sinh công nợ**.
- In phiếu: 1 phiếu hoặc **in nhiều phiếu cùng lúc** (tích chọn → Thao tác → In).
- **Import XNK từ Excel**: Kiểu = *Lập phiếu XNK*; Kiểu XNK = *[N] Nhà cung cấp*; chọn kho; “Cập nhật giá nhập sản phẩm” chỉ áp dụng cho phiếu nhập NCC; **không dùng ký tự đặc biệt trong tên file**.
- **Trả nhà cung cấp**: phiếu Xuất kho, chọn NCC; SP IMEI phải nhập IMEI; có cột **Số lượng lỗi** để trừ tồn lỗi.
- **Xuất nhập khác**: chọn “Loại nhập/xuất hàng = Khác”.
- FAQ: đính kèm chứng từ ở chi tiết phiếu (mục File đính kèm); **không bóc tách phí vận chuyển** vào giá vốn (tự cộng vào giá nhập); **quà tặng là IMEI/Lô/Combo chưa sinh phiếu XNK trừ tồn** (workaround: sửa đơn, bán giá 0 hoặc chiết khấu 100%).

### 4.2 Chuyển kho

Nguồn: [Danh sách chuyển kho](https://pos.manual.nhanh.vn/pos/kho-hang/chuyen-kho/ds-chuyen-kho.md)

**Điều kiện:** phải có **tối thiểu 2 kho**; **muốn thêm kho phải liên hệ admin** (không tự mở bằng tài khoản quản trị).
**4 tab:** Chuyển kho · Phiếu nháp · **Đang chuyển đi** (kho gửi theo dõi) · **Sắp chuyển đến** (kho nhận theo dõi, cần xác nhận).
**Bộ lọc:** Từ kho · Đến kho · ID · Ngày tạo · Loại · **ID phiếu nháp** (nâng cao).
**Quy ước hiển thị kho (rất dễ nhầm):** `Kho 2 - Thảo ← Kho Tổng` = **nhập** chuyển kho; `Kho Tổng -> Kho 2 - Thảo` = **xuất** chuyển kho.

**5 cách tạo (menu Thêm mới):**
1. **Thêm phiếu chuyển kho** – sinh ngay **2 phiếu XNK**: 1 phiếu Xuất `[C] Chuyển kho` + 1 phiếu **chuyển kho nháp chờ xác nhận** vào kho nhận.
2. **Thêm mới phiếu chuyển kho nháp** – cần **Duyệt** (sinh phiếu Xuất) rồi **Xác nhận** (sinh phiếu Nhập).
3. **Gọi hàng từ kho khác** – tạo phiếu nháp, thường do kho nhận yêu cầu.
4. **Nhập từ Excel**.
5. **Import điều chuyển kho** – nhiều SP, số lượng cụ thể, tới **nhiều kho cùng lúc**.
6. **Phân phối chuyển kho theo tỷ lệ** – tài liệu nêu “tiết kiệm tới 70% việc tính toán số lượng cần chuyển”.

**Trạng thái phiếu nháp (enum):** Mới → Đã duyệt → Đã xác nhận; nhánh **Đã hủy**. Tồn tương ứng: Đã duyệt → tồn kho chuyển giảm, SP sang **Đang chuyển kho**; Đã xác nhận → kho nhận tăng **Tồn trong kho**.
**Quy tắc huỷ/xoá:** không huỷ được yêu cầu đã có phiếu XNK (phải xoá phiếu XNK trước); chỉ xoá được phiếu nháp ở **Mới** hoặc **Đã hủy**; phiếu **Đã duyệt** muốn huỷ phải xoá phiếu Xuất chuyển kho → phiếu nháp tự sang **Đã hủy**.
**Kiểm soát số lượng:** người duyệt được duyệt **cao hơn hoặc thấp hơn** số lượng nhân viên yêu cầu; SL sau xác nhận = SL trong phiếu chuyển kho.
Cảnh báo: khi xác nhận phiếu nháp **phải xác nhận đủ số lượng**; nếu thất lạc phải tạo phiếu XNK khác để xử lý.

### 4.3 Kiểm kho

Nguồn: [Kiểm kho](https://pos.manual.nhanh.vn/pos/kho-hang/kiem-kho.md) — tài liệu rất chi tiết, là một trong những module “nặng nghiệp vụ” nhất.

**3 loại kiểm kho:**
| Loại | Hành vi |
|---|---|
| Theo sản phẩm | Kiểm 1/nhóm SP đã lên danh sách trước; **không hỗ trợ “Tìm sản phẩm thiếu”** |
| Theo danh mục | Chỉ ghi nhận SP **trong danh mục** đã chọn; SP ngoài danh mục → báo “Sản phẩm không thuộc danh mục đang kiểm kho” |
| Toàn bộ | SP không nằm trong danh sách kiểm ⇒ **mặc định tồn = 0** |

**Loại SP trong phiếu kiểm:** **Combo không thêm được**. IMEI: có sinh phiếu XNK kiểm kho nhưng **làm lệch tồn IMEI** (không điền IMEI kiểm). Theo lô: **không** sinh phiếu XNK kiểm kho. Dịch vụ: có sinh phiếu nhưng logic không có tồn.

**Danh sách phiếu kiểm** – 2 tab (Kiểm kho / Sản phẩm kiểm kho); cột **SP thiếu** (chỉ với phiếu chưa bù trừ + loại danh mục/toàn bộ) và **Bù trừ kiểm kho** (nút bù trừ, hoặc thông tin người + ngày giờ bù trừ).
**Gộp phiếu kiểm:** điều kiện **cùng cửa hàng + cùng danh mục kiểm + cùng loại kiểm kho + chưa bù trừ**; lỗi “Loại kiểm kho không hợp lệ” nếu khác loại; **gộp xong không hoàn tác được**; phiếu gộp lấy thông tin **phiếu tạo sau**.
**Chi tiết phiếu kiểm** – 4 tab: **Tất cả / Khớp / Lệch / Chưa kiểm**. Tab Lệch tính **Giá trị lệch = SL lệch × giá vốn tại thời điểm tạo phiếu**. Có “Tính lại tồn sản phẩm” trong menu 3 gạch.

**Quy tắc vàng về cột “Thực tế”:** đối chiếu với **Tồn trong kho (icon ngôi nhà)**, mà `Tồn trong kho = Có thể bán + Tạm giữ + Lỗi` ⇒ **số điền vào cột Thực tế phải bao gồm cả tạm giữ và hàng lỗi**, nếu không sẽ âm tồn.

**Quy trình chuẩn 6 bước chuẩn bị:** (1) ngừng bán hàng & XNK; (2) xác nhận lại **phiếu chuyển kho + phiếu nháp**; (3) cập nhật đúng trạng thái đơn hàng; (4) xác nhận **đơn hoàn** (đổi Đang hoàn/Xác nhận hoàn → **Đã hoàn**); (5) **chạy lại tồn kho** (tính lại tạm giữ/đang chuyển, tính lại số chuyển kho); (6) phân công nhân sự & khu vực.
- Nguyên tắc “vừa bán vừa kiểm”: hàng **đang chuyển** không ảnh hưởng; hàng sắp nhập thì **dừng nhập**; tồn trên phiếu kiểm **đóng băng** tại thời điểm SP được thêm vào phiếu (trừ khi load lại tồn).
- Tổ chức: tối thiểu **3 cặp kiểm / 3 khu vực / 3 máy tít / 3 laptop**; số đầu tít = số máy tính; **1 người chuyển tem, 1 người tít**; **tít vào Excel trước, không tít trực tiếp lên hệ thống**; ghi chú vị trí từng kệ; tách riêng SP mất tem.

**4 bước tiến hành:** ghi chú khu vực kiểm → thêm SP (2 cách: trực tiếp với **chế độ máy tít** / **chế độ nhập tay**, hoặc **file Excel** — Excel không điền SL thì mặc định **= 1**) → **gộp phiếu** → **tìm sản phẩm thiếu** → **bù trừ kiểm kho**.
- **Hệ thống tự lưu phiếu sau mỗi thao tác thêm SP** (chống mất dữ liệu).
- Bù trừ: hệ thống tự tạo **phiếu Xuất + phiếu Nhập** để cân bằng; **ngày bù trừ = ngày tạo phiếu** (không phải ngày bấm bù trừ); phiếu đã bù trừ muốn xoá phải xoá phiếu XNK trước.
- Nghiệp vụ đặc biệt: **kiểm kho toàn bộ về 0** (chọn Kiểm toàn bộ → Tìm sản phẩm thiếu → xác nhận → bù trừ).
- Báo cáo kiểm kho: tổng giá trị thất thoát theo **Giá nhập / Giá bán / Giá vốn**; **tính cộng trừ theo hàng ngang, không theo cột dọc**.
- **Không điều chỉnh được ngày kiểm kho** (mặc định = ngày tạo phiếu).

### 4.4 Phiếu nháp

Nguồn: [Phiếu nháp](https://pos.manual.nhanh.vn/pos/kho-hang/phieu-nhap.md)
- Mục đích: lưu phiếu nháp; **ước tính SL hàng dự kiến XNK**; **quản lý hoạt động lập phiếu của nhân viên** mà không ảnh hưởng tồn kho/doanh thu.
- 2 tab: **Phiếu XNK nháp** và **Sản phẩm XNK nháp**.
- **6 loại phiếu nháp:** nhà cung cấp · bán lẻ · bán buôn · chuyển kho · khác · **xuất bán doanh nghiệp khác** (bán chéo).
- Trạng thái: **Mới → Đã duyệt → Đã xác nhận**; nhánh **Đã hủy**.
- **Duyệt:** SL duyệt có thể **=, < hoặc >** SL yêu cầu; IMEI phải nhập đủ IMEI; có nút copy SL yêu cầu. Sau duyệt: phiếu nháp chuyển kho sinh **1 phiếu Xuất [C] Chuyển kho** (trừ tồn kho A); các loại khác **chưa** sinh phiếu.
- **Xác nhận:** SL xác nhận **phải bằng SL đã duyệt**; IMEI phải khớp (nếu không: *“IMEI này không nằm trong danh sách chuyển kho”*); có cột **SL lỗi**.
- Quy tắc trạng thái khi xoá: xoá SP trong phiếu nhập chuyển kho → phiếu nháp về *Đã duyệt, chưa xác nhận*; với phiếu Khác/NCC → **không** đổi trạng thái.
- Không thêm SP được sau khi phiếu đã duyệt/xác nhận. Doanh thu ghi nhận **theo ngày tạo phiếu**, không theo ngày sửa.

### 4.5 Hạn mức tồn kho

Nguồn: [Hạn mức tồn kho](https://pos.manual.nhanh.vn/pos/kho-hang/han-muc-ton-kho.md)
- Khái niệm: **tồn tối thiểu** (đảm bảo sẵn có) và **tồn tối đa** (tránh ứ đọng vốn).
- Danh sách: cột **Tồn hiện tại**; **số tồn min/max hiển thị màu xanh nếu trong hạn mức, màu đỏ nếu ngoài**.
- Thao tác: Xuất Excel; tích chọn → Thao tác → **Xoá các dòng đã chọn**; sửa từng dòng qua menu 3 gạch.
- Nhập: thêm tay (mỗi lần **chỉ 1 kho**) hoặc **Import Excel** (Tên SP, tồn tối thiểu, tồn tối đa).

### 4.6 Hàng lỗi

Nguồn: [Hàng lỗi](https://pos.manual.nhanh.vn/pos/kho-hang/hang-loi.md)
- Mục đích: kê khai SL hàng lỗi; **số lỗi không tính vào tồn có thể bán**; tổng hợp lý do lỗi.
- **Nhập hàng lỗi**: chỉ **ghi nhận/kê khai**, KHÔNG phải nhập kho. Sau khi lưu: **tồn Lỗi tăng, tồn Có thể bán giảm**.
- Với **IMEI**: chỉ ghi nhận số lượng; phải vào danh sách IMEI để **đổi IMEI sang trạng thái Lỗi**.
- Thao tác hàng loạt: Xuất Excel · **Lập phiếu xuất [N]** (trả NCC, trừ tồn lỗi; SP chọn phải **cùng kho + cùng NCC**) · **Lập phiếu nháp xuất [N]** · **Xóa sản phẩm đã chọn** (tồn Lỗi chuyển về **Có thể bán**).
- Thao tác từng dòng: Sửa số lượng (phải lọc theo kho trước) · Sửa mô tả (lý do lỗi) · Xóa.
- 2 hướng xử lý: trả NCC / **xuất huỷ** (phiếu xuất kho khác, điền cột SL và **SL lỗi**).
- **Kiểm kho bắt buộc phải kiểm cả hàng lỗi** vì hàng lỗi vẫn tính tồn trong kho.

### 4.7 Hạn sử dụng sản phẩm

Nguồn: [Hạn sử dụng sản phẩm](https://pos.manual.nhanh.vn/pos/kho-hang/han-su-dung-sp.md)
- Cập nhật hạn dùng để kiểm tra hàng hoá, lên kế hoạch bán sớm; xem ngày nhập kho, ngày hết hạn, **trạng thái đã kiểm tra**.
- Nhập liệu 2 cách: trong **phiếu nhập kho** (điền **Ngày cảnh báo trước** + **Ngày hết hạn**) hoặc **Thêm cảnh báo** trực tiếp.
- Có **thông báo trình duyệt** (phải cho phép notification). **Chỉ áp dụng cảnh báo với phiếu nhập hàng.**
- Quy tắc: sửa ở đây **độc lập**, không ảnh hưởng module khác; sửa ở phiếu nhập kho **thì** thay đổi theo; **xoá phiếu XNK thì phiếu cảnh báo KHÔNG mất**.
- Thao tác: Sửa · Xoá · Đổi trạng thái · Xuất dữ liệu (tích chọn → Thao tác).

### 4.8 Vị trí sản phẩm trong kho

Nguồn: [Giới thiệu vị trí SP trong kho](https://pos.manual.nhanh.vn/pos/kho-hang/vi-tri-san-pham-trong-kho/gioi-thieu.md)
- Tình huống: nhân viên kho mới tìm được vị trí SP; **giảm từ 2 người kho xuống 1**; xếp hàng ở bất kỳ vị trí trống mà vẫn quản lý được.
- Quy trình 4 bước: **xây dựng vị trí** → **xếp hàng vào kho** (song song với lập phiếu XNK/phiếu trả hàng) → **lấy hàng khỏi kho** (mang chứng từ xuất hàng) → **báo cáo diện tích lưu kho** (% đã dùng / còn trống).
- Trang con: Danh mục vị trí · Vị trí sản phẩm · Hoá đơn xuất nhập vị trí · Báo cáo diện tích lưu kho. *Tài liệu không nêu chi tiết cột của các trang này trong phạm vi đã đọc.*

### 4.9 Các trang kho khác

Có trong mục lục nhưng **chưa khảo sát chi tiết**: Dự báo nhập hàng · Bán chéo doanh nghiệp · Tổng công ty · Lịch sử XNK. (Lịch sử XNK được nhắc ở danh sách XNK dưới dạng **log sửa/xoá phiếu**.)

---

## 5. Module Kênh bán

### 5.1 Cửa hàng → Bán lẻ (màn hình POS)

Nguồn: [Bán lẻ](https://pos.manual.nhanh.vn/pos/kenh-ban/cua-hang/ban-le.md) — **trang mô tả màn hình POS chi tiết nhất trong toàn bộ tài liệu.**

**Định nghĩa:** hoá đơn bán lẻ = giao dịch trực tiếp tại cửa hàng; **tách biệt hoàn toàn** với đơn hàng ship/COD (module Đơn hàng riêng).

**Danh sách hoá đơn bán lẻ** (`/pos/bill/index`) — cột:
| Cột | Nội dung |
|---|---|
| ID | **Cũng chính là ID phiếu xuất kho** → lọc lại ở module Kho hàng |
| Sản phẩm | Ảnh – tên – SP tặng kèm – SL tặng; icon **quà đã tặng** vs **nợ quà tặng** |
| Giá | Giá bán lẻ (đen) + tổng chiết khấu từng SP (đỏ); 2 icon: **CK tự động theo CTKM** vs **CK tay** |
| Chiết khấu | Tổng tiền CK + **% giảm giá/tổng hoá đơn**; icon mũi tên = CK tự động |
| Thanh toán | Số tiền + phương thức |
| Ghi chú | **2 loại: ghi chú tự động** (không sửa được: tên CTKM, điểm tích luỹ, thông tin HĐ đổi hàng) **và ghi chú thủ công** |

**6 icon thanh toán:** Tiền khách đưa · Tiền mặt · Tiền chuyển khoản · Tiền quẹt thẻ · **Tiền tiêu điểm** · **Tiền trả góp**.

**3 sheet/tab:** Tất cả · Bộ lọc đã lưu · **Xác nhận thanh toán** (giao dịch chuyển khoản chờ đối chiếu).

**Nút Thao tác (hàng loạt):** Tạo phiếu đổi trả hàng (nhập ID hoá đơn) · Xuất Excel · **Gắn nhãn hoá đơn** · **Tạo hoá đơn điện tử nháp** / **Phát hành hoá đơn điện tử** (popup chọn thông tin xuất HĐ).

**Phím tắt màn hình bán lẻ (chỉ nơi tài liệu nêu rõ phím tắt):**
| Phím | Hành động |
|---|---|
| **F1** | Thêm hoá đơn (mở tab hoá đơn mới) |
| **F3** | Tìm sản phẩm |
| **F4** | Khách hàng |
| **F6** | Chiết khấu tổng hoá đơn |
| **F8** | Tiền mặt |
| **F9** | Lưu hoá đơn |
| **F10** | Lưu và in hoá đơn |

**Luồng thêm hoá đơn 4 bước:**
1. **Nhập sản phẩm** — 3 cách với máy quét: quét 1 lần = SL 1, quét 2 lần = SL 2; hoặc quét 1 lần rồi sửa cột SL; hoặc bật ô số lượng cạnh ô tìm kiếm rồi quét/điền/Enter. Gõ tay cần **tối thiểu 3 ký tự**.
2. **Nhập khách hàng** — có thể **bắt buộc nhập KH** (cài đặt); DN dùng kế toán **bắt buộc nhập KH** để kiểm soát thu chi/công nợ.
3. **Thanh toán** — nhập chiết khấu tổng (F6); chọn phương thức; với kế toán phải điền **đúng số tiền từng phương thức + chọn đúng đầu tài khoản tiền**; khách nợ thì **để trống phần thanh toán**; chọn **ngày hẹn thanh toán**.
4. **Lưu** — F9 (không in) hoặc F10 (lưu + in); có tuỳ chọn bật in hoá đơn/in hoá đơn quà tặng mặc định.

**Quy tắc nghiệp vụ quan trọng (POS):**
- Một hoá đơn **chỉ được dùng chiết khấu theo SP HOẶC chiết khấu tổng hoá đơn**, không dùng đồng thời.
- Phân quyền: nên cho NV **sửa chiết khấu**, **không nên cho sửa giá bán**.
- Quà tặng: tự động (theo CT quà tặng) hoặc thủ công (Thao tác → Quà tặng, BHMR, Ghi chú → chọn SP → SL → **tích nợ quà tặng nếu cửa hàng không đủ SL** → ghi chú). *Chỉ tích nợ quà tặng khi thiếu hàng, nếu không sẽ lệch tồn.*
- Có thể mở **nhiều hoá đơn song song** trên cùng giao diện bằng nút `+` hoặc **F1**.
- Có thể **bỏ chiết khấu tự động / bỏ tích điểm tự động** cho từng khách.
- Nhân viên bán hàng: khai báo để tính lương/theo dõi doanh số; có cài đặt **bắt buộc nhập NV bán hàng**.

**Màn hình POS là “multi-tab”**: mỗi hoá đơn là 1 tab; tab lưu trong **IndexedDB** (`POS_BUS_<mã> / posBills`) — FAQ hướng dẫn xoá Object Store khi mở quá nhiều tab.

**Chi tiết hoá đơn** — 3 sheet: Thông tin (upload ảnh/file đính kèm) · **Lịch sử sửa** (người sửa, ngày, dữ liệu sửa) · **Hoá đơn trả hàng** (1 HĐ bán lẻ có thể sinh **nhiều** HĐ trả hàng).
**Nút Thao tác:** **Hạch toán lại kế toán** · **Tiếp nhận bảo hành, sửa chữa**.

**Đối soát thanh toán tại quầy (QR/chuyển khoản)** — các tình huống được đặc tả:
- Khách **chuyển thừa** → cảnh báo thu ngân trả lại tiền thừa; lưu lịch sử giao dịch.
- Khách **chuyển thiếu** → cảnh báo; thu ngân cho khách thanh toán tiếp (tiền mặt/chuyển khoản, hiện QR số còn lại).
- Khách **sửa nội dung chuyển khoản** → hệ thống không tự xác nhận; thu ngân vào tab **Chờ xác nhận** chọn đúng giao dịch; chọn sai thì vào tab **Hủy xác nhận**.
- Nút **Kiểm tra giao dịch** + **Làm mới** khi không nhận được thông báo.
- Tab xác nhận thanh toán **chỉ lưu giao dịch chuyển khoản cho hoá đơn**, không gồm chi tiền điện nước…

### 5.2 Bán sỉ

Nguồn: [Bán sỉ](https://pos.manual.nhanh.vn/pos/kenh-ban/cua-hang/ban-si.md)
- Danh sách riêng (`/pos/bill/wholesale`); khác bán lẻ ở chỗ có thêm cột **Cửa hàng** và **Tổng tiền**.
- Cột: ID (cũng là ID phiếu xuất kho) · Cửa hàng · Giá · Chiết khấu · Tổng tiền · Ghi chú · Cài đặt (In hoá đơn – tạo phiếu đổi trả – sửa – xoá).
- Nghiệp vụ thêm HĐ: khai báo KH (Tên, SĐT, địa chỉ, **nguồn khách hàng**) → khai báo SP (chọn đúng chế độ tìm: **Tìm sản phẩm / Cân điện tử / IMEI**) → thanh toán → lưu (F9/F10).
- Có **Bán chéo doanh nghiệp**.
- Chi tiết: 3 sheet (Thông tin, Lịch sử sửa, Hoá đơn trả hàng); Thao tác: **In bảo hành** · **Hạch toán lại kế toán** · **Tiếp nhận bảo hành, sửa chữa**.

### 5.3 Kết ca

Nguồn: [Kết ca](https://pos.manual.nhanh.vn/pos/kenh-ban/cua-hang/ket-ca.md)
- Mục đích: quản lý tiền **vào ca – bán hàng – kết ca – lịch sử kết ca – bàn giao ca sau – báo cáo kết ca**.
- **Tiền vào ca**: màn hình bán lẻ → **Thao tác Nhanh → Tiền vào ca**. 2 cách khai: **đếm số tờ theo mệnh giá** (hệ thống tự tính tổng) hoặc **nhập thẳng tổng tiền**.
  - Quy tắc: 1 thu ngân có thể mở ca và đăng nhập **nhiều thiết bị**, mọi hoá đơn của tài khoản đó ghi vào ca của người đó. **Quên khai tiền đầu ca** → khi kết ca có thể chọn lại giờ vào ca + khai tiền đầu ca.
- **Chi phí phát sinh**: 2 cách — Thao tác Nhanh → **Thêm chi phí khác** (chọn ca, tên chi phí, tổng tiền, thêm nhiều dòng) hoặc khai tại màn hình **Kết ca**.
- **Kết ca**: nhập **tiền bàn giao thực tế** + chi phí tiền mặt + ghi chú → **Kết ca** hoặc **Kết ca và in phiếu bàn giao** (biên bản bàn giao cho 2 nhân viên xác nhận).
  - **Cảnh báo: đã kết ca là không quay ngược được.**
- **Lịch sử kết ca**: Thao tác nhanh → Lịch sử kết ca.
- **Báo cáo kết ca** (`/report/retail/workshift`) — công thức được nêu rõ:
  | Chỉ tiêu | Công thức |
  |---|---|
  | Tiền trong ca | **Doanh thu tiền mặt − Chi phí khác − Trả hàng bằng tiền mặt** |
  | Tiền cuối ca | **Tiền đầu ca + Tiền trong ca** |
  | Chênh lệch | **Tiền bàn giao thực tế − Tiền cuối ca** |
  Có tab **Chi phí** riêng + nút xem tổng chi phí.

### 5.4 Trả hàng

Nguồn: [Trả hàng](https://pos.manual.nhanh.vn/pos/kenh-ban/cua-hang/tra-hang.md)
- 2 hình thức: **Trả hàng có hoá đơn** và **Trả hàng không có hoá đơn**.
- **Trả từ hoá đơn**: tìm HĐ (theo ID, SĐT, ngày, sản phẩm) → nút Đổi trả hàng, hoặc Thao tác → **Tạo phiếu đổi trả hàng** → nhập ID HĐ → Lưu.
  - Trường hợp 1: chỉ trả — **giữ lại SP khách trả, xoá SP không trả** (icon thùng rác), sửa SL.
  - Trường hợp 2: trả + **mua mới** (mục “Sản phẩm mua mới”, gõ tên/tít mã vạch). *Khuyến nghị: đổi SP có giá **≥** SP trả.*
  - 3 khối tiền: **Tổng trả** (Phí trả hàng + Tổng tiền trả hàng) · **Tổng mua** (tiền SP mới, **tiêu điểm, chiết khấu, coupon**) · **Thanh toán** (hệ thống tự báo *“cần trả khách”* hoặc *“khách cần trả thêm”*).
  - Nhân viên bán hàng / thu ngân gắn theo hoá đơn SP mua mới.
- **Trả không cần hoá đơn**: phải **chọn cửa hàng (kho nhận hàng trả)**; chọn **Loại phiếu** = trả từ HĐ bán lẻ hay bán sỉ.
- Quy tắc IMEI: IMEI không tồn tại → hỏi **Đúng/Nhập lại** (chọn Đúng ⇒ sinh IMEI mới); IMEI gắn SP khác → báo *“IMEI không thuộc sản phẩm bạn đang thêm”*; với IMEI đích danh, **giá nhập = giá vốn = giá nhập trả vào** (coi như nhập hàng mới từ khách).
- Trả HĐ bán lẻ → **tự động trừ điểm tích luỹ** đã tặng; có cài đặt **“Chuyển điểm tích luỹ thành phí trả hàng”**.
- Dùng kế toán: điền loại tiền + tài khoản trả → **tự sinh 1 phiếu chi**.
- **Trả hàng có hoá đơn không được chọn cửa hàng** — mặc định theo cửa hàng của HĐ mua.

### 5.5 Hoá đơn điện tử (kênh bán)

Nguồn: [Danh sách hoá đơn điện tử](https://pos.manual.nhanh.vn/pos/kenh-ban/hoa-don-dien-tu/danh-sach-hoa-don-dien-tu.md)
- **4 tab:** **Cần xử lý** (HĐ lỗi) · **Hoá đơn nháp** · **Hoá đơn chờ xử lý** (đơn hệ thống *chưa* hỗ trợ tự động xuất: trả hàng không cần HĐ, trả hàng từ đơn hàng, đơn hoàn 1 phần) · **Đã phát hành**.
- **Bộ lọc:** Pháp nhân · Kho hàng · **Mã tham chiếu** · Khách hàng (tên/SĐT) · Ngày bán.
- **Mã tham chiếu = `ID doanh nghiệp` + `B` (bán lẻ/bán sỉ) hoặc `O` (đơn online) + `ID hoá đơn/đơn hàng`** → tra cứu HĐĐT theo mã này.
- **Cột:** Mã tham chiếu · Kênh bán (+ tên gian hàng nếu là sàn) · Kho hàng · Khách hàng · Tổng tiền · **Loại** (Nháp / Phát hành) · **Trạng thái** (Nháp / **Hoá đơn điều chỉnh** / **Hủy**) · **Lỗi** · tuỳ chọn (Xem trước / Tải hoá đơn / Xoá).
- **Thao tác:** Phát hành HĐĐT (nháp hoặc phát hành lại HĐ lỗi) · Xoá HĐ · **Cập nhật thông tin mới nhất** · Xuất dữ liệu.
- **Danh mục lỗi được liệt kê (rất hữu ích để đặc tả error states):** *Chưa ghép nối sản phẩm* · *Chưa điền VAT* · *Trạng thái hoá đơn không hợp lệ, chỉ điều chỉnh cho HĐ đã phát hành* · *Import không thành công* · *Trạng thái HĐ không hợp lệ, chỉ hỗ trợ phát hành HĐ nháp* · *Mã tham chiếu này đã sử dụng cho hoá đơn khác* · *Chưa truyền giá trị* · *Không tìm thấy thông tin hoá đơn* · *Mã tham chiếu này đã phát hành hoá đơn rồi* · *Không hỗ trợ điều chỉnh HĐ đối với HĐ sửa VAT* · *Bạn chưa nhập ID hoá đơn*.
- **Phát minh UI đáng chú ý:** nút **“Cập nhật thông tin VAT theo giá trị ở thông tin sản phẩm”** — tự điền lại VAT cho SP và điều chỉnh giá bán sao cho tổng tiền thanh toán không đổi (thay vì sửa tay từng HĐ).

### 5.6 Kết nối kênh bán & đồng bộ sàn

Nguồn: [Các bước đồng bộ cơ bản (Shopee)](https://pos.manual.nhanh.vn/pos/kenh-ban/shopee/cac-buoc-dong-bo-co-ban.md) — mô tả đủ chi tiết để suy ra mô hình chung cho Tiktok/Lazada/Tiki/Sendo.

**4 bước kết nối:** (1) Thêm kết nối → đăng nhập sàn (chọn quốc gia **Vietnam (VN)**, user/pass, **OTP SMS**, **Confirm Authorization**); (2) **Cấu hình đồng bộ**; (3) **Cấu hình kho**; (4) **Cài đặt ngày lấy hàng**.

**Cấu hình đồng bộ (các toggle – đây là “settings inventory” của module):**
- Tên gian hàng.
- **Tự động tải đơn hàng về Nhanh** (Tự động / thủ công).
- **Tự động tải bản in phiếu gửi** về Nhanh (giúp in được phiếu khi đơn đã ở trạng thái Đang chuyển/Thành công).
- **Ngày bắt đầu tạo xuất nhập kho** — đơn thành công trước mốc này **không** sinh phiếu XNK (tránh âm kho khi đã kiểm kho). Mặc định khi kết nối mới = **ngày hiện tại + 1**.
- **Gắn toàn bộ đơn cho khách hàng** (vì đơn Shopee bị ẩn thông tin KH).
- **Đồng bộ sản phẩm khi tải đơn**: *Đợi ghép sản phẩm* hoặc *Luôn lấy sản phẩm từ Shopee về*.
- **Tự động đẩy tồn kho từ Nhanh lên sàn**.

**Cấu hình kho – 3 chế độ:**
| Chế độ | Hành vi |
|---|---|
| **1 kho** | Chọn kho gắn đơn + kho đồng bộ tồn (1 kho) |
| **Đa kho** | Cấu hình kho trừ tồn khi đơn về; **chọn nhiều kho đẩy tồn**; gắn đơn vào kho tương ứng |
| **Kho FBS** (Fulfillment by Shopee) | Đơn FBS vẫn **trừ tồn tại Nhanh** nhưng **không đồng bộ tồn ngược lên Shopee**; Nhanh không can thiệp tồn FBS |

Quy tắc: SP trong đơn thuộc **cùng 1 kho** trên sàn → lấy theo cấu hình “Chế độ 1 kho”; thuộc **nhiều kho** → kéo về kho ở “Chế độ đa kho”.

**Đồng bộ sản phẩm – 2 trường hợp:**
- **TH1: đã có SP cả 2 bên** → *Tải sản phẩm từ sàn* → tab **Sản phẩm chưa ghép** → ghép nối **thủ công** (chọn SP Nhanh + cột **Số lượng** = SL tương ứng 1 SP sàn ↔ 1 SP Nhanh; combo thì điền SL quy đổi) hoặc **ghép bằng Excel** (dùng cột Mã SKU/Tên/ID SP sàn; Shopee format **`ID sản phẩm cha - ID sản phẩm con`**).
- **TH2: chỉ có SP trên sàn** → tích **“Tự động tạo sản phẩm trên Nhanh bằng SKU của Shopee”** → tạo SP **độc lập** cùng tên + tự ghép; **mã SKU sàn ↔ mã sản phẩm Nhanh**.

**Đồng bộ đơn hàng:** *Tải đơn* (khoảng thời gian **không quá 15 ngày**); kết quả trả về **theo từng đơn**: thành công / *SP chưa được tải về Nhanh* / *SP chưa được ghép nối*.
**Xử lý đơn sàn 3 bước:** **Báo sàn sẵn sàng đi giao** (tương đương “Chuẩn bị hàng”; 2 cách: qua **Biên bản bàn giao** hoặc tại danh sách đơn) → **In phiếu gửi sàn** (có mẫu riêng + in phiếu nhặt hàng nhiều đơn) → **Theo dõi cập nhật trạng thái** (tự động; nên tải đơn thủ công 1–2 lần/ngày; đơn hoàn phải đổi sang **Đã hoàn** hoặc thêm vào biên bản **Nhận HVC hoàn về**).
**Bảng map trạng thái sàn → Nhanh (Shopee)** được liệt kê đầy đủ 12 dòng: `UNPAID→Mới`, `READY_TO_SHIP→Đã xác nhận`, `SHIPPED/TO_CONFIRM_RECEIVE→Đang chuyển`, `CANCELLED (không đánh dấu giao)→Khách hủy`, `CANCELLED (đánh dấu đã giao)→Thất bại`, `INVALID→Hệ thống hủy`, `TO_RETURN→Xác nhận hoàn`, `STATUS_COMPLETED→Thành công`, `RETRY_SHIP→Đã xác nhận`…

**Đồng bộ tồn kho 2 chiều:** (1) đơn sàn về → đơn thành công → **trừ tồn Nhanh**; (2) nếu bật **“Tự động cập nhật tồn kho từ Nhanh lên sàn”** → mỗi lần tồn đổi, đẩy lên sàn. Có cài đặt **buffer tồn**: *“Tồn đồng bộ = tồn CTB − 10%”* (theo % hoặc trừ số lượng cố định) — dùng cho ngày sale.

**Chi phí quảng cáo:** liên kết shop để lấy **Shopee Ads** → tự cập nhật **6 tháng gần nhất**; xem ở `Kênh bán > Tài chính sàn TMĐT > Tiền quảng cáo`.

### 5.7 Tài chính sàn TMĐT & Đối soát COD

Nguồn: [Đối soát sàn](https://pos.manual.nhanh.vn/pos/kenh-ban/shopee/doi-soat-cod.md) · [Tài chính sàn TMĐT](https://pos.manual.nhanh.vn/pos/kenh-ban/tai-chinh-san-tmdt.md)

**Đối soát COD** (`/ecommerce/manage/index`) — 4 bước:
1. Thêm đối soát: `Tài chính sàn TMĐT > Đối soát COD > Thêm mới > chọn sàn`, hoặc `Kênh bán > [sàn] > Đơn hàng > Đã đồng bộ > Thao tác > Thêm đối soát`.
2. **Xuất file chi tiết giao dịch Ví** từ sàn (hướng dẫn riêng cho từng sàn):
   - **Shopee**: Kênh người bán → Tài chính → Số dư TK Shopee → Doanh thu → Xuất. File `.csv` phải **đổi sang `.xls`**, **sửa tên sheet = `Orders`**, **xoá 17 dòng đầu, giữ từ dòng 18**; nếu file chỉ có 1 cột → `Data > Text to Columns > Delimited > Comma, Tab`.
   - **TikTok**: Tài chính / Hồ sơ đã quyết toán → Xuất (up trực tiếp hoặc dùng file mẫu Nhanh).
   - **Lazada**: Seller Center → Tài chính → Thu nhập của tôi → lọc ngày → tải (import thẳng).
   - **Tiki**: Seller Center → Thanh toán → Sao kê → Xuất sao kê (file gửi về **email**); chỉ giữ sheet **“GD bán hàng”**.
   - **Sendo**: `ban.sendo.vn` → Đơn hàng → tab hoàn tất → Xuất excel (`.csv`, Text to columns).
3. Điền file đối soát (hoặc dùng file mẫu Nhanh).
4. **Import đối soát** + chọn **ngày nhận tiền đối soát**.

**2 nhánh vận hành (rất đáng chú ý về mặt thiết kế sản phẩm):**
- **Không dùng kế toán** (ít đơn): chỉ cần đánh dấu đơn **“Đã đối soát đơn hàng”** — vào `[sàn] > Đơn hàng > tab Hoàn thành > Chưa thanh toán`, tìm đơn, menu 3 gạch → *Đã đối soát đơn hàng*; đơn được gắn **icon đối soát**; xem lại ở tab **Đã thanh toán**.
- **Có dùng kế toán**: tạo **quỹ “Ví trên sàn”** → import đối soát chọn **Tài khoản nhận tiền = Ví trên sàn** → **ngày đối soát = ngày sàn trả tiền về ví** → **Bút toán chuyển quỹ** từ Ví sàn về ngân hàng. Khuyến nghị import hằng ngày.

**Kiểm tra chênh lệch:** tab **Chênh lệch đối soát** (`/ecommerce/manage/cods`); xem chi tiết loại phí ở `Báo cáo tài chính sàn` (`/ecommerce/finance/dashboard`). Sau 1 tháng nên chốt chi phí sàn và hạch toán: **Nợ 112/Có 131** (nhận tiền) và **Nợ 641/Có 112** (chi phí sàn).

Trang con của Tài chính sàn: **Tổng hợp chi phí · Shopee · Tiktok · Tiền quảng cáo · Trả hàng hoàn tiền đơn sàn** (*chưa khảo sát chi tiết*).

### 5.8 Website (kênh bán)

Nguồn: [Website (kênh bán)](https://pos.manual.nhanh.vn/pos/kenh-ban/website.md) · [Module Website](https://pos.manual.nhanh.vn/pos/website/website-gt.md)
- Bật kênh Website ⇒ chọn **giao diện mẫu** đa ngành, **kết nối tên miền** (tên miền riêng hoặc **tên miền demo** `*.store.nhanh.vn`), **quản lý bán hàng tập trung** (đồng bộ SP, đơn, khuyến mãi).
- Luồng: `Kênh bán > Thêm kênh bán > Website > Tạo Website > Dùng thử ngay` → chọn tên miền → website xuất hiện trong danh sách kênh bán.
- Module Website đầy đủ gồm: Nội dung website · Cài đặt website · Menu · Tin tức · Banner · Album · Liên hệ · Newsletters · Hiển thị SP trên nhiều danh mục · **Quản lý mã nhúng** (vchat, Google Analytics, chat Zalo, Google Webmaster, hiệu ứng bông tuyết/hoa đào) · **Tên miền** (xác thực tên miền + chọn sự kiện Facebook Pixel, trỏ tên miền, đăng ký với Bộ Công Thương, email `ten@tenmien`) · Bán hàng trên Facebook · Google Shopping · Dịch nhiều ngôn ngữ.

### 5.9 Các trang kênh bán khác (chưa khảo sát chi tiết)

Xác nhận thanh toán hoá đơn · Hoá đơn nháp · **Nợ quà tặng** · Tìm hoá đơn · **Nhập máy cũ** · Tiếp thị liên kết · Facebook Shop (Live Shopping, quản lý đơn Messenger, Adslive) · Kết nối kênh bán (Logic tải đơn hàng, In đơn sàn bằng Extension, Chuyển hạn gian sàn).

---

## 6. Module Đơn hàng

### 6.1 Danh sách đơn hàng

Nguồn: [Danh sách đơn hàng](https://pos.manual.nhanh.vn/pos/don-hang/danh-sach-don-hang/danh-sach-don-hang.md)
- Danh sách chia **nhiều tab, mỗi tab = 1 khâu xử lý** (từ tạo mới → thành công). *Tài liệu không liệt kê tên đầy đủ các tab trong phạm vi đã đọc.*

**Bộ lọc (được mô tả chi tiết):**
- **Trạng thái** (18 giá trị — xem 6.2).
- **Loại đơn** (13 giá trị — bảng enum quan trọng):
  | Loại | Ý nghĩa |
  |---|---|
  | Chuyển hàng – Giao hàng tận nhà | DN vận chuyển cho khách |
  | Chuyển hàng – **Khách trả lại hàng** | Khách gửi trả SP về DN |
  | Chuyển hàng – **Đổi sản phẩm** | Khách đã trả hàng cũ/lỗi, đơn này ghi nhận việc giao hàng mới |
  | Chuyển hàng – **Hàng chuyển kho** | Chuyển SP sang kho khác |
  | Chuyển hàng – **Đơn hoàn một phần** | Đơn phụ khi khách trả 1 phần |
  | Chuyển hàng – **Đền bù mất hàng** | Đơn phụ khi xác nhận mất hàng khi giao |
  | **Đặt trước** | Khách thanh toán trước 1 phần để giữ hàng |
  | **Mua tại quầy** | Khách đến cửa hàng mua & thanh toán trực tiếp |
  | **Mua nhanh** | |
  | **Dùng thử** | Trải nghiệm SP trước khi mua (công nghệ/gia dụng/điện tử) |
  | **Đổi quà** | Đổi điểm tích luỹ sang quà |
  | **Xin báo giá** | |
- **Bàn giao** (5 giá trị): Đã bàn giao · Chưa bàn giao · Đã bàn giao chuyển hoàn · Chưa bàn giao chuyển hoàn · **Đã cho vào tour**.
- **NV CSKH**: lọc đơn có NV là người đổi trạng thái từ **Mới** sang *Chờ xác nhận/Đang xác nhận/Đã xác nhận*.
- **Người đóng gói**: theo NV thao tác đóng gói; nếu không có thì lấy **người in đơn**.

**Thao tác hàng loạt:** **Gộp các đơn đã chọn** · **Gắn nhãn các đơn đã chọn** · **Xuất dữ liệu** · **Đổi trạng thái** · **Nhập trạng thái từ Excel**.

**Thao tác từng đơn (kèm trạng thái áp dụng — bảng điều kiện rất giá trị):**
| Thao tác | Trạng thái áp dụng |
|---|---|
| Xác nhận đơn hàng (mở màn sửa đơn, chọn trạng thái xác nhận) | Mới, Chờ xác nhận, Đang xác nhận, Đã xác nhận |
| **Gửi sang hãng vận chuyển** | Mới, Chờ xác nhận, Đang xác nhận, Đổi kho hàng, Đã xác nhận, Đang đóng gói, Đã đóng gói |
| **Tạo hoá đơn bán lẻ / bán sỉ từ đơn** (khách đặt rồi tự đến lấy) → đơn chuyển **Mua tại quầy** + **Thành công** | Mới … Đã đóng gói |
| Sửa đơn / Xoá đơn (chỉ đơn **chưa gửi HVC**) | Mới … Đã đóng gói |
| **Tạo phiếu trả hàng** (Trả hàng chuyển hàng / Trả hàng tại quầy) | Chờ thu gom, Đang chuyển, Thành công, Thất bại, Đang hoàn, Đã hoàn |
| **Khiếu nại** | Chờ thu gom, Đang chuyển, Đang hoàn |
| **Báo hãng vận chuyển hủy đơn** | Chờ thu gom, Đang chuyển, Đang hoàn |
| **Copy đơn hàng** | **Tất cả** trạng thái |

**Logic đơn Đặt trước** (được đặc tả riêng): SP không trừ tồn mà ở trạng thái **Đặt trước**; khi có hàng → đổi sang **Chờ thu gom** thì đơn **tự chuyển loại** về *Chuyển hàng – Giao hàng tận nhà*; tồn chuyển từ cột **Đặt trước** sang **Đang chuyển**; **nếu không có tồn thì không đổi được**.

**FAQ có giá trị đặc tả:** xuất Excel giới hạn **10.000 dòng**; đổi trạng thái hàng loạt nhanh nhất = *Thêm mới > Nhập trạng thái từ Excel* (điền ID đơn + trạng thái); cài `&icpp=200` để xem 200 đơn/trang; phân quyền “chỉ thấy đơn mình tạo” ở `Cài đặt > Nhân viên > Phân quyền xem dữ liệu`.

### 6.2 Trạng thái đơn hàng (18 trạng thái) — enum cốt lõi

Nguồn: [Trạng thái đơn hàng](https://pos.manual.nhanh.vn/pos/don-hang/danh-sach-don-hang/trang-thai-don-hang.md)

| # | Trạng thái | Ý nghĩa | Tồn kho SP trong đơn |
|---|---|---|---|
| 1 | **Mới** | Khách đã đặt, chưa xử lý | Tạm giữ |
| 2 | **Chờ xác nhận** | Chờ NV xác nhận | Tạm giữ |
| 3 | **Đang xác nhận** | NV CSKH đang gọi điện xác nhận | Tạm giữ |
| 4 | **Đã xác nhận** | Đã xác nhận thành công | Tạm giữ |
| 5 | **Hết hàng** | SP khách đặt đã hết | **Không ghi nhận tồn** |
| 6 | **Đổi kho hàng** | Chuyển từ CH hết hàng sang CH còn tồn | Tạm giữ |
| 7 | **Đang đóng gói** | Đang nhặt + đóng gói | Tạm giữ |
| 8 | **Đã đóng gói** | Đóng gói xong | Tạm giữ |
| 9 | **Chờ thu gom** | Đã bắn sang HVC, chờ HVC tiếp nhận | Đang chuyển |
| 10 | **Đang chuyển** | Đang giao cho khách | Đang chuyển |
| 11 | **Thành công** | Giao thành công | **Sinh phiếu XNK, giảm Tồn + Đang chuyển** |
| 12 | **Thất bại** | Chưa giao được (khách bận/vắng/hẹn lại) | Đang chuyển |
| 13 | **Khách hủy** | Khách không lấy hàng | Không ghi nhận tồn |
| 14 | **Hệ thống hủy** | Hệ thống tự hủy | Không ghi nhận tồn |
| 15 | **HVC hủy** | Hãng vận chuyển hủy | Không ghi nhận tồn |
| 16 | **Đang hoàn** | Đang luân chuyển trả về DN | Đang chuyển |
| 17 | **Xác nhận hoàn** | HVC cập nhật đã hoàn | Đang chuyển |
| 18 | **Đã hoàn** | DN đã thực nhận hàng hoàn | **Giảm Đang chuyển, tăng Có thể bán** |

**Bảng map trạng thái HVC → Nhanh** (22 giá trị, ví dụ): `ready_to_pick/picking/money_collect_picking→Chờ thu gom`, `picked/storing/transporting/sorting/delivering/money_collect_delivering→Đang chuyển`, `delivered→Thành công`, `delivery_fail→Thất bại`, `waiting_to_return/return/return_transporting/return_sorting/returning/return_fail/damage/lost→Đang hoàn`, `returned→Xác nhận hoàn`, `exception→Đang chuyển hoàn`, `cancel→HVC hủy`.

**4 quy tắc cập nhật trạng thái:** (1) đơn qua cổng Nhanh/kết nối riêng → **HVC tự cập nhật**; đơn **Tự vận chuyển** → DN tự cập nhật; (2) với HVC, **không** cập nhật “Đã chuyển hoàn” theo file đối soát phí — để nhân viên kho tự đổi khi nhận hàng (tránh mất hàng); (3) đơn tự vận chuyển: **Thành công = Đã thanh toán**; (4) **chỉ nhân viên kho** được phân quyền đổi trạng thái đơn; NV CSKH chỉ cần quyền **Xác nhận đơn hàng** để đổi sang Khách hủy/Hệ thống hủy.

**Bảng “trạng thái đơn → trạng thái tồn”** (dùng cho kiểm kho & tính tồn):
- *Tạm giữ*: Mới, Chờ xác nhận, Đang xác nhận, Đã xác nhận, Đang đóng gói, Đã đóng gói, Đổi kho hàng
- *Đang giao hàng*: Chờ thu gom, Đang chuyển, Thất bại, Đang hoàn, Xác nhận hoàn
- *Có thể bán*: Khách hủy, Hệ thống hủy, HVC hủy, Hết hàng, Đã hoàn

### 6.3 Chi tiết đơn hàng (tab theo trạng thái — pattern UI đáng học)

Nguồn: [Chi tiết đơn hàng](https://pos.manual.nhanh.vn/pos/don-hang/danh-sach-don-hang/chi-tiet-don-hang.md)

**Tab set thay đổi theo giai đoạn đơn:**
| Nhóm | Trạng thái | Tab hiển thị |
|---|---|---|
| **Chưa gửi HVC** | Mới, Chờ xác nhận, Đang xác nhận, Đã xác nhận, Hết hàng, Đổi kho hàng, Đang đóng gói, Đã đóng gói, Khách hủy | **Thông tin · Xác nhận · In & đóng gói · Gửi vận chuyển** (đơn *Đổi kho hàng* và *Hệ thống hủy* **không có** tab Xác nhận) |
| **Đã gửi HVC** | Chờ thu gom, Đang chuyển, Thành công, Thất bại, HVC hủy, Đang hoàn, Xác nhận hoàn, Đã hoàn | **Thông tin · Lịch trình · In & đóng gói · Thanh toán · Khiếu nại** (đơn **tự vận chuyển** không có *Lịch trình* và *Thanh toán*) |

**Tab Thông tin** — các khối cấu hình vận chuyển theo từng đơn:
- **Thu phí khi khách từ chối nhận hàng** (chỉ **SPX, GHN, Viettel, VN POST** hỗ trợ). Nếu thu: gửi qua Nhanh → đơn về **Hoàn**, Nhanh đối soát trừ tiền PVC, phí chuyển khoản riêng; gửi tự kết nối → DN tự đối soát.
- **Khai giá hàng hoá** (đền bù theo giá trị đơn, phát sinh phí khai giá; bỏ trống thì lấy theo giá trị hàng hoá).
- **Cài đặt cho khách xem hàng**: cho xem & thử / cho xem không thử / cho xem không thu ship / không cho xem.
- **Hàng dễ vỡ** (GHTK, VN POST; VN POST thu thêm phí).
- **Khối lượng đơn**: Cân nặng (theo khối lượng SP; **mặc định 500 gram** nếu trống) + **Cân nặng kiện hàng** & **Kích thước** (kích thước đơn **ưu tiên hơn** kích thước SP).
- **Thao tác:** Đổi kho hàng (sinh log) · **Gửi lại Email đơn hàng**.

**Tab Xác nhận** — chỉ đơn chưa gửi HVC; là form sửa đơn + chọn **trạng thái xác nhận** → Lưu (đơn chuyển trạng thái).
**Tab In & đóng gói** — *không hỗ trợ đơn Thành công/Đang hoàn/Đã hoàn*. Gồm: **In phiếu gửi** (nhiều kích cỡ/loại giấy; in xong đơn tự chuyển **Đang đóng gói**) · **Lý do không nhặt được hàng** (Hết hàng / Lý do khác → đơn chuyển **Hết hàng**) · **Thống kê trạng thái đơn của khách** (tỷ lệ thành công/chuyển hoàn của khách **trên toàn hệ thống Nhanh**, có cài đặt chỉ lấy dữ liệu DN mình) · **Đổi kho hàng** (kèm bảng kiểm tra tồn từng kho).
**Tab Gửi vận chuyển** — sửa **phí ship báo khách** + chọn lại hãng VC → Gửi.
**Tab Lịch trình** — lịch trình chi tiết (theo HVC) + lịch trình tóm tắt (theo action đổi trạng thái).
**Tab Thanh toán** — các khoản phí; chưa đối soát hiển thị **“Đơn hàng chưa được thanh toán”**.
**Tab Khiếu nại** — quản lý yêu cầu khiếu nại với HVC + phản hồi từ khách.

### 6.4 Quy trình xử lý đơn hàng (end-to-end)

Nguồn: [Quy trình xử lý đơn hàng](https://pos.manual.nhanh.vn/pos/don-hang/quy-trinh-xu-ly-don/quy-trinh-xu-ly-don.md)

**Phân luồng theo bộ phận (mỗi bộ phận chỉ làm việc trong 1 tab):**
- **Sale/telesale** → Tạo đơn + Xác nhận đơn
- **Nhân viên kho** → In & đóng gói
- **CSKH đơn hàng** → Theo dõi, báo phát, khiếu nại
- **Kế toán** → Đối soát công nợ

**5 nguồn đơn:** Website (Nhanh cấp hoặc tích hợp API) · Kênh chat/giỏ hàng **Facebook Shop** (qua `vpage.nhanh.vn`) · **Sàn TMĐT** · **API** bên thứ 3 · Tự tạo trên phần mềm (**nguồn Admin**).

**Luồng chuẩn:**
1. **Tạo đơn** (màn thêm mới đơn hàng).
2. **Xác nhận đơn** (gọi khách): Mới → *Chờ xác nhận* (không gọi được) / *Đang xác nhận* (cần đổi SP, SL) → **Đã xác nhận** (Lưu hoặc **Lưu và In** → tự sang **Đang đóng gói**) / **Khách hủy** (+ lý do hủy) / **Hết hàng**.
3. Sau xác nhận: **biên bản bàn giao**, **in phiếu nhặt hàng**, gửi biên bản sang hãng để **lấy mã vận đơn**, **in phiếu gửi + đóng gói**.
4. **Gửi đơn sang HVC** — bưu tá đến lấy hàng; dùng biên bản bàn giao để kiểm đủ số đơn.
5. **Theo dõi**: tab **Cần xử lý** chia đơn theo vấn đề (*Mới thất bại, HVC hủy đơn, Đang hoàn, Chuyển chậm, Vượt cân*) + tab **Khiếu nại**.
6. **Xác nhận hàng hoàn**: HVC trả về trạng thái cuối là **Xác nhận hoàn** (dù lịch trình HVC là “Đã hoàn”); khi hàng về shop, kiểm tra rồi đổi sang **Đã hoàn** (hoặc xác nhận hoàn qua biên bản bàn giao) để **cộng tồn**.
7. **Đối soát thanh toán**: (a) Nhanh đối soát với HVC — HVC đối soát với Nhanh **1 ngày sau** khi đơn Thành công/Chuyển hoàn; (b) Nhanh đối soát & thanh toán COD cho shop qua **tài khoản ngân hàng** (phải ký hợp đồng vận chuyển, cung cấp CCCD chủ TK; đổi TK **bắt buộc email** tới `contact@nhanh.vn` từ email đã ký HĐ, kèm CMT/phiếu đề nghị có dấu đỏ).
   **Lịch trả COD theo hãng:** Vietnam Post, Viettel Post, J&T, Best: T2–T6; **Giao Hàng Nhanh**: T2–T6 cho đơn **nội tỉnh**, **T2/T4/T6** cho đơn **liên tỉnh**. Ngày thanh toán = **ngày vận đơn Thành công + 2** (không tính lễ, T7, CN).

---

## 7. Module Khách hàng

### 7.1 Khách hàng & thêm khách hàng

Nguồn: [Khách hàng](https://pos.manual.nhanh.vn/pos/khach-hang/khach-hang.md) · [Thêm khách hàng](https://pos.manual.nhanh.vn/pos/khach-hang/them-khach-hang.md)

**3 khái niệm nền:** **Cấp độ** (tự động đổi theo tiêu chí) · **Nhóm** (do người dùng tự phân, **không tự đổi**) · **Điểm tích luỹ** (quy đổi từ tổng tiền mua theo tỉ lệ tự quy định).

**Danh sách khách hàng — 5 tab (pattern “phân khúc tự động”):**
| Tab | Điều kiện |
|---|---|
| Bộ lọc | Tất cả KH + bộ lọc |
| **Mua nhiều** | số lần mua **> 3** |
| **Mua nhiều, sinh nhật trong tháng** | > 3 lần mua **và** sinh nhật trong tháng hiện tại |
| **Mua thường xuyên** | **chu kỳ mua < 60 ngày** và số lần mua > 3 |
| **Lâu chưa mua** | chu kỳ < 60 ngày, **số ngày chưa mua > 60**, số lần mua > 3 |

**Cột:** Cấp độ · Nhóm · **Chu kỳ mua hàng** (thời gian TB khách quay lại, từ lần mua đầu tới lần cuối) · menu 3 gạch (Sửa/Xoá).
**Bộ lọc đặc biệt:** **Số ngày chưa mua** (tính từ lần mua cuối tới hiện tại).
**Thao tác hàng loạt (11 mục):** Gửi SMS · Gửi Email · **Tặng điểm/trừ điểm** · **Tặng tiền/trừ tiền tích luỹ** · Gọi điện/Nhắn tin/Gửi email/Nhận cuộc gọi (chỉ **ghi log**) · **Cập nhật nhóm khách hàng**.

**Form thêm khách hàng — các trường & quy tắc:**
- **Mã khách hàng** = mã thẻ (phải tạo trước trong chương trình phát hành thẻ).
- **Điện thoại**: chỉ nhận số đúng định dạng — **không chữ cái/ký tự đặc biệt, 10–11 số, bắt đầu bằng 0**; nếu 10 số không bắt đầu bằng 0 thì **tự thêm số 0** thành 11 số.
- **Địa chỉ**: chỉ nhập số nhà/phố/phường (quận/huyện/tỉnh nhập ở trường khác).
- **Nhân viên bán hàng** phụ trách: **chỉ tài khoản NV đó xem được KH của mình**.
- **Khách giới thiệu** (phục vụ tặng điểm/giảm giá khi giới thiệu).
- Công ty, **Mã số thuế**, Ghi chú.
- **Hạn mức công nợ riêng** (xem 10.1).

**Chi tiết khách hàng — 5 tab:** **Thông tin** · **Lịch sử mua hàng** · **Lịch sử chăm sóc** · **Bảo hành** · **Lịch sử sửa**.
Công thức được nêu rõ:
- `Tổng SP đã mua = Tổng SP hoá đơn mua − Tổng SP hoá đơn trả`
- `Tổng tiền tích luỹ = Tổng tiền đã mua − Tổng tiền đã trả lại − Tổng tiền sử dụng điểm tích luỹ`
- **Ngày bắt đầu mua / Ngày mua cuối cùng chỉ tính hoá đơn mua**, không tính hoá đơn trả.

### 7.2 Thẻ khách hàng

Nguồn: [Thẻ khách hàng](https://pos.manual.nhanh.vn/pos/khach-hang/the-khach-hang.md)
- 2 thông tin chính: **mã thẻ + thông tin khách hàng**; 1 thẻ ↔ 1 khách.
- **3 trạng thái thẻ (enum) + ma trận chuyển trạng thái (rất đáng đặc tả):**
  | Trạng thái | Được chuyển sang |
  |---|---|
  | **Mới** (chưa có thông tin KH) | chỉ → **Khóa** (thẻ hỏng, chưa đưa khách) |
  | **Đã kích hoạt** | chỉ → **Khóa** (thẻ hỏng/đổi mã); **gỡ khách khỏi thẻ** |
  | **Khóa** | chỉ → **Mới** (mở lại để in lại / gắn khách khác) |
- **Phát hành thẻ — 3 cách:** hệ thống tạo ngẫu nhiên (**tối đa 20.000 thẻ/đợt**, mã **13 số** không trùng) · tự nhập mã (**tối đa 10.000 mã**, cho phép chữ + ký tự, mỗi mã 1 dòng) · **import Excel**.
- **In thẻ**: xuất Excel danh sách mã → gửi đơn vị in. Khuyến nghị thẻ có **mã vạch**, thông tin DN, **cấp độ**. 2 loại thẻ phổ biến: **thẻ giấy tích điểm** và **thẻ cứng** (full / mini móc khoá).
- **Gắn thẻ cho khách — 2 tình huống:** (a) gắn khách vào thẻ trước rồi gửi thẻ — 3 cách (Thêm khách hàng trên thẻ / sửa KH thêm mã thẻ / import); (b) gửi thẻ trước — khi bán hàng **quét mã thẻ vào ô “Mã thẻ khách hàng”** trên hoá đơn/đơn.
- **Đổi thẻ**: 2 cách (sửa KH → quét mã thẻ mới; hoặc trên danh sách thẻ → **Đổi code**). **Mã thẻ cũ bị khóa** và mất thông tin KH; mã mới phải **không thuộc danh sách đã phát hành**.

### 7.3 Cấp độ khách hàng

Nguồn: [Cấp độ khách hàng](https://pos.manual.nhanh.vn/pos/khach-hang/cap-do-khach-hang.md)
- **3 kiểu Rule (enum):** *Mức trung bình theo tháng* · *Tổng tiền tích luỹ* · *Tổng tiền mỗi tháng*.
- Trường: Tên cấp độ · Kiểu Rule · **Giá trị nhỏ nhất** · **Giá trị lớn nhất** · **Hạn mức điểm tối thiểu**.
- **Ràng buộc:** giá trị min **không được trùng** min/max của cấp khác; **`min cấp sau = max cấp trước + 1`**; cấp không khai max = **cấp cao nhất**; phải chọn trạng thái **Đang hoạt động**.
- **Hạn mức điểm tối thiểu** = số điểm tối thiểu phải duy trì; KH chỉ tiêu được `điểm hiện có − hạn mức` (VD: Vàng giữ 100 điểm, có 180 → tiêu tối đa 80).
- **Tự động nâng/hạ cấp** ngay sau mỗi lần mua/trả (ví dụ minh hoạ đầy đủ trong tài liệu). Có nút **“Cập nhật lại cấp độ khách hàng”** để chạy lại ngay.

### 7.4 Nhóm khách hàng

Nguồn: [Nhóm khách hàng](https://pos.manual.nhanh.vn/pos/khach-hang/nhom-khach-hang.md)
- Không có điều kiện ràng buộc; **do người dùng tự phân**.
- **4 cách thêm KH vào nhóm:** khi tạo hoá đơn · sửa thông tin KH · **Thao tác > Cập nhật nhóm khách hàng** (hàng loạt) · **import Excel**.
- Quy tắc: **không xoá được nhóm còn khách** (phải gỡ từng khách); báo cáo **Nhóm khách hàng** không hiển thị nhóm rỗng.

### 7.5 Chăm sóc khách hàng

Nguồn: [Chăm sóc khách hàng](https://pos.manual.nhanh.vn/pos/khach-hang/cham-soc-khach-hang/cskh.md)
- Mục đích: lưu **lịch sử chăm sóc**, tránh NV chăm sóc trùng khách.
- **Điều kiện tiên quyết:** phải tạo **Hình thức chăm sóc** và **Lý do chăm sóc** trước; 1 hình thức có nhiều lý do; 1 hành động có nhiều hình thức.
- **5 loại hành động:** Tặng điểm · Trừ điểm · **Tặng tiền** · **Trừ tiền tích luỹ** · Gọi điện/Nhận cuộc gọi · Gửi email/nhắn tin.
- **4 cách thao tác:** tại module CSKH · tại danh sách KH (Thao tác) · **Import hành động chăm sóc** · **Import cập nhật điểm tích luỹ** (có 2 toggle **“Xóa toàn bộ tiền/điểm tích lũy cũ về 0”** — nếu không tích thì hệ thống **cộng dồn**: *giá trị sau import = giá trị trước + giá trị trong file*).
- Lưu ý: Gọi điện/Nhắn tin/Gửi email **chỉ ghi log**, không thực sự gửi (gửi thật dùng module Gửi SMS/Email).

---

## 8. Module Kế toán

### 8.1 Khái niệm & nguyên lý

Nguồn: [Kế toán](https://pos.manual.nhanh.vn/pos/ke-toan/ke-toan.md)
- **3 khái niệm:** Công nợ · Bút toán · Tài khoản kế toán.
- Bật module tại `Cài đặt > Cài đặt chung > Kế toán`: **Sử dụng hạch toán kế toán tự động** + **Cài đặt ngày bắt đầu hạch toán đơn hàng tự động**.
- Nguyên lý được dạy lại trong tài liệu (VN): `TỔNG TÀI SẢN = TỔNG NGUỒN VỐN`; `Tổng số dư NỢ = Tổng số dư CÓ`.
- **TK tài sản (đầu 1,2,6,8)**: dư Nợ, tăng ghi Nợ, giảm ghi Có. **TK nguồn vốn (đầu 3,4,5,7)**: dư Có, tăng ghi Có, giảm ghi Nợ.
- **Bảng TK lưỡng tính:** 131, 138, 331, 333, 334, 338.

### 8.2 Hạch toán tự động (bảng định khoản — tài liệu cho ví dụ số cụ thể)

Nguồn: [Hạch toán kế toán tự động](https://pos.manual.nhanh.vn/pos/ke-toan/hach-toan-tu-dong.md)

| Nghiệp vụ | Định khoản tự sinh |
|---|---|
| **Nhập hàng NCC** (10 SP × 100.000, VAT 10%, CK 20%, TM 400.000 + CK 480.000) | Nợ 1561/Có 331 (800.000) · Nợ 1331/Có 331 (80.000) · Nợ 331/Có 1111 (400.000) · Nợ 331/Có 1112 (480.000) — đối tượng **Nhà cung cấp**, chứng từ = **Phiếu XNK** |
| **Xuất trả NCC** | Nợ 331/Có 1561 · Nợ 331/Có 1331 · Nợ 1111/Có 331 · Nợ 1121/Có 331 |
| **Hoá đơn bán lẻ/bán sỉ** (150.000, VAT 10%, CK 10.000, TM 100.000 + CK 54.000, giá vốn 98.692) | Nợ 131/Có 5111 (140.000) · Nợ 131/Có 33311 (14.000) · **Nợ 632/Có 1561** (98.692) · Nợ 1111/Có 131 (100.000) · Nợ 1121/Có 131 (54.000) |
| **Trả hàng / trả không cần HĐ** | Nợ 1561/Có 632 · Nợ 33311/Có 131 · **Nợ 5213/Có 131** (140.000) · Nợ 131/Có 1111 (154.000) |
| **Khởi tạo đơn hàng** (không thanh toán trước) | **Không tạo hạch toán** |
| **Đơn hàng có đặt cọc/chuyển khoản** | Nợ 1111/Có 131 (đặt cọc) · Nợ 1121/Có 131 (chuyển khoản) — gắn **ID đơn hàng** |
| **Đơn Thành công** | Nợ 632/Có 1561 (giá vốn) · Nợ 131/Có 5111 (doanh thu **trừ chiết khấu, chưa gồm phí ship báo khách**) |
| **Đơn đối soát qua Nhanh** | Nợ 131/Có 711 (phí thu của khách) → công nợ KH = giá trị đơn + phí · Nợ 1121/Có 131 (xác nhận nhận tiền) · Nợ 811/Có 1121 (trả phí VC + vượt cân, đối tượng **NCC Nhanh.vn**) |
| **Đơn sàn (Lazada/Shopee/Sendo)** | Nợ 632/Có 1561; Nợ 131/Có 5111; đối soát → Nợ 1121/Có 131 = cột *Thanh toán* trong file đối soát, đối tượng **sàn TMĐT** |
| **Nhập máy cũ (IMEI)** | Nợ 1561/Có 131 (đối tượng **khách hàng**) · Nợ 131/Có 1111 · Nợ 131/Có 1121 |
| **Nhập/Xuất khác** | Nhập: Nợ 1561/Có 131 + Nợ 131/Có 1111 · Xuất: Nợ 632/Có 1561 + Nợ 131/Có 5111 + Nợ 1111/Có 131 |
| **Trả góp** | Nợ 632/Có 1561 · Nợ 131/Có 5111 (giá trị SP − trả góp) · Nợ 131/Có 5111 (**số tiền trả góp, đối tượng là dịch vụ trả góp**) |
| **Bán giữa 2 doanh nghiệp** | DN A: Nợ 632/Có 1561 + Nợ 131/Có 5111 (**đối tượng khách hàng tên “Đại lý” do hệ thống tự tạo**) + Nợ 1111/1121/Có 131; DN B: sinh **phiếu yêu cầu Nhập NCC** |
| **Đổi IMEI sang SP khác** | **Không hạch toán tự động** |

**Quy tắc:** giao dịch tự sinh **không sửa/xoá được** — tự cập nhật khi sửa/xoá chứng từ gốc. **Chưa hạch toán**: xuất/nhập **combo**, **bù trừ kiểm kho**, **chuyển kho**. Cảnh báo nghiệp vụ: nếu **không xác nhận tiền COD** thì kế toán báo KH còn nợ; nếu tạo phiếu thu thủ công rồi vẫn xác nhận sẽ **bị x2 (DN nợ ngược lại khách)**.

### 8.3 Công nợ

Nguồn: [Công nợ](https://pos.manual.nhanh.vn/pos/ke-toan/cong-no.md)
- **5 loại công nợ:** Khách hàng · Nhà cung cấp · **Nhân viên bán hàng** · **Sàn TMĐT** · **Dịch vụ trả góp/trả góp theo ngày**.
- **Công nợ KH** (TK 131) — 6 bộ lọc con: Công nợ KH · **Hạn thanh toán** (mặc định **7 ngày** nếu không hẹn) · **Nợ quá hạn** · **Hạn hôm nay** · **Hạn 7 ngày tới** · **Hạn trên 7 ngày**.
- Cột: Số dư đầu kỳ · Phát sinh trong kỳ (**Nợ** = mua hàng, **Có** = thanh toán) · Số dư cuối kỳ (**Nợ [Phải thu]** / **Có [Phải trả]**).
  Công thức: `Phải thu = Nợ trong kì − Có trong kì`; `Phải trả = Có trong kì − Nợ trong kì`.
- **Công nợ NCC** (TK 331): ngược lại — **nhập kho ghi Có**, **thanh toán ghi Nợ**.
- **3 cách thu công nợ KH:** (1) tổng công nợ → *Thêm phiếu thu chi* (tiền mặt) / *Lập phiếu báo có* (CK, quẹt thẻ); (2) theo từng hoá đơn ở tab *Hạn thanh toán* → *Thanh toán công nợ bằng tiền mặt/chuyển khoản*; (3) **sửa hoá đơn, điền tiền thanh toán**.
- **Công nợ NVBH**: cột *Đã thu (bán lẻ, bán buôn)* · *Đã thu (đơn hàng)* · *Còn nợ*; số liệu màu xanh **click được** → ra danh sách bút toán. *Lưu ý: không dùng để theo dõi công nợ nội bộ nhân viên; đơn sàn cũng ghi nhận nếu có gắn NVBH; cột Còn nợ của đơn hàng **không gồm phí ship***.
- **Công nợ theo đầu tài khoản** (`/accounting/debts/byaccount`) — lọc Phải thu / Phải trả / cả hai; cột Dư nợ đầu kỳ | Dư có đầu kỳ · Nợ | Có · Dư cuối kỳ. Bút toán liên quan TK nhưng **không gắn đối tượng sẽ không hiện**.
- FAQ quan trọng: **không xoá được công nợ** (phải xoá chứng từ gốc hoặc thêm phiếu thu chi bù); công nợ KH **tính chung toàn hệ thống**, không tách theo cửa hàng.

### 8.4 Bút toán

Nguồn: [Bút toán](https://pos.manual.nhanh.vn/pos/ke-toan/but-toan.md)
- **5 loại phiếu (enum):** *Báo nợ (rút tiền)/báo có (nộp tiền)* · *Phiếu thu/chi* · *Phiếu nhập* · *Phiếu bán hàng* · *Phiếu trả hàng*.
- Thêm: **Thêm giao dịch** (TK Nợ/TK Có, Loại chứng từ = Phiếu XNK/Đơn hàng) hoặc **Import giao dịch**.
- **Chuyển quỹ**: di chuyển tiền giữa các tài khoản (VD tiền mặt → ngân hàng).
- **Import số dư đầu kỳ tài khoản**: file `Nhanh.vn_Accounting_Account_ImportAmount.xls`; mỗi TK **chỉ điền 1 trong 2 cột Nợ/Có**; **phải chọn “Ngày ghi nhận số dư” trong quá khứ** (nếu để ngày hiện tại sẽ bị tính vào phát sinh trong kỳ).
- **Phát sinh đối ứng**: nhập TK (VD `TK1111.4`) → xem các TK đối ứng.
- **Thu hộ trả góp** · **Lịch sử** (log sửa/xoá bút toán).
- **TK đối ứng mặc định** khi chọn đối tượng: Khách hàng → **131**, NCC → **331**, Sàn TMĐT → **131**, Dịch vụ trả góp → **131**. Với đối tượng **Khác/Nhân viên phải tự điền TK đối ứng**.

### 8.5 Thu chi tiền mặt / ngân hàng

Nguồn: [Thu chi tiền mặt](https://pos.manual.nhanh.vn/pos/ke-toan/thu-chi-tien-mat.md)
- Bộ lọc: **ID chứng từ** (ID đơn hàng hoặc phiếu XNK) · **Loại đối tượng** (Khách hàng, NCC, Nhân viên, Dịch vụ trả góp, Sàn TMĐT, **Khác**) · Đối tượng.
- Form: **Tài khoản** · **Loại phiếu** (thu/chi) · **Loại đối tượng** · **Đối tượng** · **Loại chứng từ + ID chứng từ** (nếu muốn thu/chi theo từng hoá đơn).
- Hiệu ứng: phiếu thu ⇒ **giảm công nợ KH**; phiếu chi ⇒ **giảm công nợ NCC/dịch vụ trả góp**.
- **Import Excel** nhiều giao dịch.
- **Quy tắc:** TK kế toán gắn kho thì chỉ hiện TK có kho đó; **TK tiền mặt phải có số dư bên Nợ mới làm được phiếu chi**; nếu chọn Loại đối tượng = **Khác** thì phiếu **không có TK đối ứng** → **bảng cân đối kế toán sẽ sai lệch** (muốn đủ đối ứng phải vào Bút toán).

### 8.6 Các trang kế toán khác (trong mục lục, chưa khảo sát chi tiết)
Khởi tạo tài khoản kế toán · Thu chi ngân hàng · Dịch vụ trả góp · Kết chuyển cuối kỳ · Kết chuyển cuối kỳ năm tài chính · Lập bảng cân đối kế toán · **Bảng cân đối kế toán mẫu B01-DN** · Hạch toán tiền lương/BHXH/BHYT/BHTN · Hạch toán đơn hàng · Nhật ký kế toán chung.

---

## 9. Module Báo cáo

Nguồn: [Báo cáo](https://pos.manual.nhanh.vn/pos/bao-cao/bao-cao.md) · [Tổng quan](https://pos.manual.nhanh.vn/pos/bao-cao/tong-quan.md)

**Quy mô:** **11 module báo cáo, ~80 báo cáo chi tiết**, số liệu **realtime** tại thời điểm truy cập.

**Danh sách báo cáo theo module (nguyên văn):**
| Module | Báo cáo |
|---|---|
| **Doanh thu** (10) | Theo thời gian · Theo cửa hàng · Theo thương hiệu · Theo nhân viên · Theo danh mục SP · Theo **danh mục nội bộ** · Theo sản phẩm · Theo nhà cung cấp · Theo khách hàng · **Tỷ suất doanh thu/tồn kho** |
| **Đơn hàng** (13) | Theo kênh bán · Tạo theo thời gian · Thành công · Theo giá trị đơn · Theo danh mục SP · Theo sản phẩm · Theo trạng thái · **Theo địa chỉ** · Theo thời gian xử lý · **Theo lý do khách hủy** · Theo NV xử lý · **Theo quảng cáo** · **Theo tiền đối soát** · Theo hãng vận chuyển |
| **Bán lẻ** (9) | Tổng quan · Theo nguồn KH · Theo nhân viên · Theo cửa hàng · **Theo chi tiết quẹt thẻ** · Theo giá trị hoá đơn · **Theo BHMR** · **BHMR theo nhân viên** · **Tỷ lệ hoá đơn/khách vào cửa hàng** · **Báo cáo kết ca** |
| **Bán sỉ** (1) | Tổng quan |
| **Tồn kho** (10) | Xuất nhập tồn theo SP · Chi tiết SP theo kiểu XNK · **Tổng XNK** · Tổng XNK theo cửa hàng · Tồn theo NCC · Tồn theo danh mục · Số lượng hàng tồn · **Chuyển kho chưa xác nhận** · **Tồn theo IMEI** · Tồn theo trạng thái từng cửa hàng / từng SP |
| **Sản phẩm** (7) | Bán chạy nhất · Bán chạy theo cửa hàng · **Tốc độ bán hàng** · SL bán ra theo danh mục & cửa hàng · SP theo khoảng giá · SP theo ngày · **Bán hàng theo IMEI** · **SP theo thuộc tính** |
| **Khách hàng** (6) | Theo sản phẩm · **Cấp độ KH** · **Nhóm KH** · Tỷ lệ KH · KH tạo mới theo CH · **Sinh nhật KH** · **Chu kì mua hàng** |
| **Khuyến mại** (6) | Tỷ lệ với doanh thu · **Quà tặng theo SP** · Theo SP · Quà tặng theo CH · **Affiliate** · **Doanh thu theo bảng giá** |
| **Kế toán** (6) | Tổng hợp theo tài khoản · Tổng hợp thu chi theo CH · **Tổng hợp tiền bán lẻ hằng ngày theo CH** · Tổng hợp thu chi theo ngày · **Tổng hợp KQHĐKD** · **Bảng cân đối kế toán** |
| **Bảo hành** (6) | Doanh thu theo ngày/CH/NV · Doanh thu thay thế linh kiện · Trạng thái theo CH/NV |
| **Zalo** (2) | Số lượng tin gửi · Đánh giá của khách hàng |

**Trang Tổng quan (dashboard)** — `/admin`: bộ lọc **Thời gian** (hôm nay / hôm qua / tuần này / tháng này / tháng trước) + **Cửa hàng** (1 hoặc tất cả).
- Khối **Tổng quát**: kênh bán · doanh thu · số đơn · **GTTB** · **SL sản phẩm trung bình** · **Ads** · lợi nhuận.
- Công thức: `GTTB = Doanh thu / Số đơn`; `SL SP TB = Tổng SL SP trong đơn / Số đơn`.
- Khối chi tiết: **Doanh thu theo thời gian** (biểu đồ cột) · **Đơn hàng** (theo trạng thái) · **Số dư ví** · **Sản phẩm bán chạy** (sort doanh thu giảm dần) · **Tồn kho** (giá trị theo SL – giá vốn).
- **Phân quyền dashboard:** **chỉ tài khoản Giám đốc xem được số liệu**; cửa hàng trưởng và thu ngân/NVBH có giao diện khác.

**Các “bẫy số liệu” tài liệu giải thích (rất giá trị khi đặc tả tooltip/help):**
- Giá vốn báo cáo doanh thu ≠ giá vốn báo cáo XNT theo sản phẩm (tổng vs từng SP).
- Cùng 1 báo cáo KQHĐKD, **lọc kho vs không lọc kho** ra số khác nhau.
- Doanh thu theo danh mục cha ≠ tổng danh mục con (SP nằm trực tiếp ở danh mục cha).
- Giá vốn trong báo cáo = giá vốn **tại thời điểm đơn Thành công**; trong file xuất = giá vốn **tại thời điểm xuất**.
- **“Thực trả”** = COD shop nhận sau khi trừ phí, **không gồm tiền chuyển khoản**; có tính cả Shopee nếu lọc kênh bán.
- Doanh thu đơn thành công tháng ≠ tiền đối soát về (đơn 31/12 có thể thanh toán tháng sau).
- Giá trị tồn kho trên dashboard = `giá vốn SP × số tồn`, bằng báo cáo **Tổng XNK**.

---

## 10. Module Khuyến mại

### 10.1 Chiết khấu

Nguồn: [Chiết khấu – Nội dung](https://pos.manual.nhanh.vn/pos/khuyen-mai/chiet-khau/noi-dung.md)

**5 tab (trạng thái chương trình – enum):** Tất cả · **Đang khuyến mại** · **Sắp khuyến mại** · **Chưa kích hoạt** · **Ngừng khuyến mại**.

**Cột danh sách:** **Ưu tiên** (1–5, **1 = cao nhất**; bỏ trống tự lấy = 1) · **Áp dụng** (kênh bán: bán lẻ / bán sỉ / đặt hàng online) · **Thời hạn** (ngày còn hạn hiện **xanh**, quá hạn hiện **đỏ**) · **Trạng thái hoạt động** (click icon để bật/tắt) · menu sửa / **lấy link gắn website**.

**Trường chung khi tạo:** Ngày áp dụng (bắt buộc ngày bắt đầu; **bỏ trống ngày kết thúc = không kết thúc**) · Cửa hàng (bỏ trống = **toàn bộ**) · Độ ưu tiên 1–5 · Trạng thái Hoạt động/Không hoạt động · Kênh bán (Bán lẻ / Bán sỉ / Đặt hàng online / **Tất cả**) · **Website: ảnh đại diện + ảnh banner**.

**6 loại khuyến mại cơ bản:**
| Loại | Điều kiện/đặc điểm |
|---|---|
| **Theo giá trị hoá đơn** | 2 biến thể: chiết khấu **Toàn bộ hoá đơn** (theo tổng) hoặc **Sản phẩm trong hoá đơn** (theo từng SP). Dùng được để làm **chiết khấu theo Nhóm/Cấp độ KH** |
| **Theo danh mục sản phẩm** | Nhập CK cho từng danh mục; **CK danh mục cha KHÔNG áp cho danh mục con**; luôn là CK theo từng SP trong hoá đơn |
| **Giảm giá theo số lượng** | Theo khoảng SL của 1 SP; có **Import Excel**; tùy chọn **Áp dụng SP cha–con** (cha cho tất cả con / theo từng con) |
| **Giá theo số lượng sản phẩm** | Đổi **đơn giá** theo khoảng SL (thay vì giảm tiền) |
| **Theo từng sản phẩm** | Danh sách SP tự chọn, CK tiền mặt hoặc % |
| **Đồng giá** | Bán đồng giá danh sách SP; **mỗi SP có thể 1 mức giá riêng**; có Import Excel |
| **Khuyến mại nâng cao** | Kết hợp nhiều điều kiện (xem dưới) |

**Ràng buộc số học (rất cụ thể):** giá trị chiết khấu phải là **số nguyên** (chưa hỗ trợ thập phân) ở CTKM cơ bản; với giá trị trùng nhau hệ thống **ưu tiên dòng dưới** ⇒ nên tách khoảng rời nhau (VD 0–199.999, 200.000–399.999, ≥ 400.000).

**CTKM nâng cao — 4 nhóm điều kiện:**
- **Đơn hàng**: giá trị tối thiểu / tối đa, SL SP tối thiểu, SL SP.
- **Sản phẩm áp dụng**: tất cả, hoặc theo **thương hiệu / danh mục / nhà cung cấp / danh sách tự chọn**.
- **Thời gian mua hàng**: theo **ngày** hoặc theo **giờ** (giờ vàng/ngày vàng).
- **Khách hàng**: sinh nhật (**ngày / tuần sinh / tháng sinh**), tiền tích luỹ min–max, **số lần mua** + số lần mua tối thiểu, **cấp độ**, **nhóm**.
- **Nội dung khuyến mại**: **Chiết khấu** (theo đơn hàng / theo SP; 3 kiểu: **tiền mặt, %, fix giá**) + **Tích điểm** (theo đơn/SP, theo tiền mặt/%; `điểm = % × giá trị HĐ / tỷ lệ quy đổi`, tỷ lệ mặc định **1000đ = 1 điểm**).
- Ưu tiên cấu hình: nếu cài Tích điểm ở **cả** CTKM nâng cao **và** `Khuyến mại > Tích điểm` thì **ưu tiên mục Tích điểm**.
- **Không sửa/xoá được CTKM đã tạo** (FAQ).
- Phân quyền: quyền **sửa chiết khấu** chỉ áp cho nhóm **nhân viên**; tài khoản **Giám đốc mặc định luôn được sửa**.

### 10.2 Mã giảm giá (Coupon/Voucher)

Nguồn: [Mã giảm giá](https://pos.manual.nhanh.vn/pos/khuyen-mai/coupon.md)
- Coupon = giảm theo **%**; Voucher = giảm theo **giá trị**; Nhanh **gộp chung** và quản lý thêm **số lần sử dụng**.
- 2 kiểu dùng: **1 mã dùng nhiều lần** hoặc **nhiều mã, mỗi mã 1 lần**.
- **3 cách tạo:** tự sinh ngẫu nhiên (có **Tiền tố + Hậu tố + độ dài**, VD `ABxxxxxxCD`) · tự nhập mã · **import Excel**.
- **Phân bổ giá trị coupon về từng SP** (logic rất chi tiết, có ví dụ số):
  - Coupon **có gắn danh mục**: tỉ lệ phân bổ = `giá trị coupon / giá trị đơn sau chiết khấu A` → giá SP cuối = `giá sau CK A − giá sau CK A × tỉ lệ`.
  - Coupon **không gắn danh mục**: phân bổ theo **% giá gốc SP trên tổng đơn gốc** → `giá SP cuối = giá sau CK A − giá trị coupon × (giá gốc SP / tổng đơn gốc)`.
- Dùng khi lập **hoá đơn bán lẻ** (ô dưới cột Chiết khấu) hoặc **đơn hàng** (ô ở phần Thanh toán).
- **Quy tắc:** số lượng mã trong 1 chương trình **không đổi được** (muốn thêm mã phải tạo chương trình mới); mã hết lượt → trạng thái **Đã sử dụng**, muốn dùng tiếp phải tăng số lần + đổi trạng thái về **Đã tặng**; **hoá đơn đã có chiết khấu tay thì KHÔNG áp được coupon**; có cài đặt **“Áp dụng chiết khấu theo coupon trước khi áp dụng chiết khấu theo chương trình khuyến mại”**.
- **Tặng coupon cho lần mua sau**: tạo CT coupon cho đơn ≥ X, rồi cài **tặng coupon khi in** ở danh sách; trong **mẫu in** thêm từ khoá **“Mã Coupon sử dụng lần sau”**.

### 10.3 Tích điểm

Nguồn: [Tích điểm](https://pos.manual.nhanh.vn/pos/khuyen-mai/tich-diem.md)
- Quy trình: **Cài đặt tích điểm → Tạo chương trình → Tiêu điểm**.
- **Tỉ lệ tích điểm** (VD `10.000đ = 1 điểm`, tích 30% ⇒ hoá đơn 600.000 → `(600.000×30%)/10.000 = 18 điểm`) và **tỉ lệ tiêu điểm** (VD `1 điểm = 5.000đ` ⇒ tiêu 10 điểm = giảm 50.000đ).
- **3 hình thức:** theo **giá trị hoá đơn** (kiểu **cố định** hoặc **% giá trị**) · theo **danh mục** (CK danh mục cha **áp luôn** cho danh mục con; có **loại trừ sản phẩm**) · theo **từng sản phẩm**.
- **Bắt buộc điền cả giá trị hoá đơn tối thiểu và tối đa**.
- **Tiêu điểm**: ô “Tiêu điểm tích luỹ” **chỉ hiện sau khi điền Tên + SĐT khách**.
- **Giới hạn:** chỉ áp cho hoạt động bán hàng trên Nhanh, **chưa áp cho đơn sàn kéo về**; có thể chọn chỉ áp cho **Bán lẻ**.

### 10.4 Thiết lập bảng giá

Nguồn: [Thiết lập bảng giá](https://pos.manual.nhanh.vn/pos/khuyen-mai/thiet-lap-bang-gia.md)
- Mục đích: 1 SP có **nhiều mức giá** theo **chi nhánh / nhóm KH / thời điểm**; hỗ trợ **áp dụng chiết khấu 2 tầng**; kiểm soát việc NV tự nhập giá.
- Trường: **Kênh bán** (bắt buộc) · **Nhóm/cấp độ khách hàng** · **NV tạo hoá đơn** (NV được xem bảng giá). Bỏ trống = áp **tất cả**.
- **Thiết lập giá tự động**: công thức tăng/giảm từ **giá bán / giá sỉ / giá nhập / giá vốn** theo **VND hoặc %**; toggle **“Tự động cập nhật bảng giá khi giá sản phẩm thay đổi”** (nếu không tích thì giá **cố định**).
- **3 mức cảnh báo khi bán hàng (enum):** *Cho phép chọn hàng hoá khác bảng giá* · *Cho phép + cảnh báo* · *Không cho phép*.
- **3 cách thêm SP vào bảng giá / đặt giá:** thêm trực tiếp · theo nhóm/danh sách tự chọn/**các SP chưa có trong bảng giá nào** · **import Excel**; có nút **“Cập nhật lại giá sản phẩm”** sau khi đổi công thức.
- Xem **tối đa 5 bảng giá song song** để so sánh; **xuất Excel**.
- **Không áp dụng** cho: **sản phẩm cha, sản phẩm IMEI, sản phẩm theo lô, sản phẩm nhiều ĐVT**.
- **Logic kết hợp bảng giá + CTKM:** chiết khấu tính **trên giá sau bảng giá** (VD 185.000 − 10% = 166.500); với **CTKM đồng giá** thì hệ thống **chỉ lấy giá bảng giá** → muốn dùng đồng giá thì **không chọn bảng giá** khi lên đơn.
- Báo cáo: `Báo cáo > Khuyến mại > Doanh thu theo bảng giá`.

### 10.5 Chương trình Affiliate

Nguồn: [Chương trình Affiliate](https://pos.manual.nhanh.vn/pos/khuyen-mai/chuong-trinh-affiliate.md)
- **Chỉ hỗ trợ khi dùng website của Nhanh.vn.**
- Cơ chế: khách tạo **mã giới thiệu** → link `domain?affcode=<mã>` (có thể gắn vào **bất kỳ** đường link nào của domain).
- Cần tạo **menu Website** trỏ `/affiliate`.
- **3 loại CT chiết khấu cho người được giới thiệu:** theo **giá trị hoá đơn** · theo **từng sản phẩm** · theo **nhóm mã**.
- **Hoa hồng người giới thiệu**: cấu hình riêng từng mã (nhóm Affiliate, **thông tin tài khoản ngân hàng** của CTV, mức hoa hồng %/tiền mặt, **duyệt/bỏ duyệt**). **Mã phải ở trạng thái “Đã Duyệt” mới có hiệu lực.**
- Theo dõi: bộ lọc **“Mã affiliate”** trong danh sách đơn; báo cáo `Báo cáo > Khuyến mại > Affiliate`; người giới thiệu tự xem ở `domain/affiliate?tab=report`.

### 10.6 Các trang khuyến mại khác (chưa khảo sát chi tiết)
Chiết khấu V2 (Thêm mới CTKM, Cài đặt CK, CTKM cơ bản, CTKM nâng cao) · Quà tặng · Quà tặng V2 · **Hoa hồng nhân viên** · Một số CTKM hay gặp.

---

## 11. Module Cài đặt

### 11.1 Nhân viên & phân quyền

Nguồn: [Nhân viên](https://pos.manual.nhanh.vn/pos/cai-dat/nhan-vien.md)

**13 nhóm quyền (enum):** Giám đốc · Nhân viên kế toán · Nhân viên cửa hàng trưởng (phải gắn kho/cửa hàng) · Nhân viên thu ngân · Nhân viên bán hàng · Chăm sóc khách hàng · Trưởng nhóm CSKH · Nhân viên kho · Nhân viên vận chuyển · Nhân viên nhập liệu · Nhân viên thu mua · **Nhà cung cấp** · **Nhân viên kiểm soát viên/đối soát viên**.

**3 tầng phân quyền:**
1. **Phân quyền nhóm** — sửa quyền trong nhóm, **áp ngay cho mọi tài khoản thuộc nhóm**.
2. **Phân quyền riêng từng nhân viên** — có nút mũi tên để **load lại quyền mặc định của nhóm** trước khi thêm/bớt; có nút **“Khởi tạo phân quyền mặc định”** để trả về nhóm; **tài khoản đã có phân quyền riêng thì không phân quyền chung được nữa**.
3. **Phân quyền xem dữ liệu** — giới hạn theo **người tạo** + **số ngày xem**, áp cho: **Danh sách hoá đơn bán lẻ** · **Danh sách đơn hàng** (kèm tuỳ chọn *cho xem Chi tiết đơn của người khác*) · **Chi tiết sản phẩm** (chỉ xem tồn kho mình quản lý).

**Quy tắc đa doanh nghiệp (quan trọng, dễ bỏ sót):** nếu tài khoản **có phân quyền riêng ở BẤT KỲ doanh nghiệp nào** → hệ thống **chỉ áp phân quyền riêng, bỏ qua phân quyền nhóm** ở **tất cả** doanh nghiệp; nếu không có phân quyền riêng ở đâu → áp phân quyền nhóm theo từng DN; quyền cuối cùng là **tổng hợp** theo các DN tài khoản tham gia.

**Công cụ hỗ trợ tìm quyền:** tìm theo **tên quyền** (`Ctrl/Command + F`) hoặc **dán URL chức năng** vào thanh tìm kiếm → quyền tương ứng **tô đỏ**.

**Bảng “quyền bao hàm quyền khác” (rất hữu ích để đặc tả permission tree):**
| Quyền | Bao gồm |
|---|---|
| Thêm sản phẩm | upload ảnh sản phẩm |
| Xác nhận đặt hàng và Đóng gói | nhặt hàng, đóng gói, **in phiếu gửi** |
| Xác nhận đơn hàng | **sửa đơn hàng** |
| Trả hàng từ hoá đơn online | cần thêm quyền **Thêm đơn hàng** |
| Thêm phiếu nhập NCC / Thêm HĐ bán lẻ / bán sỉ | tương ứng quyền **Xác nhận phiếu nháp** |
| Thêm phiếu nhập NCC | bao gồm **Nhập từ excel** XNK & phiếu nháp |
| Duyệt phiếu chuyển kho | **Đổi kho nhận hàng** trong danh sách phiếu nháp |
| Xoá đơn khỏi Tour | **Xoá đơn khỏi biên bản bàn giao** |
| Trả hàng từ đơn online (sàn) | cần thêm **Quản lý giao hàng 1 phần** |
| Xuất dữ liệu DS SP combo | cần thêm **Xem giá nhập sản phẩm** |
| Nhập máy cũ | bao gồm **DS hoá đơn bán lẻ** + **Hoá đơn điện tử** |

**Bảo mật:** tab **Log Profile** (username, loại đăng nhập/xuất/đổi MK/SĐT/Google/Facebook, ngày tạo, **IP**, **userAgent**); khoá tài khoản khi NV nghỉ việc.
**FAQ đặc tả hành vi:** NV CSKH A đổi trạng thái đơn → **NV CSKH B cùng nhóm không sửa/đổi trạng thái được** (chống chăm sóc trùng); quyền NV bán hàng/thu mua **mặc định lọc theo người tạo**.

### 11.2 Cài đặt chung

Nguồn: [Cài đặt chung](https://pos.manual.nhanh.vn/pos/cai-dat/cai-dat-chung.md)

**Sản phẩm (5 toggle):** Tự sinh mã vạch (**đầu mã 20–29**, chủ yếu cho **bán chéo** để không trùng mã vạch giữa DN) · Hiển thị **tồn có thể bán** khi gợi ý SP · Hiển thị ảnh SP từ sàn nếu SP chưa có ảnh · **Bán SP lô lấy theo ngày hết hạn gần nhất** · **Tìm kiếm SP** (tuần tự hoặc gần đúng, độ chính xác 30%…90%).

**Kế toán (7 toggle, có logic chi tiết):** Hạch toán tự động · **Tự động tạo phiếu thu nếu thu ngân không điền tiền thanh toán** · Ngày bắt đầu hạch toán đơn tự động · **Số ngày cho phép sửa bút toán** · **Số ngày cho phép xoá bút toán** · **Số ngày quá khứ NV được chọn khi lập phiếu thu chi** · Phí bảo hành sửa chữa mặc định.
- Logic “số ngày quá khứ”: nếu **không** cấu hình → ô ngày giao dịch **bị mờ**, lưu giữ nguyên ngày; nếu **có** cấu hình → hiện ô sửa ngày, chặn nếu vượt quá `ngày thao tác − số ngày`.

**Hạn mức công nợ:** đặt **hạn mức chung** cho mọi KH và/hoặc **hạn mức riêng từng KH**; **hạn mức riêng được ưu tiên hơn hạn mức chung**; hệ thống **chặn** khi lập HĐ bán lẻ/bán sỉ/đơn hàng vượt hạn mức (có **pop-up cảnh báo**).

**Khác:** Bảo hành (bắt buộc nhập NV tiếp nhận) · **Xác thực SĐT theo quốc gia** (bỏ kiểm tra độ dài 10 số) · **Open API** (bật kết nối + chọn nhóm quyền được cấp) · **Giới hạn đăng nhập theo IP** (chỉ IP tĩnh; **tài khoản Giám đốc không bị chặn**) · **2FA** (chọn nhóm quyền bắt buộc; NV đã đăng nhập mà DN mới bật thì **sau 15 phút** bị yêu cầu; dùng **Google Authenticator**, mã 6 số; có thể **tắt 2FA** cho từng NV) · Thông tin doanh nghiệp.

### 11.3 Mẫu in

Nguồn: [Mẫu in](https://pos.manual.nhanh.vn/pos/cai-dat/mau-in.md)
- **3 tab:** **Thiết kế mẫu in** · **Lịch sử mẫu in** (xem log sửa, **khôi phục “Lấy lại mẫu in”**) · **Mẫu in** (danh sách).
- Thiết kế 3 bước: chọn **cửa hàng + mẫu in + khổ giấy + số bản in** → thiết kế nội dung bằng **chèn Từ khoá** → xem trước → **Lưu cài đặt**. Có nút **“Quay về dùng mẫu in mặc định”**.
- Từ khoá nhóm theo: **doanh nghiệp · hoá đơn · sản phẩm · khách mua hàng**. Có thể định dạng (căn lề, font, cỡ, màu, nền), **tạo bảng**, chèn **logo/ảnh**, chèn **mã vạch HVC** `{MA_VACH_HANG_VAN_CHUYEN}` và chỉnh kích thước qua phần **Nguồn**.
- **Quy tắc ưu tiên:** mẫu in gắn **cửa hàng cụ thể** được **ưu tiên**; mẫu không gắn cửa hàng in được cho mọi CH nhưng **xếp sau**.
- **Ngoại lệ Shopee Express:** in đơn SPX **luôn theo mẫu mặc định của SPX** (kể cả A4/A5) nếu DN có thiết kế riêng; **mẫu A4 / in nhãn / K80 vẫn theo hệ thống**.

### 11.4 Cài đặt đơn hàng & vận chuyển

Nguồn: [Cài đặt vận chuyển](https://pos.manual.nhanh.vn/pos/cai-dat/don-hang/cai-dat-van-chuyen.md)

**Phạm vi áp dụng:** chỉ cho đơn tạo ở **Nhanh.vn, website bán hàng, Vpage** — **KHÔNG áp dụng cho đơn từ sàn** (Shopee, Lazada, Tiktok…).

| Cài đặt | Chi tiết / hãng hỗ trợ |
|---|---|
| **Khai giá hàng hoá** | Chỉ áp dụng cho **kết nối tài khoản riêng của shop**; với **kết nối có sẵn của Nhanh** thì khai giá theo **bảng Dịch vụ phụ** của bảng giá VC |
| **Giao hàng một phần** | Chỉ **GHN, JT Express, Ninja Van, SuperShip** |
| **Tự động thêm ghi chú** | Ghi chú gửi kèm khi gửi HVC, **không hiện** ở ghi chú đơn |
| **Cho khách xem hàng** | 4 lựa chọn |
| **Gửi vận chuyển** | Lấy **SĐT nhân viên tạo đơn** thay SĐT cửa hàng (**GHTK, EMS chưa hỗ trợ**) |
| **Chọn kho lấy hàng khác địa chỉ kho xử lý đơn** | Chỉ **GHN, BEST** |
| **Trạng thái đơn khi gửi HVC bị lỗi** | Giữ nguyên / Đã đóng gói / Chờ thu gom / Đang chuyển |
| **Trạng thái đơn sau khi gửi HVC** | Chọn 1 trong 4: Đã xác nhận / Đang đóng gói / Đã đóng gói / **Chờ thu gom** (mặc định) |
| **Shop tự mang hàng ra bưu cục** | Chọn cửa hàng |

**Quy trình gợi ý của Nhanh** (đáng dùng làm “wizard”): Gửi HVC → *Đã xác nhận* → In đơn kèm mã vận đơn → *Đang đóng gói* → dùng **Đóng gói** → *Đã đóng gói* → gửi bưu tá → *Đang chuyển*.
**Cảnh báo liên quan kiểm kho:** đơn ở **Đã xác nhận, Đang đóng gói, Đã đóng gói** vẫn **tính Tồn trong kho** ⇒ khi kiểm kho phải kiểm thêm; DN dùng tồn để bán nên chọn trạng thái **Chờ thu gom**.

### 11.5 Các trang cài đặt khác (chưa khảo sát chi tiết)
Bán hàng và XNK · Hoá đơn điện tử · Cài đặt đơn hàng · **Cài đặt phí ship báo khách** · Kết nối dịch vụ vận chuyển · **Cài đặt nhãn** · Gửi Zalo ZBS (8 trang) · Gửi Email · Gửi SMS · Thanh toán (TPBank, MoMo DN, **Cài đặt xác nhận thanh toán**: Momo/TPBank/VPBank) · Chi nhánh · Đại lý · Hạn sử dụng · Thông báo · Driver máy in · Đồng bộ Bartender.

---

## 12. Quy trình end-to-end quan trọng

### 12.1 Bán lẻ tại quầy (POS)
`Mở ca` → *(Khai tiền vào ca: đếm mệnh giá hoặc nhập tổng)* → **F1 mở hoá đơn** → quét mã vạch / gõ tên (≥3 ký tự) / **F3 tìm SP** → *(SP cân đo: quét mã vạch cân điện tử; SP IMEI: “tìm IMEI” hoặc nhập IMEI)* → **F4 khách hàng** (quét mã thẻ nếu có) → chiết khấu (F6 **hoặc** CK theo SP, **không cả hai**) / quà tặng / tiêu điểm → **thanh toán** (F8 tiền mặt, CK/quẹt thẻ/trả góp; đối soát **tab Xác nhận thanh toán** nếu CK) → **F9 lưu** hoặc **F10 lưu + in** → *(tuỳ chọn)* **xuất HĐĐT nháp/phát hành HĐĐT** → **Thao tác Nhanh → Thêm chi phí khác** (nếu có) → **Kết ca** (+ in phiếu bàn giao) → `Báo cáo > Bán lẻ > Kết ca` đối chiếu **chênh lệch**.
Nguồn: [Bán lẻ](https://pos.manual.nhanh.vn/pos/kenh-ban/cua-hang/ban-le.md) · [Kết ca](https://pos.manual.nhanh.vn/pos/kenh-ban/cua-hang/ket-ca.md)

### 12.2 Xử lý đơn sàn TMĐT (lấy Shopee làm mẫu)
`Kết nối gian hàng (OTP + cấp quyền)` → `Cấu hình đồng bộ` (bật tự tải đơn, tự tải bản in, **ngày bắt đầu XNK**, gắn KH) → `Cấu hình kho` (1 kho / đa kho / **FBS**) → **Tải & ghép sản phẩm** (ghép thủ công hoặc Excel; hoặc để hệ thống tự tạo SP theo SKU) → **Tải đơn** (≤15 ngày) → **Báo sàn “Sẵn sàng đi giao”** (qua biên bản bàn giao hoặc danh sách đơn) → **In phiếu gửi / in phiếu nhặt hàng** → **đóng gói + biên bản bàn giao** → *tự động cập nhật trạng thái* (`READY_TO_SHIP→Đã xác nhận`, `SHIPPED→Đang chuyển`, `STATUS_COMPLETED→Thành công`) → *(nếu hoàn)* đổi **Đã hoàn** hoặc thêm vào biên bản **Nhận HVC hoàn về** → **Đối soát COD** (xuất file ví từ sàn → chuẩn hoá file → import + ngày nhận tiền) → *(nếu dùng kế toán)* **Bút toán chuyển quỹ** từ **Ví trên sàn** về ngân hàng → kiểm **Chênh lệch đối soát** → chốt **chi phí sàn** (Nợ 641/Có 112).
Nguồn: [Các bước đồng bộ cơ bản](https://pos.manual.nhanh.vn/pos/kenh-ban/shopee/cac-buoc-dong-bo-co-ban.md) · [Đối soát sàn](https://pos.manual.nhanh.vn/pos/kenh-ban/shopee/doi-soat-cod.md)

### 12.3 Nhập kho từ nhà cung cấp
`Nhập hàng về kho + phân loại SP` → `Kiểm SL & chất lượng` → **Kho hàng > Xuất nhập kho > Thêm mới > Nhập kho** → nhập NCC, SP (IMEI mỗi dòng 1 IMEI; tối đa ~200 SP/phiếu; điền **Ngày cảnh báo trước + Ngày hết hạn** nếu là SP theo lô/hạn dùng) → **Lưu** → **In phiếu nhập (2 bản, ký 2 bên)** → **In tem/mã vạch và dán lên SP** (in theo phiếu XNK đúng SL) → *(nếu dùng kế toán)* điền số tiền + chọn quỹ, nếu không sẽ thành **công nợ 331**.
Nguồn: [Thao tác xuất nhập](https://pos.manual.nhanh.vn/pos/kho-hang/xuat-nhap-kho/thao-tac-xuat-nhap.md) · [In tem sản phẩm](https://pos.manual.nhanh.vn/pos/san-pham/danh-sach-san-pham/in-tem-san-pham.md)

### 12.4 Kiểm kho
`Ngừng bán hàng & XNK` → `Xác nhận phiếu chuyển kho + phiếu nháp` → `Cập nhật đúng trạng thái đơn (kể cả đơn sàn)` → `Xác nhận đơn hoàn → Đã hoàn` → **Chạy lại tồn** (tính lại tạm giữ/đang chuyển + số chuyển kho) → `Phân công ≥3 cặp / 3 khu vực` → **Tạo phiếu kiểm** (chọn kho + **loại kiểm** + ghi chú khu vực) → **Thêm SP** (máy tít hoặc nhập tay hoặc **file Excel**) → **Gộp phiếu** → **Tìm sản phẩm thiếu** → rà soát 4 tab *Tất cả/Khớp/Lệch/Chưa kiểm* → **Bù trừ kiểm kho** (sinh phiếu Xuất + Nhập) → **Báo cáo kiểm kho** (giá trị thất thoát theo giá nhập/bán/vốn).
Nguồn: [Kiểm kho](https://pos.manual.nhanh.vn/pos/kho-hang/kiem-kho.md)

### 12.5 Chuyển kho (có kiểm soát)
`Thêm phiếu chuyển kho nháp` (hoặc **Gọi hàng từ kho khác**) → **Duyệt** (sinh **phiếu Xuất [C]**, tồn kho chuyển giảm, SP sang **Đang chuyển kho**) → **Xác nhận** tại kho nhận (sinh **phiếu Nhập [C]**, kho nhận tăng tồn) → *(tuỳ chọn)* dùng **Phân phối chuyển kho theo tỷ lệ** hoặc **Import điều chuyển nhiều kho**.
Biến thể nhanh: **Thêm phiếu chuyển kho** (1 bước, sinh ngay 2 phiếu).
Nguồn: [Danh sách chuyển kho](https://pos.manual.nhanh.vn/pos/kho-hang/chuyen-kho/ds-chuyen-kho.md)

### 12.6 Trả hàng (tại quầy)
`Tìm hoá đơn gốc` (ID / SĐT / ngày / SP) → **Đổi trả hàng** → *chỉ trả* (xoá SP không trả, sửa SL) **hoặc** *trả + mua mới* (nhập SP mới, áp tiêu điểm/coupon/CK) → nhập **Phí trả hàng** → hệ thống báo **“cần trả khách” / “khách trả thêm”** → chọn phương thức → **Lưu** / **Lưu và in** → *(hệ quả)* trừ điểm tích luỹ đã tặng, *(nếu kế toán)* **tự sinh phiếu chi**.
Không có hoá đơn: chọn **cửa hàng nhận hàng trả** + **Loại phiếu** (bán lẻ/bán sỉ).
Nguồn: [Trả hàng](https://pos.manual.nhanh.vn/pos/kenh-ban/cua-hang/tra-hang.md)

### 12.7 Xuất hoá đơn điện tử
`Kết nối pháp nhân + tài khoản cổng HĐĐT` → `Ghép sản phẩm xuất HĐĐT` (**tab “Sản phẩm chưa ghép”**) + `Điền VAT` → **Phát hành HĐĐT** từ danh sách hoá đơn bán lẻ/bán sỉ/đơn hàng (hoặc từ tab **Cần xử lý**) → theo dõi **4 tab** → xử lý **lỗi** theo danh mục lỗi → *khi cần sửa*: **Hoá đơn điều chỉnh** / **Huỷ** (chỉ HĐ nháp) / mẫu **04/SS-HĐĐT**.
Nguồn: [Danh sách hoá đơn điện tử](https://pos.manual.nhanh.vn/pos/kenh-ban/hoa-don-dien-tu/danh-sach-hoa-don-dien-tu.md) · [Nhanh Invoice](https://pos.manual.nhanh.vn/nhanh-invoice/noi-dung-chung.md)

### 12.8 Đối soát COD (đơn gửi qua Nhanh)
`HVC đối soát với Nhanh 1 ngày sau khi đơn Thành công/Chuyển hoàn` → `Nhanh đối soát & thanh toán COD về TK ngân hàng đã ký hợp đồng` → shop **vào tab Thanh toán của đơn** xem phí/“chưa được thanh toán” → **Xác nhận đã nhận tiền** → *(hệ quả hạch toán)*: **Nợ 131/Có 711** (phí thu của khách) · **Nợ 1121/Có 131** (tiền còn lại) · **Nợ 811/Có 1121** (phí VC + vượt cân, đối tượng NCC Nhanh.vn). **Lịch trả:** T2–T6 (GHN: nội tỉnh T2–T6, liên tỉnh **T2/T4/T6**); **ngày trả = ngày vận đơn Thành công + 2**.
Nguồn: [Quy trình xử lý đơn hàng](https://pos.manual.nhanh.vn/pos/don-hang/quy-trinh-xu-ly-don/quy-trinh-xu-ly-don.md) · [Hạch toán tự động](https://pos.manual.nhanh.vn/pos/ke-toan/hach-toan-tu-dong.md)

### 12.9 Mua hàng có tích điểm / lên cấp độ
`Tạo cấp độ (Rule + min/max + hạn mức điểm)` → `Tạo CT tích điểm` (theo HĐ/danh mục/SP; tỉ lệ tích & tiêu) → `Tạo CT chiết khấu theo cấp độ/nhóm` → Bán hàng: **nhập Tên + SĐT** → ô **Tiêu điểm tích luỹ** xuất hiện → lưu HĐ → hệ thống **tự cộng điểm + tự nâng/hạ cấp độ** → khách xem ở tab **Thông tin** (Tổng SP đã mua, Tổng tiền tích luỹ).
Nguồn: [Tích điểm](https://pos.manual.nhanh.vn/pos/khuyen-mai/tich-diem.md) · [Cấp độ khách hàng](https://pos.manual.nhanh.vn/pos/khach-hang/cap-do-khach-hang.md)

### 12.10 Xử lý hàng lỗi
`Phát hiện hàng lỗi` → **Kho hàng > Hàng lỗi > Nhập hàng lỗi** (kê khai SL + lý do) → **tồn Lỗi ↑, Có thể bán ↓** → chọn hướng: **Lập phiếu xuất [N]** (trả NCC — phải cùng kho + cùng NCC) hoặc **xuất huỷ** (phiếu xuất khác, điền **SL lỗi**) → khi kiểm kho **phải kiểm cả hàng lỗi**.
Nguồn: [Hàng lỗi](https://pos.manual.nhanh.vn/pos/kho-hang/hang-loi.md)

---

## 13. Điểm UI/UX đặc trưng quan sát được

### 13.1 Bộ lọc & tương tác bảng
- **2 tầng bộ lọc** (cơ bản / nâng cao) ở **mọi** danh sách, mở bằng **nút mũi tên cạnh chữ “Lọc”** — pattern nhất quán toàn hệ thống.
- **Bộ lọc đã lưu**: đặt tên, **ghi đè**, xoá, **kéo‑thả sắp xếp thứ tự**.
- **Gợi ý tìm kiếm chỉ xuất hiện khi gõ ≥ 3 ký tự**.
- **2 chế độ tìm kiếm** cấu hình được: *tuần tự* vs *gần đúng* (kèm % chính xác 30–90%).
- **Ẩn/hiện cột** + **“Quay về mặc định”**; cột mặc định được đánh dấu **icon xanh mờ** (không tắt được).
- **Phân trang 50 dòng**, override bằng **`&icpp=200`** (tối đa 200).
- **Cột số liệu màu xanh = click được** để drill‑down (ID, tồn kho, số tiền công nợ, số lượng IMEI…).
- **Hover để xem chi tiết** (hover ID phiếu → ngày giờ tạo); **tooltip giải thích** (thống kê trạng thái đơn của khách).
- **Màu ngữ nghĩa**: đỏ = chiết khấu / lỗi / quá hạn / ngoài hạn mức; xanh = trong hạn mức / còn hạn; đen = giá riêng của IMEI/lô, đỏ = giá của sản phẩm.

### 13.2 Thao tác hàng loạt (bulk) — xuất hiện ở hầu hết module
Checkbox chọn dòng → nút **Thao tác** → menu: **Xuất dữ liệu / Xuất Excel / Xuất Google Sheets · Gắn nhãn · Đổi trạng thái · Gộp đơn · Gộp phiếu kiểm · Xoá các dòng đã chọn · Tặng/trừ điểm · Cập nhật nhóm KH · Lập phiếu xuất [N] · In mã vạch phiếu đã chọn · Báo sàn sẵn sàng đi giao · Nhập trạng thái từ Excel**.
Có hành vi **đổi ngày chứng từ** (XNK) và **đổi ngày giao dịch** (bút toán) nhưng thường **bị chặn theo cấu hình số ngày**.

### 13.3 In ấn
- In **tem/mã vạch** trực tiếp từ web (EAN13, C128, C128A, C39; tối đa **3/4 dòng** thông tin trên tem; căn lề 0 mm; bỏ “Vừa với trang” nếu lệch) hoặc **xuất Excel cho Bartender**.
- **Mẫu in** = trình soạn thảo WYSIWYG với **chèn từ khoá** (doanh nghiệp/hoá đơn/sản phẩm/khách), bảng, logo, **mã vạch HVC**; có **lịch sử + khôi phục mẫu in**; **mẫu gắn cửa hàng được ưu tiên**.
- In **hàng loạt**: nhiều phiếu XNK, nhiều hoá đơn, nhiều đơn sàn (**phiếu nhặt hàng nhiều đơn**, mẫu tuỳ chỉnh nhưng vẫn đủ **mã vạch/mã QR vận đơn**).
- Khổ in: A4/A5/**K80**, **in nhãn**; cảnh báo **iOS không chọn được A5**.

### 13.4 Màn hình POS
- **Multi-tab hoá đơn** trên 1 màn hình (F1 mở tab mới), lưu tab trong **IndexedDB** → có FAQ hướng dẫn **clear Object Store** khi mở quá nhiều tab.
- **Dãy phím tắt F1/F3/F4/F6/F8/F9/F10** — đây là **nơi duy nhất** trong tài liệu nêu phím tắt; các module khác **tài liệu không mô tả phím tắt**.
- **3 cách quét mã vạch** (quét tích luỹ SL / quét rồi sửa SL / bật ô SL cạnh ô tìm).
- **Chọn chế độ tìm**: *Tìm sản phẩm* / *Cân điện tử* / *IMEI*; có **“tìm IMEI”** ngay đầu thanh tìm kiếm.
- **Tự động điền** chiết khấu, quà tặng, điểm tích luỹ; có nút **bỏ chiết khấu tự động / bỏ tích điểm tự động** cho từng khách.
- **Cảnh báo tức thời khi đối soát CK**: thừa tiền / thiếu tiền / khách sửa nội dung CK; tab **Chờ xác nhận** – **Hủy xác nhận**; nút **Kiểm tra giao dịch** + **Làm mới**.
- **Phân quyền theo nút** (sửa giá vs sửa chiết khấu) hiển thị ngay trên màn hình bán.
- **Chống mất dữ liệu**: phiếu kiểm kho **tự lưu sau mỗi thao tác thêm SP**.

### 13.5 Pattern “trạng thái điều khiển UI”
- **Tab set thay đổi theo trạng thái** (chi tiết đơn hàng: chưa gửi HVC vs đã gửi HVC; đơn tự vận chuyển mất 2 tab).
- **Nút bị ẩn/disable theo trạng thái** (bảng “Thao tác + Trạng thái áp dụng” của đơn hàng; “In & đóng gói không hỗ trợ đơn Thành công/Đang hoàn/Đã hoàn”).
- **Cột điều kiện** (cột “SP thiếu”, “Bù trừ kiểm kho” chỉ hiện với phiếu chưa bù trừ / loại kiểm phù hợp).
- **Trạng thái không thể đảo ngược** có cảnh báo rõ: **Kết ca**, **Gộp phiếu kiểm kho**, **CTKM không sửa/xoá được**.

### 13.6 Trợ giúp & “self‑service sửa lỗi dữ liệu”
- Hệ thống có **bộ nút “tính lại”** rải khắp nơi: *Tính lại số tồn · Tính lại giá vốn · Tính lại số đang chuyển kho · Tính lại tạm giữ & đang chuyển · Tính lại tồn sản phẩm (kiểm kho) · Cập nhật lại giá sản phẩm (bảng giá) · Cập nhật lại cấp độ khách hàng · Hạch toán lại kế toán · Set lại cha con · Set lại SP hot/mới* → cho thấy đây là hệ thống **chấp nhận sai lệch dữ liệu do thao tác đồng thời** và cung cấp **công cụ tự phục hồi** thay vì chặn giao dịch.
- **Business Tools** là “trang y tế” tập trung cho 3 loại lỗi tồn kho.
- Rất nhiều **FAQ dạng “tại sao số liệu A ≠ B”** → hệ thống có **nhiều nguồn tính toán song song** (báo cáo realtime vs file xuất; giá vốn tại thời điểm thành công vs hiện tại).

### 13.7 Những chỗ tài liệu KHÔNG mô tả (cần khảo sát thêm nếu làm lại)
- Không có **bảng liệt kê cột đầy đủ** cho phần lớn danh sách (trừ bán lẻ, bán sỉ, XNK, kiểm kho, công nợ, HĐĐT) → phần lớn dựa vào **screenshot**, chữ không đặc tả.
- **Không mô tả phím tắt** ngoài màn hình bán lẻ.
- Không mô tả **responsive/mobile**; chỉ có ghi chú về **thiết bị bán hàng** và **máy hiện QR Code** (2 trang trong mục lục, chưa khảo sát).
- Không mô tả **empty state / loading state / error boundary** ngoài danh mục lỗi HĐĐT.
- Không mô tả **i18n** cho UI (chỉ có “Dịch nhiều ngôn ngữ” cho website).
- Không mô tả **API spec** (chỉ link sang `apidocs.nhanh.vn` / `open.nhanh.vn` v3.0).

---

## 14. Danh sách màn hình ưu tiên nếu làm lại Web Admin

Tiêu chí phân loại: **P0** = thiếu thì không bán được hàng/không ra được số liệu; **P1** = cần cho vận hành đa kênh & kiểm soát; **P2** = hoàn thiện trải nghiệm/quản trị nâng cao.

### P0 — Lõi giao dịch & tồn kho (không thể thiếu)
| # | Màn hình | Vì sao P0 | Nguồn |
|---|---|---|---|
| 1 | **Khung UI chung**: danh sách + bộ lọc 2 tầng + bộ lọc đã lưu + ẩn/hiện cột + phân trang + xuất Excel/Sheets + import | Mọi module phụ thuộc; tái sử dụng 1 lần cho ~50 màn | [Các tính năng chung](https://pos.manual.nhanh.vn/gioi-thieu-chung/cac-tinh-nang-chung.md) |
| 2 | **POS bán lẻ** (multi-tab, F1–F10, quét mã vạch, IMEI/cân đo, CK, quà tặng, tiêu điểm, thanh toán đa phương thức, đối soát QR) | Doanh thu chính; phức tạp nhất | [Bán lẻ](https://pos.manual.nhanh.vn/pos/kenh-ban/cua-hang/ban-le.md) |
| 3 | **Danh sách sản phẩm + Chi tiết sản phẩm** (9 loại SP, 4 trạng thái, 9 loại tồn, 7 loại giá, 6 nút “tính lại”, cha–con–thuộc tính) | Nền của mọi thứ | [Trang DSSP](https://pos.manual.nhanh.vn/pos/san-pham/danh-sach-san-pham/trang-dssp.md) |
| 4 | **Thêm/Sửa sản phẩm + Import Excel** (2 ngành hàng mẫu, import ảnh, chia thuộc tính) | Onboarding dữ liệu | [Thêm sản phẩm](https://pos.manual.nhanh.vn/pos/san-pham/them-sp.md) |
| 5 | **Xuất nhập kho** (danh sách phiếu + danh sách SP XNK, 5 loại phiếu, công thức tổng tiền, in phiếu, in mã vạch, đổi ngày) | Ghi nhận tồn & giá vốn | [DS XNK](https://pos.manual.nhanh.vn/pos/kho-hang/xuat-nhap-kho/ds-xnk.md) |
| 6 | **Tạo phiếu nhập NCC / xuất trả NCC** | Nguồn giá vốn + công nợ 331 | [Thao tác XNK](https://pos.manual.nhanh.vn/pos/kho-hang/xuat-nhap-kho/thao-tac-xuat-nhap.md) |
| 7 | **Danh sách đơn hàng** (18 trạng thái, 13 loại đơn, 5 bàn giao, thao tác theo trạng thái, gộp đơn, gắn nhãn, import trạng thái) | Trung tâm đa kênh | [DS đơn hàng](https://pos.manual.nhanh.vn/pos/don-hang/danh-sach-don-hang/danh-sach-don-hang.md) |
| 8 | **Chi tiết đơn hàng** (tab set động theo trạng thái, In & đóng gói, Gửi VC, Lịch trình, Thanh toán, Khiếu nại) | Nơi xử lý thực tế | [Chi tiết đơn](https://pos.manual.nhanh.vn/pos/don-hang/danh-sach-don-hang/chi-tiet-don-hang.md) |
| 9 | **Kiểm kho** (3 loại, gộp phiếu, tìm SP thiếu, bù trừ, tự lưu) | Chống thất thoát; nghiệp vụ VN đặc thù | [Kiểm kho](https://pos.manual.nhanh.vn/pos/kho-hang/kiem-kho.md) |
| 10 | **Trả hàng** (có/không hoá đơn, đổi hàng, phí trả hàng, IMEI) | Bắt buộc theo luật & thực tế | [Trả hàng](https://pos.manual.nhanh.vn/pos/kenh-ban/cua-hang/tra-hang.md) |
| 11 | **Kết ca + Báo cáo kết ca** | Quản lý tiền mặt tại quầy | [Kết ca](https://pos.manual.nhanh.vn/pos/kenh-ban/cua-hang/ket-ca.md) |
| 12 | **Danh sách khách hàng + Chi tiết KH** (5 tab, 5 tab phân khúc, thao tác chăm sóc hàng loạt) | Gắn với mọi hoá đơn | [Thêm KH](https://pos.manual.nhanh.vn/pos/khach-hang/them-khach-hang.md) |
| 13 | **Phân quyền: Nhân viên / nhóm quyền / quyền riêng / quyền xem dữ liệu** | Chặn thao tác sai; bắt buộc trước khi mở cho nhiều người | [Nhân viên](https://pos.manual.nhanh.vn/pos/cai-dat/nhan-vien.md) |
| 14 | **Mẫu in** (WYSIWYG + từ khoá + lịch sử/khôi phục + ưu tiên theo cửa hàng) | Không có mẫu in = không giao được hàng | [Mẫu in](https://pos.manual.nhanh.vn/pos/cai-dat/mau-in.md) |
| 15 | **Dashboard Tổng quan** (lọc thời gian/cửa hàng; GTTB; top SP; giá trị tồn) | Màn hình mở đầu của chủ shop | [Tổng quan](https://pos.manual.nhanh.vn/pos/bao-cao/tong-quan.md) |

### P1 — Đa kênh, đối soát & kiểm soát
| # | Màn hình | Vì sao P1 |
|---|---|---|
| 16 | **Kết nối kênh bán + cấu hình đồng bộ + cấu hình kho (1 kho/đa kho/FBS)** | Điều kiện để bán sàn |
| 17 | **Ghép nối sản phẩm sàn** (thủ công + Excel + tự tạo theo SKU) | Chặn tải đơn nếu chưa ghép |
| 18 | **Đơn sàn + “Báo sàn sẵn sàng đi giao” + In phiếu gửi/phiếu nhặt hàng** | Luồng fulfillment sàn |
| 19 | **Đối soát COD sàn** (import file + ngày nhận tiền + tab chênh lệch + 2 nhánh có/không kế toán) | Dòng tiền thực |
| 20 | **Biên bản bàn giao** (gom nhiều đơn, gửi HVC, nhận hoàn) | Chống thất thoát & lấy mã vận đơn |
| 21 | **Chuyển kho** (phiếu + phiếu nháp + duyệt/xác nhận + phân phối theo tỷ lệ) | Doanh nghiệp nhiều kho |
| 22 | **Hàng lỗi** (nhập hàng lỗi, xuất trả NCC, xuất huỷ) | Ảnh hưởng tồn có thể bán |
| 23 | **Hạn sử dụng + Lô sản phẩm** | Ngành mỹ phẩm/thực phẩm/dược |
| 24 | **Khuyến mại: chiết khấu (6 loại + nâng cao 4 nhóm điều kiện)** | Doanh thu & cạnh tranh |
| 25 | **Mã giảm giá (coupon)** + phân bổ coupon về SP | Yêu cầu marketing phổ biến |
| 26 | **Tích điểm + Cấp độ + Nhóm khách hàng** | Giữ chân khách; liên kết CTKM |
| 27 | **Thiết lập bảng giá** (giá theo chi nhánh/nhóm/thời điểm + công thức + cảnh báo) | Giá đa kênh/đa tệp |
| 28 | **Báo cáo: Doanh thu · Đơn hàng · Bán lẻ · Tồn kho (Tổng XNK)** | Bộ báo cáo tối thiểu để vận hành |
| 29 | **Công nợ (KH/NCC/Sàn)** + phiếu thu chi | Kiểm soát dòng tiền |
| 30 | **Hoá đơn điện tử** (4 tab + danh mục lỗi + điều chỉnh/huỷ) | Bắt buộc pháp lý VN |
| 31 | **Thu chi tiền mặt/ngân hàng + Import** | Nghiệp vụ kế toán hằng ngày |
| 32 | **Cài đặt chung** (5 toggle SP + 7 toggle kế toán + hạn mức công nợ + 2FA + IP) | Cấu hình hành vi toàn hệ thống |
| 33 | **Cài đặt vận chuyển** (11 toggle, khác biệt theo hãng) | Ảnh hưởng chi phí & tồn kho |

### P2 — Hoàn thiện & nâng cao
| # | Màn hình |
|---|---|
| 34 | **Vị trí sản phẩm trong kho** (danh mục vị trí, vị trí SP, hoá đơn xuất nhập vị trí, báo cáo diện tích lưu kho) |
| 35 | **Hạn mức tồn kho** (min/max + màu cảnh báo + import) |
| 36 | **Phiếu nháp** (6 loại, duyệt/xác nhận, SL duyệt lệch) |
| 37 | **Bút toán + Chuyển quỹ + Import số dư đầu kỳ + Phát sinh đối ứng** |
| 38 | **Hạch toán tự động** (bảng định khoản theo nghiệp vụ) |
| 39 | **Thẻ khách hàng** (phát hành 3 cách, ma trận trạng thái, đổi code, in thẻ) |
| 40 | **Chăm sóc khách hàng** (5 loại hành động, 4 cách thao tác, import) |
| 41 | **Affiliate** (mã giới thiệu, 3 loại CK, hoa hồng CTV, báo cáo) |
| 42 | **Báo cáo bổ sung**: Đơn hàng theo trạng thái/thời gian xử lý/lý do huỷ/hãng VC · SP bán chạy/tốc độ bán · KH chu kỳ mua · Kế toán KQHĐKD/BCĐKT · Khuyến mại · Zalo |
| 43 | **Website builder** (menu, tin tức, banner, album, mã nhúng, tên miền, BCT) |
| 44 | **Bảo hành** (quy trình, tiếp nhận, trung tâm BH, BH mở rộng) |
| 45 | **Gửi Zalo ZBS / Email / SMS** |
| 46 | **Thanh toán** (xác nhận thanh toán MoMo/TPBank/VPBank) |
| 47 | **Nhập máy cũ** (IMEI) · **Đổi IMEI sang SP khác** · **Lệch tồn IMEI** |
| 48 | **Business Tools** (3 tab tính lại) + **Log Profile / Lịch sử sửa / Log sửa phiếu** |
| 49 | **Dự báo nhập hàng · Bán chéo doanh nghiệp · Tổng công ty** |
| 50 | **Thiết bị bán hàng** (máy hiện QR Code) · **Driver máy in** · **Đồng bộ Bartender** |

---

## 15. Ghi chú về độ tin cậy của từng mục

| Mức | Mục |
|---|---|
| **Cao** (tài liệu mô tả bằng bảng/công thức/enum rõ ràng) | Các tính năng chung; 9 loại tồn kho; 4 trạng thái SP; 9 loại SP; 18 trạng thái đơn + map HVC; enum loại đơn; tab set chi tiết đơn; kiểm kho; chuyển kho (trạng thái + quy tắc huỷ); phiếu nháp; hàng lỗi; hạn sử dụng; công thức kết ca; công thức tổng tiền XNK; công thức công nợ; toàn bộ bảng định khoản hạch toán tự động; mã tham chiếu HĐĐT + danh mục lỗi; 6 loại chiết khấu + 4 nhóm điều kiện nâng cao; logic phân bổ coupon; 13 nhóm quyền + bảng quyền bao hàm; 5 toggle SP + 7 toggle kế toán; 13 cài đặt vận chuyển; phím tắt POS; bảng map trạng thái Shopee |
| **Trung bình** (có mô tả nhưng dựa nhiều vào screenshot) | Danh sách cột của DSSP, chi tiết SP, bảng giá, affiliate, mẫu in, website |
| **Thấp / chưa khảo sát** | Dự báo nhập hàng, bán chéo doanh nghiệp, tổng công ty, vị trí kho (5 trang con), hoá đơn nháp, nợ quà tặng, tìm hoá đơn, nhập máy cũ, tiếp thị liên kết, Facebook Shop, Tiktok/Lazada/Tiki/Sendo chi tiết, quà tặng V2, hoa hồng nhân viên, chiết khấu V2, gửi Zalo/Email/SMS, thanh toán, bảo hành, Nhanh Invoice/KOC Invoice, thiết bị bán hàng |

*Các mục “thấp” có thể bổ sung bằng cách fetch thêm trang `.md` tương ứng trong `llms.txt`.*
