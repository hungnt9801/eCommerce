using System.Reflection;
using eCommerce.Customers;
using Volo.Abp;

namespace eCommerce.Tests.SoDiaChi
{
    /// <summary>
    /// 🚨 **SỔ ĐỊA CHỈ CỦA KHÁCH** (`ADDR-004` phần B) — các bất biến **append-only** và *"một mặc định"*.
    ///
    /// 📌 **VÌ SAO TEST NÀY KHÔNG Ở NAMESPACE `.Address`:** đã trả giá ở `S1` (`ADDR-004` **F11**) —
    /// namespace kết thúc bằng `.Address` làm `Address` (type) bị hiểu là **namespace** ⇒ `CS0118`,
    /// và lỗi biên dịch nổ ở **file không liên quan**. Ở đây dùng `eCommerce.Tests.SoDiaChi`.
    ///
    /// 🔒 **LUẬT KIỂM:**
    ///   (1) `AddressId`/`AddressType` **KHÔNG** sửa được — cố sửa ⇒ **NÉM `0013`** (`BR-008`);
    ///   (2) liên kết đã gỡ **KHÔNG** phục hồi được ⇒ **NÉM `0014`** (nếu cho phục hồi, cờ mặc định cũ
    ///       "sống lại" và đụng partial unique index theo cách không đoán trước);
    ///   (3) `DatMacDinh` **idempotent** (retry/double-click không được làm khách thấy lỗi);
    ///   (4) nhãn rỗng ⇒ `null`; nhãn quá dài ⇒ **NÉM** (DB cũng có CHECK — hai lớp);
    ///   (5) 🔑 **KHÔNG property nội dung địa chỉ nào có setter công khai** — nếu có, module khác sửa được
    ///       địa chỉ của **khách khác** (địa chỉ dùng chung trong tenant — `ADR-03`).
    /// </summary>
    public class CustomerAddressLinkTests
    {
        private static CustomerAddressLink Tao(
            string? nhan = "Nhà riêng",
            bool laMacDinh = false,
            LoaiDiaChi loai = LoaiDiaChi.Shipping)
            => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), loai, nhan, laMacDinh);

        // ── (1) APPEND-ONLY ───────────────────────────────────────────────────────────────────────

        [Fact]
        public void DoiDiaChi__luon_NEM_0013()
        {
            var lk = Tao();

            var loi = Assert.Throws<BusinessException>(() => lk.DoiDiaChi(Guid.NewGuid()));

            Assert.Equal(CustomerErrorCodes.KhongSuaDuocLienKet, loi.Code);
        }

        [Fact]
        public void AddressId_va_AddressType_KHONG_co_setter_cong_khai()
        {
            // 🚨 Reflection: nếu ai đó "tiện tay" mở setter thì bất biến append-only **mất** mà không có
            // lỗi biên dịch nào. 📌 Đây là lớp thứ hai sau `DoiDiaChi` (hai thứ không tương đương).
            foreach (var ten in new[] { nameof(CustomerAddressLink.AddressId), nameof(CustomerAddressLink.AddressType) })
            {
                var prop = typeof(CustomerAddressLink).GetProperty(ten)!;

                Assert.NotEqual(true, prop.SetMethod?.IsPublic);
            }
        }

        // ── (2) CẤM PHỤC HỒI ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void PhucHoi__luon_NEM_0014()
        {
            var lk = Tao();

            var loi = Assert.Throws<BusinessException>(() => lk.PhucHoi());

            Assert.Equal(CustomerErrorCodes.KhongPhucHoiDuocLienKet, loi.Code);
        }

        // ── (3) MẶC ĐỊNH ──────────────────────────────────────────────────────────────────────────

        [Fact]
        public void DatMacDinh__lan_hai_khong_NEM_va_van_mac_dinh()
        {
            // 📌 Idempotent: retry / double-click KHÔNG được là lỗi với khách.
            var lk = Tao();

            lk.DatMacDinh();
            lk.DatMacDinh();

            Assert.True(lk.IsDefault);
        }

        [Fact]
        public void BoMacDinh__sau_do_IsDefault_false()
        {
            var lk = Tao(laMacDinh: true);

            lk.BoMacDinh();

            Assert.False(lk.IsDefault);
        }

        [Fact]
        public void Mac_dinh_tinh_THEO_TUNG_LOAI__hai_loai_doc_lap()
        {
            // 🔑 Bất biến là *"một mặc định / (khách, loại)"* — KHÔNG phải *"một mặc định / khách"*.
            var khachId = Guid.NewGuid();
            var diaChiId = Guid.NewGuid();

            var giaoHang = new CustomerAddressLink(Guid.NewGuid(), khachId, diaChiId, LoaiDiaChi.Shipping, null, true);
            var hoaDon = new CustomerAddressLink(Guid.NewGuid(), khachId, diaChiId, LoaiDiaChi.Billing, null, true);

            Assert.True(giaoHang.IsDefault);
            Assert.True(hoaDon.IsDefault);
            Assert.NotEqual(giaoHang.AddressType, hoaDon.AddressType);
        }

        // ── (4) NHÃN ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Nhan_rong__thanh_null()
        {
            Assert.Null(Tao(nhan: "   ").Label);
        }

        [Fact]
        public void Nhan_qua_dai__NEM()
        {
            var quaDai = new string('a', CustomerAddressLink.DoDaiNhanToiDa + 1);

            var loi = Assert.Throws<BusinessException>(() => Tao(nhan: quaDai));

            Assert.Equal(CustomerErrorCodes.ThieuThongTinBatBuoc, loi.Code);
        }

        [Fact]
        public void Id_rong__NEM()
        {
            var loi = Assert.Throws<BusinessException>(() => new CustomerAddressLink(
                Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), LoaiDiaChi.Shipping));

            Assert.Equal(CustomerErrorCodes.ThieuThongTinBatBuoc, loi.Code);
        }

        // ── (5) KHÔNG NỘI DUNG ĐỊA CHỈ Ở ENTITY NÀY ───────────────────────────────────────────────

        [Fact]
        public void Lien_ket_KHONG_co_cot_noi_dung_dia_chi()
        {
            // 🚨 `ADR-01`: nội dung địa chỉ thuộc `Address`. Một cột như `SoNhaDuong` ở đây là **mở lại**
            // đúng anti-pattern "hai nguồn sự thật về địa chỉ" mà cả dự án đã loại bỏ.
            var cam = new[] { "SoNhaDuong", "DiaChiDayDu", "Street", "Ward", "District", "Province", "FullAddress" };

            var co = typeof(CustomerAddressLink)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name)
                .Where(ten => cam.Contains(ten, StringComparer.OrdinalIgnoreCase))
                .ToList();

            Assert.Empty(co);
        }
    }
}
