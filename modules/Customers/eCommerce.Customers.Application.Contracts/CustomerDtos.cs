namespace eCommerce.Customers;

/// <summary>Dữ liệu khách trả ra ngoài (API/UI) — `CUS-001` §6.1.</summary>
public class CustomerDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;

    /// <summary>Email — **PII**. 📌 Vẫn trả ra cho **người có quyền** (đó là quyền *"xem dữ liệu của mình"*
    /// theo Luật 91/2025/QH15), nhưng **cấm** đưa giá trị này vào **log** (`ADR-11`).</summary>
    public string? Email { get; set; }

    /// <summary>SĐT — **PII**, cùng lưu ý như <see cref="Email"/>.</summary>
    public string? Phone { get; set; }

    public Guid? IdentityUserId { get; set; }

    /// <summary>`0 = Inactive` · `1 = Active` · `2 = Blocked` (xem `CustomerStatus`).</summary>
    public string Status { get; set; } = default!;

    /// <summary>`true` ⇒ khách **KHÔNG** bị chặn (đường bán hàng dùng cờ này — `ADR-04`).</summary>
    public bool DungDuoc { get; set; }

    /// <summary>`true` = **khách vãng lai** (chưa gắn tài khoản) — `ADR-03`.</summary>
    public bool LaKhachVangLai { get; set; }

    public Guid? CustomerGroupId { get; set; }
    public decimal? LifetimeValueAmount { get; set; }
    public string? LifetimeValueCurrency { get; set; }
    public DateTime CreationTime { get; set; }
}

/// <summary>Tạo khách mới (idempotent theo <see cref="IdempotencyKey"/>).</summary>
public class TaoKhachInput
{
    public string Name { get; set; } = default!;
    public string? Email { get; set; }
    public string? Phone { get; set; }

    /// <summary>
    /// 🔑 **Khoá idempotency** (`ADR-05`) — **BẮT BUỘC** ở biên vào (webhook/queue).
    /// 🚨 Phải sinh từ **dữ liệu nghiệp vụ ỔN ĐỊNH** (ví dụ `"shopee:order:12345"`), **KHÔNG** `Guid.NewGuid()`:
    /// khoá ngẫu nhiên thì **mỗi lần retry là một khách mới** — đúng lỗi `XC-002` **D7**.
    /// 📌 Để trống = **không** dùng idempotency (đường tạo tay của nhân viên, đã có người xác nhận).
    /// </summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>Nâng cấp khách vãng lai thành khách có tài khoản (`ADR-07`).</summary>
public class NangCapKhachInput
{
    /// <summary>Id tài khoản ABP Identity (`AbpUsers.Id`) sẽ trở thành **chủ sở hữu** của hồ sơ khách.</summary>
    public Guid IdentityUserId { get; set; }
}

/// <summary>
/// 🚨 **HỢP ĐỒNG ĐỌC SNAPSHOT KHÁCH CHO MODULE KHÁC** — `CUS-001` **ADR-12**.
///
/// **VÌ SAO PHẢI CÓ INTERFACE NÀY (không cho module khác join thẳng bảng):**
/// quy ước **#3** bất di bất dịch của dự án: *"mọi module sở hữu entity của mình; **module → app là phụ thuộc
/// NGƯỢC, cấm**"* (`docs/structure-and-techstack.md`). Nếu `Sales`/`Pricing` join thẳng `CusCustomers` thì:
/// <list type="bullet">
///   <item>đổi **cấu trúc** bảng khách ⇒ **vỡ** những module không liên quan (mà không có lỗi biên dịch);</item>
///   <item>bất biến của khách (ví dụ **che PII**) bị **vượt qua** ở mọi chỗ đọc thẳng.</item>
/// </list>
/// 📌 Tiền lệ thật: `LOY-001` **D8** đã chọn **ETO** thay vì gọi thẳng vì cùng lý do.
///
/// 🚨 **CỐ Ý KHÔNG kế thừa `IApplicationService` (đã ĐO được, không phải suy đoán):** phiên bản đầu để
/// interface này là `IApplicationService` **và** để `CustomerAppService` hiện thực nó ⇒ swagger thật cho ra
/// `POST /api/app/customer/tim-theo-id/{id}` — tức ba method **chỉ đọc** bị ABP sinh thành **`POST`**
/// (mặc định của conventional controller) ⇒ **sai ngữ nghĩa HTTP** (proxy/CDN coi `POST` là **không
/// idempotent** ⇒ không cache, công cụ vận hành không dám gọi lại).
/// ⇒ Nay: interface **thuần hợp đồng nội bộ**; lớp hiện thực là `CustomerDirectory`
/// (`ITransientDependency`, **không** phải application service) ⇒ **không** sinh endpoint.
/// 📌 `ADR-12` nói rõ hợp đồng này được gọi **trong cùng tiến trình** (interface), **không** qua HTTP.
///
/// 🔒 **Hợp đồng này CHỈ ĐỌC** (không có `Set*`): module khác **không** được đổi trạng thái khách —
/// đó là việc của người vận hành qua app service riêng (có quyền + có audit).
/// </summary>
public interface ICustomerDirectory
{
    /// <summary>Tra khách theo Id. Trả `null` nếu không có (hoặc đã **xoá mềm** — `ADR-02`).</summary>
    Task<CustomerDto?> TimTheoIdAsync(Guid customerId);

