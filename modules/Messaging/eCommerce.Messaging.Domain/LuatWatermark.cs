namespace eCommerce.Messaging;

/// <summary>
/// **KẾT LUẬN CỦA LUẬT WATERMARK 4 NHÁNH** (`SAL-005` §3.3 — **D5/D6**, `BR-SAL-005-005..009**).
/// </summary>
public enum KetLuanWatermark
{
    /// <summary>⭐ **ÁP DỤNG** — bản đến **mới hơn** watermark (`v_in > v_đã_áp`).</summary>
    ApDung = 0,

    /// <summary>**ĐẾN MUỘN** — `v_in < v_đã_áp`. ⚠️ Đây là ca **BÌNH THƯỜNG** (webhook đến sau nhịp pull),
    /// **KHÔNG** phải lỗi, **KHÔNG** DLQ, **KHÔNG** ném (`BR-005`).</summary>
    DenMuon = 1,

    /// <summary>**TRÙNG** — `v_in == v_đã_áp` **và** `PayloadHash` khớp. Không side effect (`BR-006`).</summary>
    Trung = 2,

    /// <summary>🚨 **XUNG ĐỘT** — `v_in == v_đã_áp` **và** `PayloadHash` **khác**.
    /// **D6: KHÔNG ĐOÁN** ⇒ không ghi đè, DLQ, cần người xử lý (`BR-007`).</summary>
    XungDot = 3
}

/// <summary>Kết quả đầy đủ của luật watermark (gồm cả cờ **nghi ngờ thiếu bản trung gian**).</summary>
/// <param name="KetLuan">Nhánh nào trong 4 nhánh.</param>
/// <param name="NghiNgoThieuBanTrungGian">
/// `true` ⇒ `v_in - v_đã_áp` **vượt** ngưỡng (`BR-009`): **vẫn ÁP DỤNG** nhưng đánh cờ để job đối soát
/// quét lại. 📌 **Vì sao vẫn áp dụng:** bỏ qua bản mới hơn sẽ để trạng thái đơn **SAI** cho tới nhịp đối soát
/// (khách đã huỷ mà hệ thống vẫn tưởng đang giao) — sai lâu hơn thì tệ hơn.
/// </param>
/// <param name="DoLechGiay">`v_in − v_đã_áp` (giây) — chỉ có nghĩa khi **áp dụng**; dùng cho log/metric.</param>
public readonly record struct KetQuaWatermark(
    KetLuanWatermark KetLuan,
    bool NghiNgoThieuBanTrungGian,
    long DoLechGiay)
{
    /// <summary>Có được phép ghi dữ liệu xuống DB không (chỉ nhánh **ÁP DỤNG**).</summary>
    public bool ChoPhepGhi => KetLuan == KetLuanWatermark.ApDung;

    /// <summary>Có phải đẩy DLQ không (chỉ nhánh **XUNG ĐỘT** — `BR-007`).</summary>
    public bool PhaiDayDlq => KetLuan == KetLuanWatermark.XungDot;
}

/// <summary>
/// 🚨 **LUẬT WATERMARK 4 NHÁNH** (`SAL-005` §3.3) — **LÕI THUẦN**, test được **mọi** nhánh không cần DB.
///
/// **VÌ SAO WATERMARK LÀ `epoch giây của update_time` MÀ KHÔNG PHẢI BỘ ĐẾM TĂNG ĐỀU (`D5`):**
/// sàn TMĐT **chỉ** cho `update_time`; ta **không** kiểm soát bộ đếm của sàn. Một bộ đếm nội bộ *"tăng 1 mỗi
/// sự kiện"* **không** trả lời được câu hỏi duy nhất cần trả lời: *"bản này có MỚI HƠN bản kia không?"* —
/// nó chỉ nói *"tôi nhận được bản này lúc nào"*, mà thứ tự **nhận** khác thứ tự **xảy ra** (webhook và pull
/// là hai đường vào, `D2`).
///
/// **VÌ SAO PHẢI LÀ HÀM THUẦN (không viết thẳng trong handler):**
/// bốn nhánh này là nơi **dễ sai nhất** và sai thì **im lặng** — ví dụ coi *"đến muộn"* là lỗi ⇒
/// **con trỏ đồng bộ không tiến** ⇒ sàn gửi lại **cùng** đơn ⇒ **lặp vô hạn + chặn cả kênh**
/// (đúng lỗi **đã đo được** ở `sal-005` §16.4 #3). Hàm thuần ⇒ có test cho **cả 4 nhánh** + các biên.
///
/// 🔒 **Thứ tự kiểm là một phần của đặc tả:** so **phiên bản TRƯỚC**, rồi mới so **hash**. Nếu so hash trước
/// thì hai bản **khác** phiên bản nhưng **cùng** nội dung sẽ bị coi là *"trùng"* ⇒ **mất** bản mới hơn.
/// </summary>
public static class LuatWatermark
{
    /// <summary>Ngưỡng mặc định coi là *"nghi ngờ thiếu bản trung gian"* — **3600 giây** (`BR-009`).</summary>
    public const int NguongGiayMacDinh = 3600;

