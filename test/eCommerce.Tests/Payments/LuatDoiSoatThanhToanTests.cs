using eCommerce.PaymentHub.Payments;

namespace eCommerce.Tests.Payments;

/// <summary>
/// 🚨 **LUẬT ĐỐI SOÁT `querydr`** (`REC-001` mục 7) — hàm **THUẦN** ở tầng Domain.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🔑 **VÌ SAO LUẬT NÀY Ở TẦNG DOMAIN, VÀ VÌ SAO ĐIỀU ĐÓ QUAN TRỌNG HƠN VẺ NGOÀI:**
///
/// Luật từng **chỉ** nằm trong `PaymentAppService`. ⚠️ Nhưng lớp đó mang **`[Authorize]` ở cấp class**
/// (quy ước #4 **bắt buộc**) ⇒ 🔒 **job nền KHÔNG gọi được nó**: interceptor của ABP kiểm quyền **ngay cả khi
/// gọi trong tiến trình**, mà worker **không có principal** ⇒ **`AbpAuthorizationException`**.
/// 📌 **Lỗi này ĐÃ XẢY RA THẬT** ở `SAL-005` — job **chết giữa đường**, và tệ hơn: các ca kiểm
/// *"job không ghi gì"* **vẫn XANH** vì job **chết**, không phải vì job **cố ý** không ghi.
///
/// ⚠️ **HAI LỐI SAI:** (a) **chép** luật sang worker ⇒ luật có **HAI** định nghĩa, chúng sẽ **lệch** khi ai đó
/// sửa một chỗ; (b) cho worker gọi app service ⇒ `AbpAuthorizationException`.
/// ✅ **LỐI ĐÚNG:** luật là **hàm thuần tầng Domain** ⇒ **cả hai** chỗ gọi **cùng một** định nghĩa.
///
/// 🔒 **Và file này khoá định nghĩa đó lại.** 📌 Cùng khuôn đã trả giá ở `CAT-003` (`SinhSlug`),
/// `SAL-005` (`LuatWatermark`), `INV-003` (`ChonHanMuc`).
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// </summary>
public class LuatDoiSoatThanhToanTests
{
    // ── 🚨 BẤT BIẾN TRUNG TÂM: TRẠNG THÁI CUỐI THÌ KHÔNG ĐỤNG ────────────────────────────────

    [Theory]
    [InlineData(PaymentTransactionStatus.Succeeded)]
    [InlineData(PaymentTransactionStatus.PartiallyRefunded)]
    [InlineData(PaymentTransactionStatus.Refunded)]
    public void DaKetThuc__dung_cho_BA_trang_thai_CUOI(PaymentTransactionStatus tt)
    {
        // 🔑 `DaKetThuc` phải khớp **CHÍNH XÁC** danh sách mà `PaymentTransaction.MarkSucceeded`/`MarkFailed`
        // dùng. ⚠️ Nếu lệch thì sinh ca **vô nghĩa**: hàm quyết định nói *"cứ ghi"*, rồi entity **im lặng**
        // `return` ⇒ 📌 chỗ gọi **đếm** là *"đã đổi trạng thái"* trong khi **DB không đổi** ⇒ báo cáo SAI.
        Assert.True(LuatDoiSoatThanhToan.DaKetThuc(tt));
    }

    [Theory]
    [InlineData(PaymentTransactionStatus.Initiated)]
    [InlineData(PaymentTransactionStatus.Pending)]
    [InlineData(PaymentTransactionStatus.Failed)]
    [InlineData(PaymentTransactionStatus.Cancelled)]
    public void DaKetThuc__KHONG_dung_cho_trang_thai_chua_cuoi(PaymentTransactionStatus tt)
    {
        // ⚠️ **`Failed` KHÔNG phải trạng thái cuối — CỐ Ý.** 🔒 Một giao dịch `Failed` **vẫn được** đối soát
        // lại: IPN có thể đến muộn, hoặc lần trước cổng trả lỗi tạm thời ⇒ cần cơ hội **sửa thành `Succeeded`**
        // khi cổng xác nhận tiền **đã** vào. 📌 Đây chính là **lưới an toàn cho IPN mất**.
        Assert.False(LuatDoiSoatThanhToan.DaKetThuc(tt));
    }

