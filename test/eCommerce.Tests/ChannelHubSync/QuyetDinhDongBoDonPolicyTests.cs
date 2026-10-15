using eCommerce.ChannelHub.Sync;

namespace eCommerce.Tests.ChannelHubSync;

/// <summary>
/// Test cho **lõi thuần quyết định số phận một lô đơn kéo từ sàn** — `<see cref="QuyetDinhDongBoDonPolicy"/>`.
///
/// 🚨 ĐÂY LÀ TEST QUAN TRỌNG NHẤT CỦA ĐỢT SỬA LỖI #22.
/// Lỗi #22 (mức nghiêm trọng nhất dự án): `ChannelShopSyncer` publish `ChannelOrderPulledEto` cho từng
/// đơn nhưng **không ai nhận**, rồi vẫn **tiến con trỏ đồng bộ** ⇒ sàn không gửi lại ⇒ **đơn mất vĩnh
/// viễn** mà **không có lỗi nào được ghi**. Bất biến cần khoá là:
///
///   **CÓ ĐƠN mà CHƯA CÓ NƠI NHẬN ⇒ TUYỆT ĐỐI KHÔNG được tiến con trỏ.**
///
/// Test này chạy được **mọi nhánh** (kể cả nhánh không có nơi nhận) vì quy tắc đã được tách khỏi lớp
/// đầy phụ thuộc I/O — đó chính là lý do tách nó ra.
/// </summary>
public class QuyetDinhDongBoDonPolicyTests
{
    [Fact]
    public void Co_don_va_CHUA_co_noi_nhan_thi_CHAN_ban_giao_va_KHONG_tien_con_tro()
    {
        // 🚨 Bất biến sống còn: tiến con trỏ ở đây = mất đơn VĨNH VIỄN.
        var qd = QuyetDinhDongBoDonPolicy.Tinh(soDon: 3, coNoiNhanDon: false);

        Assert.True(qd.BiChan);
        Assert.False(qd.ChoPhepPhatSuKien);
        Assert.False(qd.ChoPhepTienConTro);
        Assert.Equal(LyDoChanDongBoDon.ChuaCoNoiNhanDon, qd.LyDo);
    }

    [Fact]
    public void Co_don_va_CO_noi_nhan_thi_cho_phep_ban_giao_va_tien_con_tro()
    {
        var qd = QuyetDinhDongBoDonPolicy.Tinh(soDon: 3, coNoiNhanDon: true);

        Assert.False(qd.BiChan);
        Assert.True(qd.ChoPhepPhatSuKien);
        Assert.True(qd.ChoPhepTienConTro);
        Assert.Equal(LyDoChanDongBoDon.KhongChan, qd.LyDo);
    }

    [Fact]
    public void Lo_RONG_va_chua_co_noi_nhan_thi_VAN_tien_con_tro()
    {
        // Lô rỗng = KHÔNG có gì để mất ⇒ không được coi là "chưa có nơi nhận".
        // Nếu chặn ở đây, con trỏ kẹt vĩnh viễn và mọi nhịp sau quét lại cùng một khoảng rỗng:
        // log đầy mà KHÔNG bảo vệ thêm được gì (đúng ca "lặp vô hạn" cần tránh).
        var qd = QuyetDinhDongBoDonPolicy.Tinh(soDon: 0, coNoiNhanDon: false);

        Assert.False(qd.BiChan);
        Assert.False(qd.ChoPhepPhatSuKien);   // không có đơn nào thì không có gì để publish
        Assert.True(qd.ChoPhepTienConTro);
    }

    [Fact]
    public void Lo_RONG_va_CO_noi_nhan_cung_tien_con_tro()
    {
        var qd = QuyetDinhDongBoDonPolicy.Tinh(soDon: 0, coNoiNhanDon: true);

        Assert.False(qd.BiChan);
        Assert.True(qd.ChoPhepTienConTro);
    }

    [Fact]
    public void So_don_AM_duoc_coi_nhu_lo_rong()
    {
        var qd = QuyetDinhDongBoDonPolicy.Tinh(soDon: -5, coNoiNhanDon: false);

        Assert.False(qd.BiChan);
        Assert.True(qd.ChoPhepTienConTro);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(50)]
    [InlineData(int.MaxValue)]
    public void BAT_BIEN_voi_MOI_so_don_duong_va_chua_co_noi_nhan_thi_KHONG_bao_gio_tien_con_tro(int soDon)
    {
        // Quét cả miền thay vì chỉ một giá trị: nếu ai đó thêm một nhánh "ngoại lệ" (ví dụ bỏ qua khi lô
        // nhỏ, hoặc khi quá lớn), test này đỏ ngay.
        var qd = QuyetDinhDongBoDonPolicy.Tinh(soDon, coNoiNhanDon: false);

        Assert.False(qd.ChoPhepTienConTro);
        Assert.False(qd.ChoPhepPhatSuKien);
    }

    [Fact]
    public void BAT_BIEN_Bi_chan_thi_KHONG_bao_gio_duoc_tien_con_tro()
    {
        // Tổ hợp nguy hiểm & vô nghĩa: "chặn bàn giao NHƯNG vẫn tiến con trỏ" — chính là lỗi #22.
        foreach (var soDon in new[] { 0, 1, 7 })
        {
            foreach (var coNoiNhan in new[] { true, false })
            {
                var qd = QuyetDinhDongBoDonPolicy.Tinh(soDon, coNoiNhan);

                Assert.False(
                    qd.BiChan && qd.ChoPhepTienConTro,
                    $"Lô bị chặn mà vẫn tiến con trỏ ⇒ MẤT ĐƠN: soDon={soDon}, coNoiNhan={coNoiNhan}");
            }
        }
    }
}
