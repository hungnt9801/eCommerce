using eCommerce.Kernel.Money;
using Volo.Abp;

namespace eCommerce.Pricing;

/// <summary>
/// 🆕 **`W17-2` — LOẠI CHUYỂN ĐỘNG KHO THEO NGHĨA ĐỊNH GIÁ** (giá trị nội bộ của Domain Pricing).
///
/// 🔑 **VÌ SAO KHÔNG DÙNG THẲNG `eCommerce.Inventory.LoaiChuyenDongGiaVon`:** Domain Pricing **KHÔNG** được
/// tham chiếu Domain Inventory (`D-186` ⑥/M2). Adapter ở module Inventory ánh xạ
/// `LoaiChuyenDongGiaVon` → kiểu này (giá trị thuần), rồi hàm thuần bên dưới mới chạy.
/// 📌 Ba giá trị khớp **1-1** với `eCommerce.Inventory.LoaiChuyenDongGiaVon` (cùng số) — adapter kiểm bằng test.
/// </summary>
public enum LoaiDongGiaVon
{
    /// <summary>Tăng tồn + **có** đơn giá ⇒ tính lại bình quân.</summary>
    NhapCoGia = 0,

    /// <summary>Tăng tồn nhưng **KHÔNG** có đơn giá ⇒ giá vốn không xác định từ mốc này.</summary>
    NhapKhongGia = 1,

    /// <summary>Giảm tồn ⇒ giảm số lượng, **KHÔNG** đổi đơn giá bình quân. `SoLuongDelta` **âm**.</summary>
    Xuat = 2
}

/// <summary>
/// Một chuyển động đã chuẩn hoá cho hàm thuần. 🚨 `SoLuongDelta` **có dấu**: nhập `&gt; 0`, xuất `&lt; 0`.
/// `DonGiaNhap` chỉ khác `null` khi <see cref="LoaiDongGiaVon.NhapCoGia"/>.
/// </summary>
public sealed record ChuyenDongGiaVon(
    string Sku,
    DateOnly NgayKinhDoanh,
    DateTime OccurredAtUtc,
    LoaiDongGiaVon Loai,
    decimal SoLuongDelta,
    Money? DonGiaNhap,
    Guid ReferenceLineId);

/// <summary>Một **kỳ giá vốn** hàm thuần sinh ra. `EffectiveTo = null` = kỳ cuối (vô thời hạn).</summary>
public sealed record KyGiaVon(string Sku, DateOnly EffectiveFrom, DateOnly? EffectiveTo, Money GiaBinhQuan);

/// <summary>Kết quả hàm thuần: danh sách kỳ + tập SKU có `costMissing` (để đếm `RowsFlaggedMissing`).</summary>
public sealed class KetQuaTinhGiaVon
{
    public List<KyGiaVon> CacKy { get; } = new();

    /// <summary>SKU mà **từ một mốc** bình quân không xác định (`NhapKhongGia`) ⇒ **KHÔNG** có kỳ sau mốc.</summary>
    public HashSet<string> SkuThieuGia { get; } = new(StringComparer.Ordinal);
}