    [Theory]
    [InlineData(PaymentTransactionStatus.Succeeded)]
    [InlineData(PaymentTransactionStatus.PartiallyRefunded)]
    [InlineData(PaymentTransactionStatus.Refunded)]
    public void Trang_thai_CUOI__KHONG_DOI__du_cong_noi_gi(PaymentTransactionStatus hienTai)
    {
        // 🚨 **CA QUAN TRỌNG NHẤT.** Với **mọi** câu trả lời của cổng, giao dịch đã ở trạng thái cuối
        // ⇒ **KHÔNG ĐỤNG**. 🔒 Lý do: ghi `Failed` lên một giao dịch **đã hoàn tiền** sẽ **xoá dấu vết dòng
        // tiền** (`RefundedAmount` vẫn còn nhưng `Status` nói *"chưa từng thu"*) ⇒ **kế toán không đối chiếu được**.
        foreach (var tuCong in Enum.GetValues<PaymentTransactionStatus>())
        {
            var q = LuatDoiSoatThanhToan.Quyet(hienTai, tuCong);

            Assert.True(q == LuatDoiSoatThanhToan.QuyetDinh.KhongDoi,
                $"Trạng thái cuối {hienTai} mà cổng nói {tuCong} lại sinh quyết định {q} — PHẢI là KhongDoi.");
        }
    }

    // ── 🔑 LƯỚI AN TOÀN CHO IPN MẤT: `Failed` + cổng nói ĐÃ THU ⇒ GHI ĐÃ THU ──────────────────

    [Fact]
    public void Failed__cong_noi_Succeeded__GHI_DA_THU__khong_bi_chan_boi_trang_thai_cu()
    {
        // 🚨 **ĐÂY LÀ LÝ DO JOB TỒN TẠI.** Giao dịch đang `Failed` (ví dụ IPN đến muộn sau khi ta tự đánh
        // dấu thất bại, hoặc lần trước cổng lỗi tạm thời) — cổng nay xác nhận **tiền ĐÃ VÀO**
        // ⇒ 🔑 sổ **phải** nói **đã thu**. ⚠️ Nếu nhánh này bị chặn thì **tiền vào mà sổ nói chưa thu**.
        var q = LuatDoiSoatThanhToan.Quyet(PaymentTransactionStatus.Failed, PaymentTransactionStatus.Succeeded);

        Assert.Equal(LuatDoiSoatThanhToan.QuyetDinh.GhiDaThuTien, q);
    }

    [Theory]
    [InlineData(PaymentTransactionStatus.Initiated)]
    [InlineData(PaymentTransactionStatus.Pending)]
    public void Chua_co_tien__cong_noi_Succeeded__GHI_DA_THU(PaymentTransactionStatus hienTai)
    {
        var q = LuatDoiSoatThanhToan.Quyet(hienTai, PaymentTransactionStatus.Succeeded);

        Assert.Equal(LuatDoiSoatThanhToan.QuyetDinh.GhiDaThuTien, q);
    }

    // ── CỔNG NÓI KHÔNG THÀNH CÔNG ⇒ GHI THẤT BẠI ─────────────────────────────────────────────

    [Theory]
    [InlineData(PaymentTransactionStatus.Initiated)]
    [InlineData(PaymentTransactionStatus.Pending)]
    [InlineData(PaymentTransactionStatus.Failed)]
    public void Cong_noi_Failed_hoac_Cancelled__GHI_THAT_BAI(PaymentTransactionStatus hienTai)
    {
        Assert.Equal(LuatDoiSoatThanhToan.QuyetDinh.GhiThatBai,
            LuatDoiSoatThanhToan.Quyet(hienTai, PaymentTransactionStatus.Failed));

        Assert.Equal(LuatDoiSoatThanhToan.QuyetDinh.GhiThatBai,
            LuatDoiSoatThanhToan.Quyet(hienTai, PaymentTransactionStatus.Cancelled));
    }

    // ── 🔑 "CHƯA BIẾT" KHÁC "KHÔNG THÀNH CÔNG" ──────────────────────────────────────────────