    /// <summary>Tài khoản đăng nhập này đã gắn với khách nào chưa (`ADR-07`: claim là **một-một**).</summary>
    Task<CustomerDto?> TimTheoTaiKhoanAsync(Guid identityUserId);

    /// <summary>
    /// **Khách có được phép giao dịch không** (`ADR-04`).
    /// 🚨 `customerId` là `Guid?` vì **khách vãng lai là hạng nhất** (`ADR-03`): `null` ⇒ **LUÔN `true`**
    /// (đơn không có khách vẫn hợp lệ). 📌 Trả `bool` (không ném) để đường bán hàng dùng được ở **điều kiện**
    /// thay vì phải bọc `try/catch`.
    /// </summary>
    Task<bool> ChoPhepGiaoDichAsync(Guid? customerId);
}


// ══════════════════════════════════════════════════════════════════════════════════════════════════
// 🚨 **XUAT DU LIEU CA NHAN** — QUYEN TRUY CAP CUA CHINH CHU (`CUS-001`, Luat **91/2025/QH15**).
//
// 🔑 **VI SAO CO NHOM DTO NAY (khong phai "them mot endpoint cho vui"):**
//
// Luat **91/2025/QH15** cho chu the du lieu quyen **TRUY CAP** (biet minh dang bi luu gi) va **LAY BAN SAO**.
// 📌 Mot he thong **khong** lam duoc dieu do thi **khong tuan thu** — va 🔒 no khong the "chua" bang cach
// ghi trong tai lieu: **khong co duong goi** nghia la **khong co tinh nang**.
//
// ⚠️ **VA VI SAO NO KHONG CHI LA "TRA VE `CustomerDto`":** `CustomerDto` la DTO **van hanh** — no **khong**
// noi *"truong nay o dau ra"*, *"vi sao he thong giu no"*. 📌 Mot ban xuat ma **khong co NGUON** thi nguoi
// doc **khong kiem chung duoc**, va nguoi van hanh **khong tra loi duoc** cau hoi *"sao cac anh co cai nay?"*
// 🔑 Nen moi dong phai kem **nguon** (`Nguon`) va **ly do** (`LyDo`) — do la khac biet giua **ban xuat** va
// **bang du lieu**.
// ══════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// 🚨 **BAN XUAT DU LIEU CA NHAN CUA MOT KHACH** — 🔑 moi dong co **NGUON** va **LY DO**.
/// </summary>
public class XuatDuLieuCaNhanDto
{
    /// <summary>Khach duoc xuat.</summary>
    public Guid CustomerId { get; set; }

    /// <summary>Ten khach — 🔑 de nguoi doc **biet ban xuat nay cua ai** (⚠️ mot ban xuat khong ten chu the
    /// la mot ban xuat **khong kiem chung duoc**).</summary>
    public string TenKhach { get; set; } = default!;