/// <summary>
/// 🆕 **`W17-2` — BÌNH QUÂN GIA QUYỀN DI ĐỘNG: HÀM THUẦN** (`PRC-001-arch` §6; `D-177` ①③).
///
/// 🚨 **KHÔNG I/O, KHÔNG đọc đồng hồ, KHÔNG tham chiếu Inventory.** Nhận dữ liệu **giá trị** đã nạp ⇒ test được
/// không cần PostgreSQL, và chạy lại bao nhiêu lần cũng **cùng đầu ra** (tất định theo
/// `(NgayKinhDoanh, OccurredAtUtc, ReferenceLineId)` — `D-176` ②/`D-177` ③).
///
/// 🔒 **BẤT BIẾN TIỀN (`D-318` C5 iv):** 100% `Money`; so sánh qua `Money.CompareTo`; **0** `double`;
/// **0** `==` trên `decimal` tiền; **0** `Math.Round`/`decimal.Round` — làm tròn chỉ ở **biên ra** (Application)
/// qua `IRoundingPolicy` (`XC-001`).
///
/// 🔒 **THIẾU GIÁ VỐN ⇒ `null`, CẤM `0`** (`D-322`/§5.5): mốc `NhapKhongGia` làm `avg = null`, `costMissing = true`
/// và **v1 KHÔNG phục hồi** (kể cả có lần nhập có giá sau đó) ⇒ **không** sinh kỳ nào cho tới cuối khoảng.
/// Giá vốn **`0`** (hàng tặng/mẫu) vẫn **hợp lệ** và **có** kỳ — khác hẳn "không biết".
/// </summary>
public static class TinhBinhQuanGiaQuyenDiDong
{
    /// <summary>
    /// Tính kỳ giá vốn cho **mọi SKU** trong <paramref name="cacChuyenDong"/>.
    /// 📌 Grain = **SKU toàn hệ thống** (không `WarehouseId` — `D-318` C9/K06-06).
    /// </summary>
    public static KetQuaTinhGiaVon Tinh(IReadOnlyList<ChuyenDongGiaVon> cacChuyenDong, Currency tienTe)
    {
        ArgumentNullException.ThrowIfNull(cacChuyenDong);
        ArgumentNullException.ThrowIfNull(tienTe);

        var ketQua = new KetQuaTinhGiaVon();

        foreach (var nhom in cacChuyenDong
                     .GroupBy(x => x.Sku, StringComparer.Ordinal)
                     .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var sapXep = nhom
                .OrderBy(x => x.NgayKinhDoanh)
                .ThenBy(x => x.OccurredAtUtc)
                .ThenBy(x => x.ReferenceLineId)
                .ToList();

            var cacKy = TinhMotSku(sapXep, tienTe, out var thieuGia);

            ketQua.CacKy.AddRange(cacKy);

            if (thieuGia)
            {
                ketQua.SkuThieuGia.Add(nhom.Key);
            }
        }

        return ketQua;
    }

    private static List<KyGiaVon> TinhMotSku(
        IReadOnlyList<ChuyenDongGiaVon> sapXep, Currency tienTe, out bool thieuGia)
    {
        var sku = sapXep[0].Sku;

        decimal soLuong = 0m;
        var giaTri = Money.Zero(tienTe);
        Money? binhQuan = null;
        var costMissing = false;

        // Kỳ tạm: (ngày, bình quân cuối ngày). Chốt `EffectiveTo` SAU khi biết kỳ kế tiếp.
        var kyTam = new List<(DateOnly Ngay, Money BinhQuan)>();

        foreach (var ngay in sapXep
                     .GroupBy(x => x.NgayKinhDoanh)
                     .OrderBy(g => g.Key))
        {
            var coNhapCoGiaTrongNgay = false;

            foreach (var cd in ngay
                         .OrderBy(x => x.OccurredAtUtc)
                         .ThenBy(x => x.ReferenceLineId))
            {
                KiemTraDong(cd);

                switch (cd.Loai)
                {
                    case LoaiDongGiaVon.NhapCoGia:
                        coNhapCoGiaTrongNgay = true;

                        if (costMissing)
                        {
                            // 🚨 v1 KHÔNG phục hồi: chỉ tăng số lượng; giá trị/bình quân vẫn KHÔNG xác định.
                            soLuong += cd.SoLuongDelta;
                        }
                        else
                        {
                            giaTri += cd.DonGiaNhap! * cd.SoLuongDelta;
                            soLuong += cd.SoLuongDelta;

                            // `qty <= 0` ⇒ không chia được; giữ `null` (không bịa số) — kỳ không sinh.
                            binhQuan = soLuong > 0m ? giaTri / soLuong : null;
                        }

                        break;

                    case LoaiDongGiaVon.NhapKhongGia:
                        soLuong += cd.SoLuongDelta;
                        costMissing = true;
                        binhQuan = null;
                        break;

                    case LoaiDongGiaVon.Xuat:
                        var xuat = -cd.SoLuongDelta;

                        // Xuất KHÔNG đổi đơn giá bình quân; chỉ trừ giá trị khi đã biết bình quân.
                        if (binhQuan is not null)
                        {
                            giaTri -= binhQuan * xuat;
                        }

                        soLuong -= xuat;
                        break;

                    default:
                        throw new BusinessException(PricingErrorCodes.CauHinhGiaVonKhongHopLe)
                            .WithData("sku", sku)
                            .WithData("loai", cd.Loai.ToString());
                }
            }

            // 📌 Chỉ sinh kỳ ở NGÀY CÓ NHẬP **và** bình quân xác định. `null` ⇒ KHÔNG sinh kỳ (CẤM ghi 0).
            if (coNhapCoGiaTrongNgay && binhQuan is not null)
            {
                kyTam.Add((ngay.Key, binhQuan));
            }
        }

        thieuGia = costMissing;

        var ketQua = new List<KyGiaVon>(kyTam.Count);

        for (var i = 0; i < kyTam.Count; i++)
        {
            var den = i + 1 < kyTam.Count ? kyTam[i + 1].Ngay.AddDays(-1) : (DateOnly?)null;

            ketQua.Add(new KyGiaVon(sku, kyTam[i].Ngay, den, kyTam[i].BinhQuan));
        }

        return ketQua;
    }

