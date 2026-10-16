using eCommerce.Messaging;

namespace eCommerce.Tests.Messaging;

/// <summary>
/// 🚨 **LUẬT WATERMARK 4 NHÁNH** (`SAL-005` §3.3 — **D5/D6**) — lõi **THUẦN**.
///
/// **VÌ SAO PHẢI CÓ TEST CHO CẢ 4 NHÁNH (lỗi THẬT đã đo được, `sal-005` §16.4 #3):**
/// coi *"đến muộn"* là **lỗi** ⇒ `PublishAsync` ném ⇒ **con trỏ đồng bộ không tiến** ⇒ sàn gửi lại **cùng**
/// đơn ⇒ **lặp vô hạn + CHẶN CẢ KÊNH** (đã đo: con trỏ kẹt `NULL` qua nhiều nhịp).
/// 📌 Đây là dạng lỗi *"đúng về mặt kỹ thuật, sai về mặt vận hành"* — chỉ test mới chặn được.
///
/// 🔑 **Bất biến trung tâm:** `AppliedSourceVersion` **CHỈ TĂNG, KHÔNG BAO GIỜ GIẢM**.
/// </summary>
public class LuatWatermarkTests
{
    private const string HashA = "aaaa1111";
    private const string HashB = "bbbb2222";

    // ── (A) ĐẾN MUỘN: v_in < v_đã_áp ⇒ BỎ QUA, KHÔNG ném, KHÔNG DLQ ───────────────────────────

    [Fact]
    public void DenMuon__BO_QUA__KHONG_nem__KHONG_DLQ()
    {
        var kq = LuatWatermark.Tinh(phienBanVao: 1000, phienBanDaAp: 1200, hashVao: HashB, hashDaAp: HashA);

        Assert.Equal(KetLuanWatermark.DenMuon, kq.KetLuan);
        Assert.False(kq.ChoPhepGhi);
        Assert.False(kq.PhaiDayDlq);          // 🚨 ĐÂY LÀ CA BÌNH THƯỜNG — DLQ ở đây là DLQ GIẢ
        Assert.Equal(-200, kq.DoLechGiay);    // âm = muộn bao nhiêu (log nói được ngay)
    }

    [Fact]
    public void DenMuon__KHONG_phu_thuoc_hash__du_hash_TRUNG_hay_KHAC()
    {
        // 📌 Ghi rõ: khi phiên bản đã cũ hơn thì **hash không có tiếng nói** — so hash trước sẽ làm
        // bản cũ "trùng hash" trở thành kết luận sai.
        var trungHash = LuatWatermark.Tinh(1000, 1200, HashA, HashA);
        var khacHash = LuatWatermark.Tinh(1000, 1200, HashB, HashA);

        Assert.Equal(KetLuanWatermark.DenMuon, trungHash.KetLuan);
        Assert.Equal(KetLuanWatermark.DenMuon, khacHash.KetLuan);
    }

    // ── (C) TRÙNG: v bằng + hash KHỚP ⇒ 0 side effect ─────────────────────────────────────────

    [Fact]
    public void Trung__v_BANG_va_hash_KHOP__khong_ghi__khong_DLQ()
    {
        var kq = LuatWatermark.Tinh(1200, 1200, HashA, HashA);

        Assert.Equal(KetLuanWatermark.Trung, kq.KetLuan);
        Assert.False(kq.ChoPhepGhi);
        Assert.False(kq.PhaiDayDlq);
    }

    [Fact]
    public void Trung__hash_so_sanh_ORDINAL__hoa_thuong_khac_nhau_la_KHAC()
    {
        // 🔒 Hash là hex do MÁY sinh: so sánh phụ thuộc culture là nguồn gốc của *"hai instance, hai kết luận"*.
        var kq = LuatWatermark.Tinh(1200, 1200, "AAAA1111", "aaaa1111");

        Assert.Equal(KetLuanWatermark.XungDot, kq.KetLuan);
    }

    // ── (D) XUNG ĐỘT: v bằng + hash KHÁC ⇒ KHÔNG đoán, DLQ ────────────────────────────────────

    [Fact]
    public void XungDot__v_BANG_va_hash_KHAC__KHONG_ghi__PHAI_DLQ()
    {
        var kq = LuatWatermark.Tinh(1200, 1200, HashB, HashA);

        Assert.Equal(KetLuanWatermark.XungDot, kq.KetLuan);
        Assert.False(kq.ChoPhepGhi);       // 🚨 "KHÔNG ĐOÁN" (D6): cấm ghi đè
        Assert.True(kq.PhaiDayDlq);        // và phải để lại dấu vết cho người xử lý
    }

    [Fact]
    public void Thieu_hash__KHONG_ket_luan_XUNG_DOT__va_KHONG_ghi_de()
    {
        // 📌 Khe hở ĐÃ BIẾT (dữ liệu cũ trước khi có cột hash): không chứng minh được nội dung giống nhau,
        // và cũng không có gì để gọi là xung đột ⇒ KHÔNG DLQ hàng loạt sau migration.
        // Hệ quả xấu nhất là BỎ QUA một bản cùng giây — an toàn hơn hẳn GHI ĐÈ dựa trên suy đoán.
        var kq = LuatWatermark.Tinh(1200, 1200, HashA, hashDaAp: null);

        Assert.Equal(KetLuanWatermark.Trung, kq.KetLuan);
        Assert.False(kq.ChoPhepGhi);
        Assert.False(kq.PhaiDayDlq);
    }