    /// <summary>Thoi diem xuat (UTC) — 🔑 **bat buoc** cho **vet kiem toan**: *"toi da lay du lieu cua minh
    /// luc nao"* la mot cau hoi co that.</summary>
    public DateTime XuatLucUtc { get; set; }

    /// <summary>
    /// Nguoi thuc hien xuat — 📌 thuong la **nhan vien** ho tro (khach chua co kenh tu phuc vu).
    /// 🔒 Ghi lai de tra loi *"ai da xem du lieu cua toi"* — do cung la mot phan cua **tinh than** luat.
    /// </summary>
    public string NguoiXuat { get; set; } = default!;

    /// <summary>⛔ **CANH BAO BAT BUOC** — 🔒 ban xuat chua **du lieu ca nhan**; phai duoc xu ly nhu **tai lieu mat**.</summary>
    public string CanhBao { get; set; } = default!;

    /// <summary>Cac dong du lieu ca nhan — 🔑 **moi dong** tu mo ta **nguon** va **ly do**.</summary>
    public List<DongDuLieuCaNhanDto> CacDong { get; set; } = [];

    /// <summary>
    /// 🚨 **DON HANG CUA KHACH** — 🔑 **phan LON** du lieu ca nhan that nam o day, **khong** o ho so khach.
    ///
    /// 📌 **VI SAO TACH KHOI `CacDong` (khong nhet moi don hang thanh mot dong):** `CacDong` la danh sach
    /// *(truong, gia tri)* **co dinh** cua **ho so**; con don hang la **danh sach DAI va TANG THEO THOI GIAN**.
    /// 🔒 Nhet vao `CacDong` se lam ban xuat **khong doc duoc** khi khach mua nhieu (📌 mot danh sach 500 don
    /// nam lan trong danh sach truong la **vo dung** cho ca hai muc dich).
    /// ➕ Va tach ra thi **dem duoc**: nguoi doc thay ngay *"khach nay co bao nhieu don"*.
    /// </summary>
    public List<DonHangCaNhanDto> DonHang { get; set; } = [];

    /// <summary>
    /// ⚠️ **CO bi CAT khong** — `true` nghia la khach co **nhieu hon** `TranDonHangMotLanXuat` don.
    /// 🔒 Bat buoc phai noi ra: 📌 mot ban xuat **im lang cat bot** se khien nguoi doc tin rang
    /// *"khach chi co bay nhieu don"* — va **do la mot loi khang dinh sai** ve du lieu ca nhan.
    /// </summary>
    public bool DonHangBiCat { get; set; }

    /// <summary>Tong so don cua khach (🔑 ke ca khi bi cat) — de nguoi doc biet **quy mo that**.</summary>
    public int TongSoDon { get; set; }

    /// <summary>
    /// 🚨 `ADDR-004` **B10** — **SỔ ĐỊA CHỈ** trong bản xuất (kể cả mục **đã gỡ**).
    /// 🔑 *"tôi đang được giao tới đâu"* là câu hỏi **chính** của chủ thể dữ liệu; và mục đã gỡ **vẫn** là
    /// dữ liệu hệ thống còn lưu ⇒ phải hiện, có cờ `DaGo` (Luật 91/2025/QH15).
    /// </summary>
    public List<DiaChiCaNhanDto> SoDiaChi { get; set; } = [];
}

/// <summary>
/// Một mục trong **sổ địa chỉ** của bản xuất dữ liệu cá nhân (`ADDR-004` B10).
/// 🔒 Có `Nguon` (bảng/cột) cho từng khối — cùng khuôn `DongDuLieuCaNhanDto`.
/// </summary>
public class DiaChiCaNhanDto
{
    public Guid LinkId { get; set; }

    public Guid AddressId { get; set; }

    /// <summary>`Shipping` / `Billing`.</summary>
    public string AddressType { get; set; } = default!;

    public string? Label { get; set; }

    public bool IsDefault { get; set; }

    /// <summary>🔑 `true` = mục **đã gỡ** (xoá mềm) nhưng hệ thống **vẫn lưu** — chủ thể dữ liệu phải thấy.</summary>
    public bool DaGo { get; set; }

    public DateTime? LastUsedAtUtc { get; set; }

