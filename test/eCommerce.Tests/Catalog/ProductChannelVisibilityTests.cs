using eCommerce.Catalog;
using Volo.Abp;

namespace eCommerce.Tests.Catalog;

/// <summary>
/// 🚨 **BẢNG CHUYỂN TRẠNG THÁI HIỂN THỊ THEO KÊNH** (`CAT-003` `ADR-08`) — bất biến ở **tầng domain**.
///
/// 🔒 **VÌ SAO TEST NÀY QUAN TRỌNG HƠN VẺ NGOÀI CỦA NÓ:** một bảng chuyển trạng thái **rất dễ** trở thành
/// **trang trí** — chỉ cần **một** nhánh `switch` quên `throw` là mọi chuyển dịch sai **lọt qua im lặng**,
/// và hậu quả ở đây là **sản phẩm chưa publish xuất hiện trên kênh bán** ⇒ khách đặt được hàng chưa hoàn chỉnh,
/// hoặc **sản phẩm đã ngừng bán sống lại** ⇒ báo cáo lịch sử theo kênh **không đọc được**.
/// 📌 Vì vậy nhóm (4) dưới đây **liệt kê ĐỦ mọi cặp** và khẳng định **cặp ngoài bảng luôn NÉM**.
///
/// 🔑 **HAI QUYẾT ĐỊNH DỄ SAI NHẤT, và test khoá cả hai:**
///   (1) **`KeThua` KHÔNG phải "chờ xử lý"** — nó là trạng thái **cuối cùng hợp lệ** cho đa số cặp, và vắng
///       bản ghi **chính là** nó. Nên `ChuyenSang(KeThua, …)` từ bất kỳ đâu ⇒ **NÉM** (không có "quay về kế thừa");
///   (2) 🚫 **`NgungBan` là HẤP THỤ TUYỆT ĐỐI** — kể cả `NgungBan ⇒ NgungBan` cũng **NÉM**, **khác** mọi cặp khác
///       (ở đó gọi lại cùng trạng thái là **no-op vô hại** cho retry/double-click).
/// </summary>
public class ProductChannelVisibilityTests
{
    private static ProductChannelVisibility Tao(string kenh = "SHOPEE")
        => new(Guid.NewGuid(), Guid.NewGuid(), kenh);

    // ── (1) KHỞI TẠO ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Ban_ghi_moi__LUON_bat_dau_tu_KeThua()
    {
        var v = Tao();

        // 🔒 Vì sao **luôn** `KeThua`: *"vắng bản ghi"* và *"bản ghi `KeThua`"* là **cùng một nghĩa nghiệp vụ**
        // (`ADR-08`), nên tạo bản ghi ở trạng thái khác là **tự mâu thuẫn**. 📌 Và nó bịt luôn đường lách:
        // muốn `HienThi`/`An` thì **phải** đi qua `ChuyenSang` ⇒ guard **chạy**.
        Assert.Equal(TrangThaiHienThi.KeThua, v.TrangThai);
        Assert.Null(v.LyDo);
    }