    [Theory]
    [InlineData(PaymentTransactionStatus.Initiated)]
    [InlineData(PaymentTransactionStatus.Pending)]
    [InlineData(PaymentTransactionStatus.Failed)]
    public void Cong_noi_CHUA_BIET__KHONG_LUI__va_giu_nguyen(PaymentTransactionStatus hienTai)
    {
        // 🚨 **BẤT BIẾN "CHỈ TIẾN, KHÔNG LÙI".** Cổng nói `Pending`/`Initiated` ⇒ **KHÔNG** hạ trạng thái.
        // 🔒 Hạ trạng thái ở đây sẽ mở đường cho **thu lại** hoặc **huỷ đơn oan**.
        // ⚠️ Và `Initiated` (từ cổng) là *"chưa biết"*, **KHÔNG** phải *"khách chưa bắt đầu"* — 📌 cùng một
        // giá trị enum nhưng **hai** ý nghĩa tuỳ nguồn, nên luật **không** được suy diễn gì từ nó.
        Assert.Equal(LuatDoiSoatThanhToan.QuyetDinh.KhongDoi,
            LuatDoiSoatThanhToan.Quyet(hienTai, PaymentTransactionStatus.Pending));

        Assert.Equal(LuatDoiSoatThanhToan.QuyetDinh.KhongDoi,
            LuatDoiSoatThanhToan.Quyet(hienTai, PaymentTransactionStatus.Initiated));
    }

    // ── QUÉT TOÀN BỘ: KHÔNG CÓ CẶP NÀO RƠI RA NGOÀI LUẬT ────────────────────────────────────

    [Fact]
    public void MOI_cap_trang_thai__deu_co_quyet_dinh_XAC_DINH__va_khong_bao_gio_ha_trang_thai()
    {
        // 🔒 **LƯỚI BAO PHỦ:** quét **mọi** cặp `(hiện tại, từ cổng)`. 📌 Ca này bắt được hai loại lỗi mà
        // các ca cụ thể ở trên **không** bắt:
        //   (1) một giá trị enum **mới** thêm sau này rơi vào nhánh `default` **sai**;
        //   (2) 🔑 **hạ trạng thái**: từ `Succeeded` xuống `Failed`, hoặc từ `Succeeded` xuống `Pending`.
        var cacTrangThai = Enum.GetValues<PaymentTransactionStatus>();
        var ketQua = new List<string>();

        foreach (var hienTai in cacTrangThai)
        {
            foreach (var tuCong in cacTrangThai)
            {
                var q = LuatDoiSoatThanhToan.Quyet(hienTai, tuCong);

                // ① Quyết định phải là một giá trị **đã định nghĩa** (không rơi vào số lạ).
                if (!Enum.IsDefined(q))
                {
                    ketQua.Add($"({hienTai}, {tuCong}) ⇒ quyết định KHÔNG hợp lệ: {q}");
                    continue;
                }

                // ② 🔑 **KHÔNG BAO GIỜ HẠ TRẠNG THÁI:** nếu đang ở trạng thái cuối thì quyết định phải là
                //    `KhongDoi` — **bất kể** cổng nói gì.
                if (LuatDoiSoatThanhToan.DaKetThuc(hienTai) && q != LuatDoiSoatThanhToan.QuyetDinh.KhongDoi)
                {
                    ketQua.Add($"({hienTai}, {tuCong}) ⇒ HẠ TRẠNG THÁI (quyết định {q}) — luật CẤM.");
                }

                // ③ ⚠️ Và `Failed` **không** được coi là cuối (nếu không thì mất lưới an toàn IPN).
                if (hienTai == PaymentTransactionStatus.Failed
                    && tuCong == PaymentTransactionStatus.Succeeded
                    && q != LuatDoiSoatThanhToan.QuyetDinh.GhiDaThuTien)
                {
                    ketQua.Add($"({hienTai}, {tuCong}) ⇒ MẤT lưới an toàn IPN (quyết định {q}).");
                }
            }
        }

        Assert.True(ketQua.Count == 0, "Luật để lọt các cặp sau:\n  " + string.Join("\n  ", ketQua));
    }
}