    /// <summary>🔒 **SNAPSHOT CHỮ** lúc tạo (`ADR-10`) — không phải tên ĐVHC hiện tại.</summary>
    public string? DiaChiDayDu { get; set; }

    /// <summary>`ChuaChuanHoa` / `DaChuanHoa` / `DaXacMinh` / `NghiNgo`.</summary>
    public string? TrangThai { get; set; }

    /// <summary>`false` ⇔ địa chỉ chưa chuẩn hoá (không dùng được cho vận đơn).</summary>
    public bool CoDungDuoc { get; set; }

    /// <summary>Nguồn của khối dữ liệu này — 🔑 để người đọc **kiểm chứng** được (cùng tinh thần `CacDong`).</summary>
    public string Nguon { get; set; } = default!;
}

/// <summary>Mot don hang trong ban xuat du lieu ca nhan.</summary>
public class DonHangCaNhanDto
{
    public Guid OrderId { get; set; }

    /// <summary>Ma don (hien thi cho khach — 📌 khach quen ma don, khong quen GUID).</summary>
    public string SoDon { get; set; } = default!;

    /// <summary>Kenh ban (`shopee` / `web`…) — 🔑 khach can biet don den tu dau.</summary>
    public string MaKenh { get; set; } = default!;

    public string TienTe { get; set; } = default!;

    public decimal TongTien { get; set; }

    /// <summary>Trang thai don — 🔑 tra loi *"don cua toi dang o dau"* (cau hoi pho bien nhat).</summary>
    public string TrangThai { get; set; } = default!;

    /// <summary>Trang thai thanh toan — ⚠️ 🔑 khach can biet **da tra tien chua**, khong chi *"don da xac nhan"*.</summary>
    public string TrangThaiThanhToan { get; set; } = default!;

    public DateTime NgayTaoUtc { get; set; }

    /// <summary>Cac dong hang — 🔑 *"toi da mua gi"* la **cau hoi chinh** cua chu the du lieu.</summary>
    public List<DongHangCaNhanDto> CacDong { get; set; } = [];
}

/// <summary>Mot dong hang trong ban xuat.</summary>
public class DongHangCaNhanDto
{
    /// <summary>Ma SKU — ⚠️ **KHONG** phai ten san pham: 📌 ten san pham nam o `CatProducts` va **co the doi**;
    /// 🔑 ban xuat phai phan anh **thoi diem mua**, nen ghi **ma** (bat bien) chu khong chi **ten** (co the doi).</summary>
    public string MaSku { get; set; } = default!;

    public int SoLuong { get; set; }

    public decimal DonGia { get; set; }

    /// <summary>🔑 **NGUON** — cung ly do nhu `DongDuLieuCaNhanDto.Nguon`.</summary>
    public string Nguon { get; set; } = default!;
}

/// <summary>
/// Mot dong du lieu ca nhan — 🔑 **KHONG** chi la *(ten truong, gia tri)*.
/// </summary>
public class DongDuLieuCaNhanDto
{
    /// <summary>Ten truong theo **NGON NGU NGUOI DUNG** (`Ho ten`, `Email`…), khong phai ten cot DB.
    /// 📌 Nguoi doc ban xuat la **chu the du lieu** hoac **nhan vien ho tro**, khong phai lap trinh vien.</summary>
    public string Truong { get; set; } = default!;

    /// <summary>Gia tri — ⚠️ `null` = he thong **khong** co du lieu do (📌 phai hien **ro** la trong, khong bo dong:
    /// 🔑 *"khong co"* va *"khong hien"* la **hai dieu khac nhau** voi nguoi dang hoi *"cac anh giu gi ve toi"*).</summary>
    public string? GiaTri { get; set; }

    /// <summary>
    /// 🔑 **NGUON** — bang/cot that trong he thong (vi du `CusCustomers.Email`).
    /// 🔒 Bat buoc: mot ban xuat **khong co nguon** thi **khong kiem chung duoc**.
    /// </summary>
    public string Nguon { get; set; } = default!;

    /// <summary>
    /// 🔑 **LY DO he thong giu truong nay** — tra loi cau hoi *"sao cac anh co cai nay?"*.
    /// 📌 Day la phan **luat** yeu cau (muc dich xu ly), khong phai phan ky thuat.
    /// </summary>
    public string LyDo { get; set; } = default!;

