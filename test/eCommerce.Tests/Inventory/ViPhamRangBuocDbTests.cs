using eCommerce.Kernel.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Volo.Abp.Data;

namespace eCommerce.Tests.Inventory;

/// <summary>
/// Test **ánh xạ vi phạm ràng buộc DB → lỗi nghiệp vụ** (vòng 29 — trả nợ đã đo được).
///
/// 🚨 **HAI BẤT BIẾN QUAN TRỌNG NHẤT CỦA BỘ TEST NÀY:**
/// 1. **KHÔNG** ánh xạ lỗi hạ tầng thành lỗi nghiệp vụ (nếu không: **che mất sự cố thật** — hệ thống hỏng
///    nhưng client tưởng là *"dữ liệu trùng"* và **retry mãi**).
/// 2. **Mãi mãi** ánh xạ được cả **ba** SQLSTATE ràng buộc (`23505`/`23514`/`23503`) — nếu rơi về `500`,
///    client **không phân biệt** được *"đã làm rồi"* với *"hệ thống hỏng"*.
/// </summary>
public class ViPhamRangBuocDbTests
{
    // ── Ba SQLSTATE ràng buộc: LUÔN có kết quả ──────────────────────────────

    [Theory]
    [InlineData("23505")]   // vi phạm duy nhất
    [InlineData("23514")]   // vi phạm CHECK
    [InlineData("23503")]   // vi phạm khoá ngoại
    public void Ba_SQLSTATE_rang_buoc_LUON_anh_xa_duoc__du_khong_biet_ten(string sqlState)
    {
        var kq = ViPhamRangBuocDb.AnhXa(sqlState, tenRangBuoc: "RANG_BUOC_LA_HOAN_TOAN");

        Assert.NotNull(kq);
        Assert.Equal(ViPhamRangBuocDb.MaChung, kq!.Code);   // mã chung — vẫn TỐT HƠN `500`
    }

    [Fact]
    public void Rang_buoc_DA_BIET_thi_tra_DUNG_ma_nghiep_vu__khong_dung_ma_chung()
    {
        var kq = ViPhamRangBuocDb.AnhXa("23505", "IX_InvStockLedger_TenantId_ReferenceLineId");

        Assert.Equal("eCommerce:Inventory:0010", kq!.Code);
        Assert.True(kq.LaViPhamDuyNhat);
    }

    [Fact]
    public void Giu_cho_TRUNG_NGUON_tra_ma_0032__day_la_ca_da_DO_duoc_500_truoc_day()
    {
        var kq = ViPhamRangBuocDb.AnhXa("23505", "IX_InvStockReservations_TenantId_Ref_Sku_Kho");

        Assert.Equal("eCommerce:Inventory:0032", kq!.Code);
    }

    [Fact]
    public void CHECK_Reserved_vuot_OnHand_tra_ma_KHONG_DU_HANG()
    {
        var kq = ViPhamRangBuocDb.AnhXa("23514", "CK_InvItems_Reserved_KhongVuotOnHand");

        Assert.Equal("eCommerce:Inventory:0001", kq!.Code);
        Assert.False(kq.LaViPhamDuyNhat);   // đây là CHECK, không phải unique
    }

    [Fact]
    public void Trung_Thang_bac_thang_tra_DUNG_ma_Pricing_0008()
    {
        var kq = ViPhamRangBuocDb.AnhXa("23505", "IX_PrcTierPrices_TenantId_SetId_Sku_MinQty");

        Assert.Equal("eCommerce:Pricing:0008", kq!.Code);
    }

    // ── 🚨 KHÔNG được nuốt lỗi hệ thống ─────────────────────────────────────

    [Theory]
    [InlineData("08006")]   // mất kết nối
    [InlineData("53300")]   // quá nhiều kết nối
    [InlineData("40001")]   // serialization failure
    [InlineData("57014")]   // query bị huỷ
    [InlineData(null)]
    public void SQLSTATE_KHONG_phai_rang_buoc_thi_TRA_NULL__de_noi_len_500(string? sqlState)
    {
        // 🚨 Nếu ánh xạ cả những mã này thì **sự cố hạ tầng bị che thành lỗi nghiệp vụ**
        // ⇒ đội vận hành không thấy, client retry vô hạn.
        Assert.Null(ViPhamRangBuocDb.AnhXa(sqlState, "IX_BAT_KY"));
    }

    [Fact]
    public void Ten_rang_buoc_NULL_van_tra_ma_chung__khong_nem()
    {
        var kq = ViPhamRangBuocDb.AnhXa("23505", tenRangBuoc: null);

        Assert.NotNull(kq);
        Assert.Equal(ViPhamRangBuocDb.MaChung, kq!.Code);
    }