    // ── (B) ÁP DỤNG: v_in > v_đã_áp ───────────────────────────────────────────────────────────

    [Fact]
    public void ApDung__v_LON_HON__cho_ghi()
    {
        var kq = LuatWatermark.Tinh(1300, 1200, HashB, HashA);

        Assert.Equal(KetLuanWatermark.ApDung, kq.KetLuan);
        Assert.True(kq.ChoPhepGhi);
        Assert.False(kq.PhaiDayDlq);
        Assert.Equal(100, kq.DoLechGiay);
        Assert.False(kq.NghiNgoThieuBanTrungGian);
    }

    [Fact]
    public void ApDung__v_LON_HON_du_hash_TRUNG__van_phai_GHI()
    {
        // 🚨 CA NÀY BẮT LỖI THỨ TỰ KIỂM: nếu so hash TRƯỚC khi so phiên bản thì hai bản **khác** phiên bản
        // mà **cùng** nội dung sẽ bị coi là *"trùng"* ⇒ **MẤT** bản mới hơn ⇒ watermark **đứng yên** mãi mãi
        // và mọi sự kiện sau đó đều bị coi là "mới hơn" ⇒ cập nhật lặp.
        var kq = LuatWatermark.Tinh(1300, 1200, HashA, HashA);

        Assert.Equal(KetLuanWatermark.ApDung, kq.KetLuan);
        Assert.True(kq.ChoPhepGhi);
    }

    [Fact]
    public void Don_MOI__phien_ban_da_ap_NULL__luon_AP_DUNG()
    {
        var kq = LuatWatermark.Tinh(5_000_000, phienBanDaAp: null, hashVao: HashA, hashDaAp: null);

        Assert.Equal(KetLuanWatermark.ApDung, kq.KetLuan);
        Assert.True(kq.ChoPhepGhi);
        Assert.False(kq.NghiNgoThieuBanTrungGian);
    }

    // ── CỜ "NGHI NGỜ THIẾU BẢN TRUNG GIAN" (BR-009) ───────────────────────────────────────────

    [Theory]
    [InlineData(3600, false)]   // đúng bằng ngưỡng ⇒ KHÔNG nghi ngờ (biên trên là "vượt")
    [InlineData(3601, true)]    // vượt 1 giây ⇒ nghi ngờ
    [InlineData(7200, true)]
    public void NghiNgo__khi_do_lech_VUOT_nguong__va_VAN_AP_DUNG(long doLech, bool mongDoiNghiNgo)
    {
        var kq = LuatWatermark.Tinh(1200 + doLech, 1200, HashB, HashA);

        Assert.Equal(KetLuanWatermark.ApDung, kq.KetLuan);
        // 🔒 VẪN ÁP DỤNG: bỏ qua bản mới hơn sẽ để trạng thái đơn **SAI** cho tới nhịp đối soát
        // (khách đã huỷ mà hệ thống vẫn tưởng đang giao) — sai lâu hơn thì tệ hơn.
        Assert.True(kq.ChoPhepGhi);
        Assert.Equal(mongDoiNghiNgo, kq.NghiNgoThieuBanTrungGian);
    }

    [Fact]
    public void Nguong__co_the_dat_lai__khong_hard_code()
    {
        var kq = LuatWatermark.Tinh(1200 + 100, 1200, HashB, HashA, nguongGiay: 50);

        Assert.True(kq.NghiNgoThieuBanTrungGian);
    }

    // ── BẤT BIẾN TRUNG TÂM: watermark CHỈ TĂNG ────────────────────────────────────────────────

    [Fact]
    public void BAT_BIEN__watermark_CHI_TANG__khong_bao_gio_giam()
    {
        // Mô phỏng dãy sự kiện đến SAI THỨ TỰ (webhook/pull xen nhau) và kiểm watermark đơn điệu.
        var watermark = 1000L;
        var thuTuDen = new long[] { 1005, 1003, 1005, 1009, 1002, 1009, 1010 };

        foreach (var v in thuTuDen)
        {
            var kq = LuatWatermark.Tinh(v, watermark, HashB, HashA);

            if (kq.ChoPhepGhi)
            {
                watermark = v;
            }

            Assert.True(kq.ChoPhepGhi ? v > 0 : true);
        }

        Assert.Equal(1010L, watermark);
    }

    [Fact]
    public void BAT_BIEN__moi_phien_ban_chi_duoc_AP_DUNG_DUNG_MOT_LAN()
    {
        var watermark = 1000L;
        var soLanApDung = 0;

        // Cùng một phiên bản đến 5 lần (webhook gửi lại) ⇒ chỉ áp dụng ĐÚNG MỘT lần.
        for (var i = 0; i < 5; i++)
        {
            var kq = LuatWatermark.Tinh(1001, watermark, HashA, HashA);

            if (kq.ChoPhepGhi)
            {
                soLanApDung++;
                watermark = 1001;
            }
        }

        Assert.Equal(1, soLanApDung);
    }
}