    /// <summary>
    /// ⚠️ `true` = **du lieu ca nhan** (PII) — 🔒 phai duoc bao ve khi luu/truyen.
    /// 📌 Cac dong `false` (vi du `Trang thai`) van nam trong ban xuat vi chung **thuoc ve** chu the,
    /// nhung **khong** canh bao nhu PII.
    /// </summary>
    public bool LaDuLieuCaNhan { get; set; }
}


// ══════════════════════════════════════════════════════════════════════════════════════════════════
// 🚨 **DOC NHAT KY KIEM TOAN XUAT DU LIEU CA NHAN** (`CUS-001` §23).
//
// 🔑 **VI SAO CAN DTO NAY (khong tra thang entity):**
//
// Bang `CusDataExportLogs` la **bang chung** — 🔒 nhung mot bang chung **khong ai doc duoc** thi **khong phuc
// vu** cau hoi nao. 📌 O §22 toi da ghi no: *"chua co UI xem nhat ky ⇒ nguoi kiem toan phai TRUY VAN SQL"*.
//
// ⚠️ **VA VI SAO KHONG TRA ENTITY:** entity co `TenantId`, `CreatorId`, va cac chi tiet **noi bo** khong giup
// nguoi doc. 🔑 Mot DTO **chi co thu nguoi kiem toan can** thi man hinh **khong bi roi** — 📌 va do cung la
// cach **khong vo tinh** lo cau truc noi bo ra ngoai.
// ══════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// 🚨 **MOT DONG NHAT KY KIEM TOAN** — tra loi *"**AI** da xuat du lieu cua toi, **KHI NAO**"*.
/// </summary>
public class NhatKyXuatDuLieuDto
{
    public Guid Id { get; set; }

    /// <summary>Khach **bi xuat** — 🔑 cot quan trong nhat khi tra theo mot nguoi cu the.</summary>
    public Guid CustomerId { get; set; }

    /// <summary>
    /// Ten khach — ⚠️ **CO THE NULL**: 🔒 nhat ky phai **song sot** ke ca khi ho so khach bi xoa
    /// (📌 do la ly do `CustomerId` **khong** dat FK cung). 🔑 Khi null thi man hinh phai **noi ro**
    /// *"(ho so da bi xoa)"* — ⚠️ chu **khong** duoc de trong (de trong trong nhu **loi hien thi**).
    /// </summary>
    public string? TenKhach { get; set; }

    /// <summary>🔑 **AI** da xuat.</summary>
    public string NguoiXuat { get; set; } = default!;

    /// <summary>🔑 **KHI NAO** (UTC).</summary>
    public DateTime XuatLucUtc { get; set; }

    public int SoDongHoSo { get; set; }

    public int SoDonHang { get; set; }

    /// <summary>⚠️ Ban xuat co **bi cat bot** khong (nhieu don hon tran).</summary>
    public bool DonHangBiCat { get; set; }

    public string? LyDo { get; set; }
}

/// <summary>
/// **KET QUA TRA CUU NHAT KY** — 🔑 **gom ca TONG SO** de nguoi doc biet *"dang xem mot phan hay tat ca"*.
/// </summary>
public class KetQuaNhatKyXuatDuLieuDto
{
    /// <summary>Cac dong nhat ky (moi nhat truoc).</summary>
    public List<NhatKyXuatDuLieuDto> CacDong { get; set; } = [];

    /// <summary>
    /// 🔑 **TONG SO dong khop bo loc** (ke ca khi `CacDong` bi cat theo tran).
    /// ⚠️ **BAT BUOC** — 📌 cung ly do nhu `DonHangBiCat` o §21: 🔒 mot man hinh **im lang cat bot** se khien
    /// nguoi kiem toan tin rang *"chi co bay nhieu lan xuat"*, va 🔑 do la mot **ket luan sai ve bang chung**.
    /// </summary>
    public int TongSo { get; set; }

    /// <summary>⚠️ `true` = co **nhieu hon** tran ⇒ man hinh phai **noi ro** la dang xem mot phan.</summary>
    public bool BiCat { get; set; }
}
