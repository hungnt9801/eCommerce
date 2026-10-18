using System.Reflection;
using eCommerce.Address.Events;

namespace eCommerce.Tests.SoDiaChi
{
    /// <summary>
    /// 🚨 **`TC-18` — ETO KHÔNG ĐƯỢC CHỨA CHỮ ĐỊA CHỈ** (`ADDR-004` §7.1, `XC-002` **D11**).
    ///
    /// **VÌ SAO ĐÂY LÀ BẤT BIẾN (không phải "cho gọn"):** PII trong event bus là PII **khó xoá nhất** —
    /// đã fan-out tới nhiều consumer, và `MsgIdempotencyRecords` **giữ payload hash** ⇒ muốn xoá phải xoá
    /// cả bản ghi đó. ➕ consumer nhận **text địa chỉ** sẽ dùng text đó thay vì `AddressId` ⇒ sinh **bản
    /// sao thứ hai** của địa chỉ mà **không ai quản lý** (`ADR-10`).
    ///
    /// 📌 Test này kiểm **cả hai chiều**: ① tên property **không** gợi PII; ② **giá trị** chuỗi lúc chạy
    /// không chứa chữ địa chỉ — vì một trường tên vô hại (`GhiChu`) vẫn có thể chứa PII nếu ai đó gán vào.
    /// </summary>
    public class AddressEtoKhongChuaPiiTests
    {
        private static readonly string[] TenCam =
        [
            // ⚠️ CỐ Ý **KHÔNG** có `Address`/`DiaChi` trần: `AddressId`/`AddressType` là trường **bắt buộc**
            // (đó là **định danh**, không phải nội dung). 📌 Một bộ lọc quá rộng sẽ **chặn việc đúng** — và
            // rồi người ta gỡ nó ra thay vì sửa nó (bài học `ham-tiem` §"phép kiểm trông có vẻ chặt").
            "SoNhaDuong", "DiaChiDayDu", "Street", "FullAddress",
            "HoTen", "TenKhach", "TenNguoiNhan", "Phone", "DienThoai", "Email"
        ];

        private static AddressChangedEto TaoDaDoi() => new()
        {
            AddressId = Guid.NewGuid(),
            AggregateId = Guid.NewGuid(),
            AggregateVersion = "v1",
            TrangThai = "DaChuanHoa",
            SchemaVersion = 2,
            TinhCode = "01",
            XaCode = "00004",
            TenantId = Guid.NewGuid(),
            CorrelationId = "corr-1"
        };

        private static CustomerDefaultAddressChangedEto TaoMacDinh() => new()
        {
            CustomerId = Guid.NewGuid(),
            AddressType = "Shipping",
            AddressId = Guid.NewGuid(),
            LinkId = Guid.NewGuid(),
            AggregateId = Guid.NewGuid(),
            AggregateVersion = "v2",
            LaMacDinh = true,
            TenantId = Guid.NewGuid()
        };

        [Fact]
        public void AddressChangedEto_KHONG_co_property_mang_chu_dia_chi()
        {
            var co = typeof(AddressChangedEto)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name)
                .Where(ten => TenCam.Any(cam => ten.Contains(cam, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            Assert.Empty(co);
        }

        [Fact]
        public void CustomerDefaultAddressChangedEto_KHONG_co_property_mang_chu_dia_chi()
        {
            var co = typeof(CustomerDefaultAddressChangedEto)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name)
                .Where(ten => TenCam.Any(cam => ten.Contains(cam, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            Assert.Empty(co);
        }

        [Fact]
        public void Gia_tri_chuoi_luc_chay_KHONG_chua_chu_dia_chi()
        {
            // 🔒 Quét **mọi** property kiểu chuỗi của cả hai ETO. 📌 Nếu ai đó thêm `DiaChiDayDu` (hoặc nhét
            // text vào `CorrelationId`) thì test đỏ **ngay**, không phải chờ tới khi PII đã fan-out.
            var cam = new[] { "Lê Lợi", "Phường", "Tỉnh", "Hà Nội" };

            foreach (var eto in new object[] { TaoDaDoi(), TaoMacDinh() })
            {
                foreach (var prop in eto.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                             .Where(p => p.PropertyType == typeof(string)))
                {
                    var giaTri = (string?)prop.GetValue(eto);

                    Assert.False(
                        cam.Any(c => giaTri?.Contains(c, StringComparison.OrdinalIgnoreCase) == true),
                        $"{eto.GetType().Name}.{prop.Name} chứa chữ địa chỉ — vi phạm XC-002 D11.");
                }
            }
        }

        [Fact]
        public void ETO_mac_dinh_mang_du_du_lieu_de_consumer_dung_lai()
        {
            // 🔑 Chiều ngược lại: ETO **phải đủ** cho consumer (id + loại + cờ) — nếu thiếu, consumer buộc
            // phải đoán hoặc gọi chéo module, đúng thứ `ADR-01` ngăn.
            var eto = TaoMacDinh();

            Assert.NotEqual(Guid.Empty, eto.CustomerId);
            Assert.NotEqual(Guid.Empty, eto.AddressId);
            Assert.NotEqual(Guid.Empty, eto.LinkId);
            Assert.False(string.IsNullOrWhiteSpace(eto.AddressType));
            Assert.True(eto.LaMacDinh);
        }
    }
}