    /// <summary>
    /// Kiểm **dấu** và **đơn giá** của một chuyển động (fail-closed — `D-321`). Sai ⇒ **NÉM `0014`**, không bỏ qua:
    /// một dòng lệch dấu làm `qty`/`value` sai ⇒ bình quân các kỳ sau sai mà **không có lỗi nào**.
    /// </summary>
    private static void KiemTraDong(ChuyenDongGiaVon cd)
    {
        if (string.IsNullOrWhiteSpace(cd.Sku))
        {
            throw new BusinessException(PricingErrorCodes.CauHinhGiaVonKhongHopLe)
                .WithData("truong", nameof(cd.Sku));
        }

        switch (cd.Loai)
        {
            case LoaiDongGiaVon.NhapCoGia:
            case LoaiDongGiaVon.NhapKhongGia:
                if (cd.SoLuongDelta <= 0m)
                {
                    throw new BusinessException(PricingErrorCodes.CauHinhGiaVonKhongHopLe)
                        .WithData("sku", cd.Sku)
                        .WithData("lyDo", "Nhập kho phải có `SoLuongDelta` DƯƠNG.")
                        .WithData("soLuongDelta", cd.SoLuongDelta);
                }

                break;

            case LoaiDongGiaVon.Xuat:
                if (cd.SoLuongDelta >= 0m)
                {
                    throw new BusinessException(PricingErrorCodes.CauHinhGiaVonKhongHopLe)
                        .WithData("sku", cd.Sku)
                        .WithData("lyDo", "Xuất kho phải có `SoLuongDelta` ÂM.")
                        .WithData("soLuongDelta", cd.SoLuongDelta);
                }

                break;

            default:
                throw new BusinessException(PricingErrorCodes.CauHinhGiaVonKhongHopLe)
                    .WithData("sku", cd.Sku)
                    .WithData("loai", cd.Loai.ToString());
        }

        if (cd.Loai != LoaiDongGiaVon.NhapCoGia)
        {
            return;
        }

        if (cd.DonGiaNhap is null)
        {
            throw new BusinessException(PricingErrorCodes.CauHinhGiaVonKhongHopLe)
                .WithData("sku", cd.Sku)
                .WithData("lyDo", "`NhapCoGia` BẮT BUỘC có `DonGiaNhap`.");
        }

        if (cd.DonGiaNhap.IsNegative)
        {
            throw new BusinessException(PricingErrorCodes.CauHinhGiaVonKhongHopLe)
                .WithData("sku", cd.Sku)
                .WithData("lyDo", "Đơn giá nhập KHÔNG được âm.")
                .WithData("donGia", cd.DonGiaNhap.Amount);
        }
    }
}