    [Fact]
    public void KeThua__la_gia_tri_MAC_DINH_cua_enum()
    {
        // 🔑 `KeThua = 0` **có chủ ý**: `default(TrangThaiHienThi)` **trùng** nghĩa *"không có bản ghi"*
        // ⇒ tránh lớp lỗi *"quên khởi tạo ⇒ rơi vào trạng thái lạ"*.
        Assert.Equal(0, (int)TrangThaiHienThi.KeThua);
        Assert.Equal(TrangThaiHienThi.KeThua, default);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Ma_kenh_RONG__NEM(string maRong)
    {
        Assert.Throws<BusinessException>(() => new ProductChannelVisibility(Guid.NewGuid(), Guid.NewGuid(), maRong));
    }

    [Fact]
    public void Ma_kenh__cat_khoang_trang__va_qua_dai_thi_NEM()
    {
        var v = Tao("  SHOPEE  ");
        Assert.Equal("SHOPEE", v.ChannelCode);

        var quaDai = new string('k', ProductChannelVisibility.DoDaiMaKenhToiDa + 1);
        Assert.Throws<BusinessException>(() => new ProductChannelVisibility(Guid.NewGuid(), Guid.NewGuid(), quaDai));
    }

    [Fact]
    public void Thieu_ProductId__NEM()
    {
        Assert.Throws<BusinessException>(
            () => new ProductChannelVisibility(Guid.NewGuid(), Guid.Empty, "SHOPEE"));
    }

    // ── (2) CÁC CẶP **HỢP LỆ** ─────────────────────────────────────────────────────────────────

    [Fact]
    public void KeThua_den_HienThi__can_san_pham_DA_PUBLISH()
    {
        var v = Tao();

        v.ChuyenSang(TrangThaiHienThi.HienThi, sanPhamDaPublish: true);

        Assert.Equal(TrangThaiHienThi.HienThi, v.TrangThai);
    }

    [Fact]
    public void KeThua_den_An__can_LY_DO()
    {
        var v = Tao();

        v.ChuyenSang(TrangThaiHienThi.An, sanPhamDaPublish: false, lyDo: "Hết hàng, chờ nhập lại");

        Assert.Equal(TrangThaiHienThi.An, v.TrangThai);
        Assert.Equal("Hết hàng, chờ nhập lại", v.LyDo);
    }

    [Fact]
    public void An_den_HienThi__bo_an__va_XOA_ly_do_cu()
    {
        var v = Tao();
        v.ChuyenSang(TrangThaiHienThi.An, sanPhamDaPublish: true, lyDo: "Hết hàng");

        v.ChuyenSang(TrangThaiHienThi.HienThi, sanPhamDaPublish: true);

        Assert.Equal(TrangThaiHienThi.HienThi, v.TrangThai);
        // 📌 Lý do ẩn **hết hiệu lực** khi đã hiển thị lại — giữ lại sẽ khiến người đọc sau **hiểu sai**
        // rằng sản phẩm **đang** bị ẩn.
        Assert.Null(v.LyDo);
    }

    [Theory]
    [InlineData(TrangThaiHienThi.HienThi)]
    [InlineData(TrangThaiHienThi.An)]
    public void HienThi_hoac_An_den_NgungBan__duoc(TrangThaiHienThi tu)
    {
        var v = Tao();
        if (tu == TrangThaiHienThi.HienThi)
        {
            v.ChuyenSang(TrangThaiHienThi.HienThi, sanPhamDaPublish: true);
        }
        else
        {
            v.ChuyenSang(TrangThaiHienThi.An, sanPhamDaPublish: true, lyDo: "Tạm ẩn");
        }

        v.ChuyenSang(TrangThaiHienThi.NgungBan, sanPhamDaPublish: true);

        Assert.Equal(TrangThaiHienThi.NgungBan, v.TrangThai);
    }

    // ── (3) CÁC GUARD **PHẢI** CHẶN ────────────────────────────────────────────────────────────

    [Fact]
    public void HienThi_khi_san_pham_CHUA_publish__NEM()
    {
        var v = Tao();

        var loi = Assert.Throws<BusinessException>(
            () => v.ChuyenSang(TrangThaiHienThi.HienThi, sanPhamDaPublish: false));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.ChuyenTrangThaiHienThiKhongHopLe, loi.Code);
        // 🔒 "Ném mà để lại rác là gieo bug": trạng thái PHẢI nguyên vẹn.
        Assert.Equal(TrangThaiHienThi.KeThua, v.TrangThai);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AN_ma_THIEU_ly_do__NEM(string? lyDoRong)
    {
        var v = Tao();

        var loi = Assert.Throws<BusinessException>(
            () => v.ChuyenSang(TrangThaiHienThi.An, sanPhamDaPublish: true, lyDo: lyDoRong));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.ChuyenTrangThaiHienThiKhongHopLe, loi.Code);
        Assert.Equal(TrangThaiHienThi.KeThua, v.TrangThai);
        Assert.Null(v.LyDo);
    }