    // ── Bảo trì: phát hiện ràng buộc bị bỏ sót ──────────────────────────────

    [Fact]
    public void So_rang_buoc_da_anh_xa_KHONG_giam_di__chong_xoa_nham_khi_sua_chua()
    {
        // Chốt sàn số lượng: nếu ai đó **xoá** mất các dòng ánh xạ khi sửa file thì test ĐỎ
        // (chứ không âm thầm để các ca đó quay lại `500`).
        Assert.True(ViPhamRangBuocDb.CacRangBuocDaAnhXa.Count >= 14,
            $"Chỉ còn {ViPhamRangBuocDb.CacRangBuocDaAnhXa.Count} ràng buộc được ánh xạ — có dòng bị xoá?");
    }

    [Fact]
    public void Moi_ma_da_anh_xa_deu_CO_TIEN_TO_he_thong__khong_tra_ve_chuoi_rong()
    {
        // Mã lỗi phải theo quy ước `eCommerce:...` — mã rỗng/sai tiền tố làm CSKH không phân loại được.
        foreach (var ten in ViPhamRangBuocDb.CacRangBuocDaAnhXa)
        {
            var kq = ViPhamRangBuocDb.AnhXa("23505", ten);

            Assert.NotNull(kq);
            Assert.StartsWith("eCommerce:", kq!.Code, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(kq.Message), $"Ràng buộc {ten} thiếu thông điệp.");
        }
    }

    // ── 🆕 `D-210` (`WP-15`): mã chung cho XUNG ĐỘT DỮ LIỆU (HTTP `409`) ─────────────────────
    // 🚨 Vì sao KHÔNG dùng lại `MaChung = 0001`: `0001` là "dữ liệu bị từ chối, phải SỬA" (`403`);
    // `0002` là "người khác vừa sửa, THỬ LẠI được" (`409`). Cùng một mã ⇒ client/alert không phân biệt
    // được hai hành vi TRÁI NGƯỢC bằng `error.code` (đúng nợ `D-66`⑦). Ca dưới khoá bất biến đó.

    [Fact]
    public void Ma_xung_dot_la_0002_va_KHONG_trung_ma_chung_0001()
    {
        Assert.Equal("eCommerce:Db:0002", ViPhamRangBuocDb.MaXungDot);
        Assert.NotEqual(ViPhamRangBuocDb.MaChung, ViPhamRangBuocDb.MaXungDot);
    }

    [Fact]
    public void LaXungDotDuLieu_nhan_AbpDbConcurrencyException_long_trong_Exception()
    {
        // 🔑 ABP/EF có thể bọc xung đột trong một lớp `Exception` khác ⇒ phải đi `InnerException`.
        var ex = new Exception("boc ngoai", new AbpDbConcurrencyException("xung dot ConcurrencyStamp"));

        Assert.True(ViPhamRangBuocDb.LaXungDotDuLieu(ex));
    }

    [Fact]
    public void LaXungDotDuLieu_nhan_AbpDbConcurrencyException_long_trong_DbUpdateException()
    {
        // Đường thật khi `SaveChangesAsync` flush: `DbUpdateException` bọc `AbpDbConcurrencyException`.
        var ex = new DbUpdateException("luu that bai", new AbpDbConcurrencyException("xung dot ConcurrencyStamp"));

        Assert.True(ViPhamRangBuocDb.LaXungDotDuLieu(ex));
    }

    [Fact]
    public void LaXungDotDuLieu_KHONG_nhan_PostgresException_va_null()
    {
        // 🚨 Nhánh `409` CHỈ nhận xung đột đồng thời, KHÔNG nuốt vi phạm ràng buộc (`23505`) hay
        // serialization failure (`40001`) — hai loại đó phải đi nhánh Postgres hiện có (`403`/`500`).
        Assert.False(ViPhamRangBuocDb.LaXungDotDuLieu(new PostgresException("vi pham duy nhat", "ERROR", "ERROR", "23505")));
        Assert.False(ViPhamRangBuocDb.LaXungDotDuLieu(new PostgresException("serialization", "ERROR", "ERROR", "40001")));
        Assert.False(ViPhamRangBuocDb.LaXungDotDuLieu(null));
    }

    [Fact]
    public void LaXungDotDuLieu_dung_o_dung_khi_tim_thay__khong_nham_Exception_thuong()
    {
        // Chiều ngược: `Exception` thường (không có xung đột trong chuỗi) phải trả `false`.
        Assert.False(ViPhamRangBuocDb.LaXungDotDuLieu(new Exception("loi thuong", new InvalidOperationException("khac"))));
    }
}
