using eCommerce.Settings;

namespace eCommerce.Inventory;

/// <summary>
/// 🚨 **NGƯỠNG LỆCH ĐỐI SOÁT — HÀM THUẦN** (`REC-001`, phần **áp ngưỡng thật**).
///
/// **VÌ SAO PHẢI CÓ (khoảng trống thật):** trước lát cắt này job đối soát **đọc + ghi log** chính sách
/// (`Recon.Threshold.Unit` / `Value` / `Mismatch.Action`) nhưng **không dùng** chúng để phân loại gì cả:
/// lệch `1` đơn vị và lệch `10 000` đơn vị **giống hệt nhau** khi nhìn log và metric.
/// 📌 Hệ quả: người vận hành đặt ngưỡng `0.5%`, **tin** rằng mình chỉ bị báo khi lệch đáng kể — nhưng hệ thống
/// báo **mọi** lệch. Đây đúng họ *"ô chọn có mà không đổi gì"* mà `docs/tiep-tuc-phien-moi.md` §5 việc 2 gọi là
/// **nợ tệ hơn không làm**: người vận hành **tin** một điều không đúng.
///
/// 🔒 Hàm **không ném**, **không side effect**: giá trị ngưỡng lạ đã được <see cref="ChinhSach.DocSo"/> lọc từ trước
/// (trả mặc định + cờ `hopLe`), còn ở đây chỉ còn **số học**.
/// </summary>
public static class NguongLech
{
    /// <summary>Kết luận cho **MỘT** cặp `(kho, SKU)` lệch.</summary>
    public enum KetQua
    {
        /// <summary>Lệch **trong** ngưỡng đã chọn ⇒ *"nhiễu đo"*, không cần người xem.</summary>
        TrongNguong,

        /// <summary>Lệch **vượt** ngưỡng ⇒ cần người xem (mức độ tuỳ `Recon.Mismatch.Action`).</summary>
        VuotNguong
    }

    /// <summary>
    /// So **độ lớn** của lệch với ngưỡng:
    /// <list type="bullet">
    ///   <item><see cref="DonViNguong.Absolute"/>: `|chenhLech|` **&gt;** `giaTri` ⇒ vượt. ⚠️ Dùng **&gt;**
    ///     (không `&gt;=`): ngưỡng `5` nghĩa là *"lệch 5 vẫn chấp nhận được"* — dùng `&gt;=` sẽ báo động **ngay tại**
    ///     con số người vận hành đã tuyên bố là chấp nhận được.</item>
    ///   <item><see cref="DonViNguong.Percent"/>: `|chenhLech| × 100 &gt; giaTri × |cơ sở|` ⇒ vượt.
    ///     📌 **CƠ SỞ (mẫu số) là `TongSoCai`** — **nguồn sự thật** (sổ cái), **không** phải `OnHand`:
    ///     `OnHand` chính là giá trị **đang nghi ngờ**; lấy nó làm mẫu số thì *"lệch 100% của một ảnh chụp sai"*
    ///     vẫn ra 100% ⇒ ngưỡng tự triệt tiêu. 🔒 Khi `TongSoCai = 0` (chưa có bút toán nào) mà lại **có**
    ///     `OnHand ≠ 0` ⇒ **vượt ngưỡng** (mọi tỷ lệ trên 0 đều là vô nghĩa; đây là ca lệch nặng nhất:
    ///     ảnh chụp có hàng mà sổ cái **không có gì**).
    ///     ⚠️ Nhân `100` thay vì chia để **tránh số thực**: `decimal` vẫn chính xác nhưng so sánh
    ///     `decimal` trần bằng `==` bị cấm ở dự án (`XC-001` BR-003) — ở đây **không** so bằng, chỉ so `&gt;`.</item>
    /// </list>
    /// 🔒 `giaTri &lt;= 0` ⇒ **KHÔNG** có ngưỡng ⇒ **mọi** lệch đều tính là **vượt** (hành vi cũ, fail-closed).
    /// 📌 Cố ý **không** lấy `0` làm *"bỏ qua mọi lệch"*: ngưỡng `0` mà hiểu là "không báo gì" sẽ là
    /// **tắt im lặng** — đúng thứ tài liệu này sinh ra để chặn.
    /// </summary>
    public static KetQua Xet(DonViNguong donVi, decimal giaTri, int tongSoCai, int chenhLech)
    {
        var doLon = Math.Abs((long)chenhLech);

        if (giaTri <= 0m)
        {
            return doLon > 0 ? KetQua.VuotNguong : KetQua.TrongNguong;
        }

        if (donVi == DonViNguong.Absolute)
        {
            return (decimal)doLon > giaTri ? KetQua.VuotNguong : KetQua.TrongNguong;
        }

        // `Percent`
        if (tongSoCai == 0)
        {
            return doLon > 0 ? KetQua.VuotNguong : KetQua.TrongNguong;
        }

        var coSo = Math.Abs((long)tongSoCai);

        // `|chenhLech| × 100 > giaTri × |cơ sở|` — không chia, không làm tròn ⇒ không mất độ chính xác.
        return (decimal)doLon * 100m > giaTri * coSo ? KetQua.VuotNguong : KetQua.TrongNguong;
    }

    /// <summary>Tiện dụng: cặp lệch này có vượt ngưỡng không.</summary>
    public static bool VuotNguong(DonViNguong donVi, decimal giaTri, int tongSoCai, int chenhLech)
        => Xet(donVi, giaTri, tongSoCai, chenhLech) == KetQua.VuotNguong;
}