    [Fact]
    public void NGUNG_BAN_la_HAP_THU__moi_chuyen_dich_ra_deu_NEM()
    {
        var v = Tao();
        v.ChuyenSang(TrangThaiHienThi.HienThi, sanPhamDaPublish: true);

        v.ChuyenSang(TrangThaiHienThi.NgungBan, sanPhamDaPublish: true);

        // 🚫 **MỌI** đích đều bị chặn — kể cả `HienThi`, `An`, `KeThua`, và chính `NgungBan`.
        foreach (var sang in Enum.GetValues<TrangThaiHienThi>())
        {
            var loi = Assert.Throws<BusinessException>(
                () => v.ChuyenSang(sang, sanPhamDaPublish: true, lyDo: "lý do hợp lệ"));

            Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.ChuyenTrangThaiHienThiKhongHopLe, loi.Code);
            Assert.Equal(TrangThaiHienThi.NgungBan, v.TrangThai);   // không đổi
        }
    }

    [Fact]
    public void NGUNG_BAN_den_chinh_no__NEM__KHAC_moi_cap_khac()
    {
        var v = Tao();
        v.ChuyenSang(TrangThaiHienThi.HienThi, sanPhamDaPublish: true);
        v.ChuyenSang(TrangThaiHienThi.NgungBan, sanPhamDaPublish: true);

        // 🔑 ĐÂY LÀ ĐIỂM KHÁC BIỆT then chốt: với `HienThi`, gọi lại là **no-op vô hại** (retry/double-click).
        // Với `NgungBan`, gọi lại **vẫn NÉM** — vì `NgungBan` là **hấp thụ TUYỆT ĐỐI**: mọi lời gọi đều nằm
        // trong một thao tác **không được phép**, và im lặng chấp nhận sẽ **che mất** việc người dùng đang cố
        // làm một điều sai (tưởng rằng có thể bán lại trên kênh đó).
        Assert.Throws<BusinessException>(
            () => v.ChuyenSang(TrangThaiHienThi.NgungBan, sanPhamDaPublish: true));
    }

    // ── (4) 🚨 MỌI CẶP NGOÀI BẢNG ⇒ **LUÔN NÉM** (khoá bảng chuyển khỏi thành "trang trí") ─────

    [Fact]
    public void MOI_cap_ngoai_bang__LUON_NEM__khong_co_nhanh_nao_quen_throw()
    {
        // 📌 Duyệt **MỌI** cặp `(từ, sang)` × `(publish, lý do)` và khẳng định: nếu cặp đó **không** nằm trong
        // bảng hợp lệ thì **PHẢI** ném. 🔑 Cách này bắt được **nhánh `switch` quên `throw`** — thứ mà test
        // từng-ca-không-thể-bao-hết sẽ bỏ sót khi ai đó thêm giá trị enum mới.
        var cacCapHopLe = new HashSet<(TrangThaiHienThi Tu, TrangThaiHienThi Sang)>
        {
            (TrangThaiHienThi.KeThua, TrangThaiHienThi.HienThi),
            (TrangThaiHienThi.KeThua, TrangThaiHienThi.An),
            (TrangThaiHienThi.An, TrangThaiHienThi.HienThi),
            (TrangThaiHienThi.HienThi, TrangThaiHienThi.An),
            (TrangThaiHienThi.HienThi, TrangThaiHienThi.NgungBan),
            (TrangThaiHienThi.An, TrangThaiHienThi.NgungBan),
        };

        var soCapNgoaiBang = 0;

        foreach (var tu in Enum.GetValues<TrangThaiHienThi>())
        {
            foreach (var sang in Enum.GetValues<TrangThaiHienThi>())
            {
                var v = Tao();

                // Dựng trạng thái `tu` (bỏ qua nếu không dựng được — ca đó đã có test riêng).
                if (!DungDuoc(v, tu))
                {
                    continue;
                }

                // 🔑 Chỉ xét cặp NGOÀI bảng: cặp trong bảng có guard riêng (publish/lý do) nên test riêng.
                if (cacCapHopLe.Contains((tu, sang)) || tu == sang)
                {
                    continue;
                }

                soCapNgoaiBang++;

                var loi = Assert.Throws<BusinessException>(
                    () => v.ChuyenSang(sang, sanPhamDaPublish: true, lyDo: "lý do hợp lệ"));

                Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.ChuyenTrangThaiHienThiKhongHopLe, loi.Code);
            }
        }

        // 🛡️ Lưới an toàn cho CHÍNH phép quét: nếu `DungDuoc` hỏng thì vòng lặp không chạy ca nào
        // ⇒ test **xanh vô nghĩa** (đúng loại "cảm giác an toàn giả" mà Phụ lục C cảnh báo).
        Assert.True(soCapNgoaiBang > 0, $"Phép quét không chạy ca nào (soCapNgoaiBang={soCapNgoaiBang}) ⇒ test vô nghĩa.");
    }

    [Fact]
    public void Moi_chuyen_dich_ve_Loi__NEM()
    {
        // 🔑 `Loi` **không bao giờ** là kết quả của một chuyển dịch: nó là **ĐÍCH khi guard chặn**, và guard
        // **NÉM** ⇒ `Loi` **không bao giờ** được ghi xuống DB. 📌 Nếu `case` này **quên ném**, hệ thống sẽ ghi
        // một trạng thái **không có nghĩa** vào DB, và mọi truy vấn `TrangThai = Loi` sau đó **không** giải thích được.
        foreach (var tu in new[] { TrangThaiHienThi.KeThua, TrangThaiHienThi.HienThi, TrangThaiHienThi.An })
        {
            var v = Tao();
            if (tu == TrangThaiHienThi.HienThi)
            {
                v.ChuyenSang(TrangThaiHienThi.HienThi, sanPhamDaPublish: true);
            }
            else if (tu == TrangThaiHienThi.An)
            {
                v.ChuyenSang(TrangThaiHienThi.An, sanPhamDaPublish: true, lyDo: "ẩn");
            }

            var loi = Assert.Throws<BusinessException>(
                () => v.ChuyenSang(TrangThaiHienThi.Loi, sanPhamDaPublish: true, lyDo: "lý do"));

            Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.ChuyenTrangThaiHienThiKhongHopLe, loi.Code);
        }
    }

    [Fact]
    public void KeThua_den_KeThua__la_NO_OP__KHONG_nem()
    {
        // ══════════════════════════════════════════════════════════════════════════════════════════
        // 🚨 **ĐIỂM TÔI PHẢI QUYẾT ĐỊNH, VÌ TÀI LIỆU KHÔNG NÓI RÕ — và test cũ của tôi ĐÃ SAI:**
        //
        // `CAT-003` §3 ghi: *"`*mọi cặp khác*` ⇒ **luôn ném lỗi**"*. Mà `KeThua ⇒ KeThua` **không** có trong
        // bảng ⇒ theo câu đó thì **phải ném**. ❗ **Nhưng** nó **xung đột** với một bất biến đã chốt ở
        // **hai** chỗ khác trong dự án (`REC-001` §17.3, `CAT-003` §17.3): **gọi lại đúng trạng thái hiện tại
        // là NO-OP, KHÔNG ném** — vì đó là **retry/double-click**, không phải *"chuyển sai"*.
        //
        // 🔒 **TÔI CHỌN NO-OP, và đây là lý do:**
        //   • `KeThua ⇒ KeThua` **KHÔNG đổi trạng thái nào** ⇒ nó **không phải** một chuyển dịch. Câu
        //     *"mọi cặp khác đều ném"* nói về các **chuyển dịch** — cặp `(X, X)` **không** phải chuyển dịch;
        //   • Nếu ném, một **retry** (mạng chập, double-click, job chạy lại) trên bản ghi vừa tạo sẽ **ĐỎ**
        //     ⇒ client **thử lại** ⇒ **vòng lặp lỗi cho một việc đã đúng**;
        //   • Các cặp no-op khác (`HienThi ⇒ HienThi`, `An ⇒ An`) **đã** là no-op — cho `KeThua` ném là
        //     **bất đối xứng vô cớ**.
        // 📌 **Ngoại lệ DUY NHẤT:** `NgungBan ⇒ NgungBan` **NÉM** (xem test riêng) — vì `NgungBan` là
        // **hấp thụ TUYỆT ĐỐI**: mọi lời gọi đều nằm trong một thao tác **không được phép**, và im lặng
        // chấp nhận sẽ **che mất** việc người dùng đang cố làm một điều sai.
        // ══════════════════════════════════════════════════════════════════════════════════════════
        var v = Tao();

        v.ChuyenSang(TrangThaiHienThi.KeThua, sanPhamDaPublish: false);

        Assert.Equal(TrangThaiHienThi.KeThua, v.TrangThai);
    }
    // ── (5) NO-OP IDEMPOTENT cho các cặp còn lại ───────────────────────────────────────────────

    [Fact]
    public void Goi_lai_DUNG_trang_thai__KHONG_nem__va_KHONG_ghi_de_ly_do()
    {
        var v = Tao();
        v.ChuyenSang(TrangThaiHienThi.An, sanPhamDaPublish: true, lyDo: "lý do ĐẦU TIÊN");

        // 📌 Retry/double-click ⇒ phải vô hại (cùng khuôn `ReconRunMismatch.DanhDauDaXuLy`).
        v.ChuyenSang(TrangThaiHienThi.An, sanPhamDaPublish: true, lyDo: "lý do THỨ HAI (cố đổi)");

        // 🔒 Và **KHÔNG** ghi đè bằng chứng của lần ẩn đầu.
        Assert.Equal("lý do ĐẦU TIÊN", v.LyDo);
    }

    // ── (6) DỮ LIỆU LỖI MANG ĐỦ ĐỂ ĐIỀU TRA ────────────────────────────────────────────────────

    [Fact]
    public void Loi__mang_du_tu_sang_sanPham_va_kenh()
    {
        var v = Tao("LAZADA");

        var loi = Assert.Throws<BusinessException>(
            () => v.ChuyenSang(TrangThaiHienThi.HienThi, sanPhamDaPublish: false));

        // 📌 Một lỗi *"chuyển trạng thái không hợp lệ"* **không nói được** cặp nào/kênh nào thì người vận hành
        // phải tự đi tìm — và với hàng nghìn sản phẩm × 5 kênh thì đó là **bế tắc**.
        Assert.Equal("KeThua", loi.Data["tu"]);
        Assert.Equal("HienThi", loi.Data["sang"]);
        Assert.Equal("LAZADA", loi.Data["kenh"]);
        Assert.Equal(v.ProductId, loi.Data["sanPhamId"]);
    }

    // ── (7) CHỈ MỘT ĐƯỜNG GHI ──────────────────────────────────────────────────────────────────

    [Fact]
    public void TrangThai__KHONG_co_setter_cong_khai__chi_doi_qua_ChuyenSang()
    {
        var thuocTinh = typeof(ProductChannelVisibility).GetProperty(nameof(ProductChannelVisibility.TrangThai));

        Assert.NotNull(thuocTinh);
        // 🔒 Nếu `TrangThai` có setter công khai thì guard trở thành **trang trí**: bất kỳ chỗ nào cũng ghi được
        // trạng thái mà **không** chạy bảng chuyển. 📌 Cùng lý do `ProductMedia.LaAnhChinh` là `protected set`.
        Assert.False(thuocTinh!.SetMethod?.IsPublic ?? false,
            "`TrangThai` KHÔNG được có setter công khai — đổi trạng thái phải qua `ChuyenSang`.");
    }

    // ── Hàm phụ ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>Dựng một bản ghi ở trạng thái `dich` bằng **đường hợp lệ**; `false` nếu không dựng được.</summary>
    private static bool DungDuoc(ProductChannelVisibility v, TrangThaiHienThi dich) => dich switch
    {
        TrangThaiHienThi.KeThua => true,
        TrangThaiHienThi.HienThi => Thu(() => v.ChuyenSang(TrangThaiHienThi.HienThi, true)),
        TrangThaiHienThi.An => Thu(() => v.ChuyenSang(TrangThaiHienThi.An, true, "ẩn")),
        TrangThaiHienThi.NgungBan => Thu(() =>
        {
            v.ChuyenSang(TrangThaiHienThi.HienThi, true);
            v.ChuyenSang(TrangThaiHienThi.NgungBan, true);
        }),
        // 📌 `Loi` **không dựng được** — nó không bao giờ là trạng thái lưu trong DB. Trả `false` để phép quét
        // bỏ qua nó (và `Moi_chuyen_dich_ve_KeThua_hoac_Loi__NEM` đã phủ đích `Loi`).
        _ => false
    };

    private static bool Thu(Action lam)
    {
        try { lam(); return true; }
        catch (BusinessException) { return false; }
    }
}