    /// <summary>
    /// Áp luật 4 nhánh.
    /// </summary>
    /// <param name="phienBanVao">`AggregateVersion` của sự kiện đến (`v_in`) — epoch giây UTC của `update_time` sàn.</param>
    /// <param name="phienBanDaAp">Watermark đã áp cho **đơn** này (`v_đã_áp`). ⚠️ **`null`** = **chưa từng nhận**
    /// ⇒ luôn **ÁP DỤNG** (đơn mới).
    /// <param name="hashVao">`PayloadHash` của sự kiện đến.</param>
    /// <param name="hashDaAp">`PayloadHash` đã lưu. ⚠️ **`null`/rỗng** khi đã có phiên bản: dữ liệu cũ
    /// (trước khi có cột hash) ⇒ **KHÔNG** được coi là *"trùng"* (không thể chứng minh nội dung giống nhau)
    /// và **KHÔNG** được coi là *"xung đột"* (không có gì để so) ⇒ xử theo **nhánh phiên bản**.
    /// 📌 Đây là quyết định **có chủ ý** để migration không làm đỏ DLQ hàng loạt.
    /// </param>
    /// <param name="nguongGiay">Ngưỡng cờ *"nghi ngờ thiếu bản trung gian"* (mặc định 3600 s).</param>
    public static KetQuaWatermark Tinh(
        long phienBanVao,
        long? phienBanDaAp,
        string? hashVao,
        string? hashDaAp,
        int nguongGiay = NguongGiayMacDinh)
    {
        // ── ĐƠN MỚI: chưa từng nhận ⇒ luôn áp dụng (không có gì để so) ──────────────────────────
        if (phienBanDaAp is null)
        {
            return new KetQuaWatermark(KetLuanWatermark.ApDung, false, 0);
        }

        var daAp = phienBanDaAp.Value;

        // ── NHÁNH A: ĐẾN MUỘN (v_in < v_đã_áp) ─────────────────────────────────────────────────
        // ⚠️ CA BÌNH THƯỜNG — KHÔNG ném, KHÔNG DLQ, KHÔNG ghi gì (`BR-005`).
        // 📌 Trả `DoLechGiay` ÂM để log nói được *"muộn bao nhiêu"* mà không phải tính lại.
        if (phienBanVao < daAp)
        {
            return new KetQuaWatermark(KetLuanWatermark.DenMuon, false, phienBanVao - daAp);
        }

        // ── NHÁNH B/C/D: v_in >= v_đã_áp ───────────────────────────────────────────────────────
        var doLech = phienBanVao - daAp;

        if (phienBanVao == daAp)
        {
            // 🔒 Chỉ kết luận TRÙNG/XUNG ĐỘT khi **CÓ ĐỦ HAI HASH**. Thiếu một bên ⇒ không thể chứng minh
            // nội dung giống nhau **và** cũng không có gì để gọi là xung đột ⇒ đi tiếp như nhánh phiên bản.
            var coDuHaiHash = !string.IsNullOrWhiteSpace(hashVao) && !string.IsNullOrWhiteSpace(hashDaAp);

            if (!coDuHaiHash)
            {
                // ⚠️ Ghi rõ: đây là khe hở **đã biết**. Hệ quả xấu nhất là **bỏ qua một bản cùng giây**
                // — an toàn hơn hẳn việc **ghi đè** dựa trên suy đoán (D6: "không đoán").
                return new KetQuaWatermark(KetLuanWatermark.Trung, false, 0);
            }

            // So hash **bằng Ordinal** — hash là hex do MÁY sinh, không phải văn bản người dùng:
            // so sánh phụ thuộc culture ở đây là nguồn gốc của *"hai instance, hai kết luận"*.
            return string.Equals(hashVao, hashDaAp, StringComparison.Ordinal)
                ? new KetQuaWatermark(KetLuanWatermark.Trung, false, 0)
                : new KetQuaWatermark(KetLuanWatermark.XungDot, false, 0);
        }

        // ── NHÁNH D: v_in > v_đã_áp ⇒ ÁP DỤNG (+ cờ nếu nhảy quá xa) ────────────────────────────
        var nghiNgo = doLech > nguongGiay;

        return new KetQuaWatermark(KetLuanWatermark.ApDung, nghiNgo, doLech);
    }
}
