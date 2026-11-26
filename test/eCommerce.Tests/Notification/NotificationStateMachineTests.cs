using eCommerce.Notification;
using Volo.Abp;

namespace eCommerce.Tests.Notification;

/// <summary>
/// `TC-05`/`TC-06` — **máy trạng thái 15 hàng** của `NotificationQueueItem` (`SRS §3` / `ARCH §3.4`).
///
/// 🔒 Mỗi cạnh: hàm đổi **đúng cặp `From→To`** + phát **đúng một `EventType`**; guard sai ⇒ **NÉM**
/// `NotificationErrorCodes.ChuyenTrangThaiKhongHopLe` (`0011`) — **KHÔNG** no-op im lặng.
/// Hàng 6 khi đã hấp thụ ⇒ `DaBanGiaoRoi` (`0014`); hàng 7 **không** tăng `AttemptCount`; hàng 14 vế
/// `ExpiresAtUtc IS NULL` **vẫn thử lại**; hàng 15 không tăng `AttemptCount`; hàng 12 trạng thái hấp thụ
/// không có cạnh ra; hàng 13 bị khử trùng ⇒ `DaHuy` + bucket `NULL`.
///
/// 📌 Test ở **tầng Domain thuần, không DB** (`NOT-001-arch.md` §13.1) — đúng tầng của luật.
/// </summary>
public class NotificationStateMachineTests
{
    private static void Nem0011(Action hanhDong)
    {
        var ex = Assert.Throws<BusinessException>(hanhDong);
        Assert.Equal(NotificationErrorCodes.ChuyenTrangThaiKhongHopLe, ex.Code);
    }

    private static void Nem0014(Action hanhDong)
    {
        var ex = Assert.Throws<BusinessException>(hanhDong);
        Assert.Equal(NotificationErrorCodes.DaBanGiaoRoi, ex.Code);
    }

    // ── Hàng 1: [*] → Moi (Enqueued, FromStatus = null) ────────────────────────────────────────

    [Fact]
    public void TC05_hang1_TaoMoi_phat_Enqueued_FromNull_ToMoi()
    {
        var muc = NotificationTestData.QueueItem();

        Assert.Equal(NotificationQueueStatus.Moi, muc.Status);
        Assert.Equal(0, muc.AttemptCount);

        var suKien = muc.SuKienTaoMoi();

        Assert.Equal(NotificationQueueEventType.Enqueued, suKien.EventType);
        Assert.Null(suKien.FromStatus);
        Assert.Equal(NotificationQueueStatus.Moi, suKien.ToStatus);
    }

    // ── Hàng 2: Moi → ChoDuyet (QueuedForApproval) ─────────────────────────────────────────────

    [Fact]
    public void TC05_hang2_DanhDauChoDuyet_MoiSangChoDuyet()
    {
        var muc = NotificationTestData.QueueItem();
        var batchId = Guid.NewGuid();

        var suKien = muc.DanhDauChoDuyet(batchId);

        Assert.Equal(NotificationQueueEventType.QueuedForApproval, suKien.EventType);
        Assert.Equal(NotificationQueueStatus.Moi, suKien.FromStatus);
        Assert.Equal(NotificationQueueStatus.ChoDuyet, suKien.ToStatus);
        Assert.Equal(NotificationQueueStatus.ChoDuyet, muc.Status);
        Assert.Equal(batchId, muc.BatchId);
    }

    [Fact]
    public void TC05_hang2_guard_sai_trang_thai_thi_NEM_0011()
    {
        var muc = NotificationTestData.ToChoGui(); // đã ở ChoGui, KHÔNG phải Moi

        Nem0011(() => muc.DanhDauChoDuyet(Guid.NewGuid()));
    }

    // ── Hàng 3: Moi → ChoGui (ReadyToDispatch, ScheduledAtUtc <= now) ─────────────────────────

    [Fact]
    public void TC05_hang3_DanhDauSanSang_MoiSangChoGui()
    {
        var muc = NotificationTestData.QueueItem(scheduledAtUtc: NotificationTestData.Now.AddMinutes(-1));

        var suKien = muc.DanhDauSanSang(NotificationTestData.Now);

        Assert.Equal(NotificationQueueEventType.ReadyToDispatch, suKien.EventType);
        Assert.Equal(NotificationQueueStatus.Moi, suKien.FromStatus);
        Assert.Equal(NotificationQueueStatus.ChoGui, suKien.ToStatus);
        Assert.Equal(NotificationQueueStatus.ChoGui, muc.Status);
    }

    [Fact]
    public void TC05_hang3_guard_chua_toi_han_thi_NEM_0011()
    {
        // ScheduledAtUtc > now ⇒ gọi hàm chuyển là VI PHẠM (job phải no-op trước khi gọi).
        var muc = NotificationTestData.QueueItem(scheduledAtUtc: NotificationTestData.Now.AddHours(1));

        Nem0011(() => muc.DanhDauSanSang(NotificationTestData.Now));
    }

    // ── Hàng 4: ChoDuyet → ChoGui (BatchApproved) ─────────────────────────────────────────────

    [Fact]
    public void TC05_hang4_DuyetLo_ChoDuyetSangChoGui()
    {
        var muc = NotificationTestData.QueueItem();
        muc.DanhDauChoDuyet(Guid.NewGuid());

        var suKien = muc.DuyetLo(Guid.NewGuid());

        Assert.Equal(NotificationQueueEventType.BatchApproved, suKien.EventType);
        Assert.Equal(NotificationQueueStatus.ChoDuyet, suKien.FromStatus);
        Assert.Equal(NotificationQueueStatus.ChoGui, suKien.ToStatus);
        Assert.Equal(NotificationQueueStatus.ChoGui, muc.Status);
    }

    [Fact]
    public void TC05_hang4_guard_sai_trang_thai_thi_NEM_0011()
    {
        var muc = NotificationTestData.QueueItem(); // Moi, KHÔNG phải ChoDuyet

        Nem0011(() => muc.DuyetLo(Guid.NewGuid()));
    }

    // ── Hàng 5: ChoGui → DangBanGiao (Dispatching) ────────────────────────────────────────────

    [Fact]
    public void TC05_hang5_GianhBanGiao_ChoGuiSangDangBanGiao()
    {
        var muc = NotificationTestData.ToChoGui();
        var lease = NotificationTestData.Now.AddSeconds(120);

        var suKien = muc.GianhBanGiao(lease);

        Assert.Equal(NotificationQueueEventType.Dispatching, suKien.EventType);
        Assert.Equal(NotificationQueueStatus.ChoGui, suKien.FromStatus);
        Assert.Equal(NotificationQueueStatus.DangBanGiao, suKien.ToStatus);
        Assert.Equal(NotificationQueueStatus.DangBanGiao, muc.Status);
        Assert.Equal(lease, muc.DispatchLeaseExpiresAtUtc);
    }

    [Fact]
    public void TC05_hang5_guard_sai_trang_thai_thi_NEM_0011()
    {
        var muc = NotificationTestData.QueueItem(); // Moi

        Nem0011(() => muc.GianhBanGiao(NotificationTestData.Now.AddSeconds(120)));
    }

    // ── Hàng 6: DangBanGiao → DaBanGiao (HandedOff); đã hấp thụ ⇒ 0014 ────────────────────────

    [Fact]
    public void TC05_hang6_DanhDauDaBanGiao_DangBanGiaoSangDaBanGiao()
    {
        var muc = NotificationTestData.ToDangBanGiao();

        var suKien = muc.DanhDauDaBanGiao(NotificationTestData.Now);

        Assert.Equal(NotificationQueueEventType.HandedOff, suKien.EventType);
        Assert.Equal(NotificationQueueStatus.DangBanGiao, suKien.FromStatus);
        Assert.Equal(NotificationQueueStatus.DaBanGiao, suKien.ToStatus);
        Assert.Equal(NotificationQueueStatus.DaBanGiao, muc.Status);
        Assert.Equal(NotificationTestData.Now, muc.DispatchedAtUtc);
        Assert.Null(muc.DispatchLeaseExpiresAtUtc);
    }

    [Fact]
    public void TC05_hang6_goi_lai_khi_da_hap_thu_thi_NEM_0014()
    {
        var muc = NotificationTestData.ToDaBanGiao(); // đã DaBanGiao (hấp thụ)

        Nem0014(() => muc.DanhDauDaBanGiao(NotificationTestData.Now));
    }

    // ── Hàng 7: DangBanGiao → ChoGui khi lease hết hạn — KHÔNG tăng AttemptCount ─────────────

    [Fact]
    public void TC05_hang7_KhoiPhucLease_veChoGui_va_KHONG_tang_AttemptCount()
    {
        var muc = NotificationTestData.ToDangBanGiao(leaseExpiresAtUtc: NotificationTestData.Now.AddSeconds(10));
        Assert.Equal(0, muc.AttemptCount);

        var suKien = muc.KhoiPhucLease(NotificationTestData.Now.AddMinutes(1));

        Assert.Equal(NotificationQueueEventType.LeaseExpired, suKien.EventType);
        Assert.Equal(NotificationQueueStatus.DangBanGiao, suKien.FromStatus);
        Assert.Equal(NotificationQueueStatus.ChoGui, suKien.ToStatus);
        Assert.Equal(NotificationQueueStatus.ChoGui, muc.Status);
        Assert.Equal(0, muc.AttemptCount); // 🚨 KHÔNG tăng (phân biệt hàng 14)
        Assert.Null(muc.DispatchLeaseExpiresAtUtc);
    }

    [Fact]
    public void TC05_hang7_lease_chua_het_han_thi_NEM_0011()
    {
        var muc = NotificationTestData.ToDangBanGiao(leaseExpiresAtUtc: NotificationTestData.Now.AddSeconds(120));

        Nem0011(() => muc.KhoiPhucLease(NotificationTestData.Now)); // now <= lease ⇒ cấm cướp
    }

    // ── Hàng 8: DangBanGiao → Loi (Failed) ────────────────────────────────────────────────────

    [Fact]
    public void TC05_hang8_DanhDauLoi_DangBanGiaoSangLoi()
    {
        var muc = NotificationTestData.ToDangBanGiao();

        var suKien = muc.DanhDauLoi("NCC-0002", "ly-do-che");

        Assert.Equal(NotificationQueueEventType.Failed, suKien.EventType);
        Assert.Equal(NotificationQueueStatus.DangBanGiao, suKien.FromStatus);
        Assert.Equal(NotificationQueueStatus.Loi, suKien.ToStatus);
        Assert.Equal(NotificationQueueStatus.Loi, muc.Status);
        Assert.Equal("NCC-0002", muc.LastErrorCode);
        Assert.Equal("ly-do-che", muc.LastReasonMasked);
    }

    [Fact]
    public void TC05_hang8_guard_sai_trang_thai_thi_NEM_0011()
    {
        var muc = NotificationTestData.ToChoGui();

        Nem0011(() => muc.DanhDauLoi("X", "Y"));
    }

    // ── Hàng 9: ChoGui → HetHan khi quá ExpiresAtUtc ──────────────────────────────────────────

    [Fact]
    public void TC05_hang9_HetHanTuChoGui_qua_han()
    {
        var muc = NotificationTestData.ToChoGui(expiresAtUtc: NotificationTestData.Now.AddHours(1));

        var suKien = muc.HetHanTuChoGui(NotificationTestData.Now.AddHours(2));

        Assert.Equal(NotificationQueueEventType.Expired, suKien.EventType);
        Assert.Equal(NotificationQueueStatus.ChoGui, suKien.FromStatus);
        Assert.Equal(NotificationQueueStatus.HetHan, suKien.ToStatus);
        Assert.Equal(NotificationQueueStatus.HetHan, muc.Status);
    }

    [Fact]
    public void TC05_hang9_con_han_thi_NEM_0011()
    {
        var muc = NotificationTestData.ToChoGui(expiresAtUtc: NotificationTestData.Now.AddHours(1));

        Nem0011(() => muc.HetHanTuChoGui(NotificationTestData.Now));
    }

    [Fact]
    public void TC05_hang9_tin_khong_han_thi_NEM_0011()
    {
        var muc = NotificationTestData.ToChoGui(expiresAtUtc: null);

        Nem0011(() => muc.HetHanTuChoGui(NotificationTestData.Now.AddDays(30)));
    }

    // ── Hàng 10: Moi/ChoGui → DaHuy (Cancelled) ───────────────────────────────────────────────

    [Fact]
    public void TC05_hang10_Huy_tu_Moi_va_tu_ChoGui()
    {
        var tuMoi = NotificationTestData.QueueItem();
        var s1 = tuMoi.Huy("huy-tu-moi", Guid.NewGuid());
        Assert.Equal(NotificationQueueEventType.Cancelled, s1.EventType);
        Assert.Equal(NotificationQueueStatus.DaHuy, tuMoi.Status);
        Assert.Equal("huy-tu-moi", tuMoi.LastReasonMasked);

        var tuChoGui = NotificationTestData.ToChoGui();
        var s2 = tuChoGui.Huy("huy-tu-chogui", Guid.NewGuid());
        Assert.Equal(NotificationQueueEventType.Cancelled, s2.EventType);
        Assert.Equal(NotificationQueueStatus.DaHuy, tuChoGui.Status);
    }

    [Fact]
    public void TC05_hang10_huy_muc_da_hap_thu_thi_NEM_0011()
    {
        var muc = NotificationTestData.ToDaBanGiao();

        Nem0011(() => muc.Huy("muon", Guid.NewGuid()));
    }

    // ── Hàng 11: Loi (giữ nguyên) phát Replayed ───────────────────────────────────────────────

    [Fact]
    public void TC05_hang11_DanhDauPhatLai_Replayed_FromToLoi()
    {
        var muc = NotificationTestData.ToDangBanGiao();
        muc.DanhDauLoi("X", "Y");
        Assert.Equal(NotificationQueueStatus.Loi, muc.Status);

        var suKien = muc.DanhDauPhatLai(Guid.NewGuid());

        Assert.Equal(NotificationQueueEventType.Replayed, suKien.EventType);
        Assert.Equal(NotificationQueueStatus.Loi, suKien.FromStatus);
        Assert.Equal(NotificationQueueStatus.Loi, suKien.ToStatus);
        Assert.Equal(NotificationQueueStatus.Loi, muc.Status); // mục gốc Ở LẠI Loi
    }

    [Fact]
    public void TC05_hang11_phat_lai_muc_khong_phai_Loi_thi_NEM_0011()
    {
        var muc = NotificationTestData.QueueItem();

        Nem0011(() => muc.DanhDauPhatLai(Guid.NewGuid()));
    }

    // ── Hàng 12: trạng thái hấp thụ KHÔNG có cạnh ra ──────────────────────────────────────────

    [Fact]
    public void TC06_khong_lui_duoc_tu_DaBanGiao()
    {
        var muc = NotificationTestData.ToDaBanGiao();

        Nem0011(() => muc.DanhDauSanSang(NotificationTestData.Now));
        Nem0011(() => muc.GianhBanGiao(NotificationTestData.Now.AddSeconds(120)));
    }

    [Fact]
    public void TC05_hang12_moi_trang_thai_hap_thu_khong_co_canh_ra()
    {
        // DaHuy: không quay lại Moi/ChoGui.
        var daHuy = NotificationTestData.QueueItem();
        daHuy.Huy("x", Guid.NewGuid());
        Nem0011(() => daHuy.DanhDauSanSang(NotificationTestData.Now));

        // HetHan: không giành bàn giao lại.
        var hetHan = NotificationTestData.ToChoGui(expiresAtUtc: NotificationTestData.Now.AddHours(1));
        hetHan.HetHanTuChoGui(NotificationTestData.Now.AddHours(2));
        Nem0011(() => hetHan.GianhBanGiao(NotificationTestData.Now.AddSeconds(120)));

        // Loi: không đánh dấu đã bàn giao.
        var loi = NotificationTestData.ToDangBanGiao();
        loi.DanhDauLoi("X", "Y");
        Nem0011(() => loi.DanhDauSanSang(NotificationTestData.Now));
    }

    // ── Hàng 13: bị khử trùng ⇒ DaHuy + bucket NULL (Suppressed) ──────────────────────────────

    [Fact]
    public void TC05_hang13_GhiNhanBiKhudup_DaHuy_va_bucket_NULL()
    {
        var bucket = NotificationTestData.Now.AddMinutes(-1);
        var muc = NotificationTestData.QueueItem(coalesceBucketStartUtc: bucket);
        Assert.Equal(bucket, muc.CoalesceBucketStartUtc);

        var suKien = muc.GhiNhanBiKhudup("cua-so-khu-trung");

        Assert.Equal(NotificationQueueEventType.Suppressed, suKien.EventType);
        Assert.Equal(NotificationQueueStatus.Moi, suKien.FromStatus);
        Assert.Equal(NotificationQueueStatus.DaHuy, suKien.ToStatus);
        Assert.Equal(NotificationQueueStatus.DaHuy, muc.Status);
        Assert.Null(muc.CoalesceBucketStartUtc); // 🚨 ra ngoài partial UNIQUE index
        Assert.Equal("cua-so-khu-trung", muc.LastReasonMasked);
    }

    [Fact]
    public void TC05_hang13_khu_trung_tu_ChoGui_cung_hop_le()
    {
        var muc = NotificationTestData.ToChoGui();

        var suKien = muc.GhiNhanBiKhudup("cua-so");

        Assert.Equal(NotificationQueueStatus.ChoGui, suKien.FromStatus);
        Assert.Equal(NotificationQueueStatus.DaHuy, muc.Status);
        Assert.Null(muc.CoalesceBucketStartUtc);
    }

    // ── Hàng 14: DangBanGiao → ChoGui (RetryScheduled), AttemptCount++ ────────────────────────

    [Fact]
    public void TC05_hang14_tin_KHONG_HAN_van_thu_lai_va_tang_AttemptCount()
    {
        var muc = NotificationTestData.ToDangBanGiao(expiresAtUtc: null); // 🚨 ExpiresAtUtc IS NULL

        var suKien = muc.HoanLaiChoGui(NotificationTestData.Now.AddMinutes(5), "loi-tam-thoi");

        Assert.Equal(NotificationQueueEventType.RetryScheduled, suKien.EventType);
        Assert.Equal(NotificationQueueStatus.DangBanGiao, suKien.FromStatus);
        Assert.Equal(NotificationQueueStatus.ChoGui, suKien.ToStatus);
        Assert.Equal(NotificationQueueStatus.ChoGui, muc.Status);
        Assert.Equal(1, muc.AttemptCount);
        Assert.Equal("loi-tam-thoi", muc.LastErrorCode);
    }

    [Fact]
    public void TC05_hang14_con_han_van_thu_lai()
    {
        var muc = NotificationTestData.ToDangBanGiao(expiresAtUtc: NotificationTestData.Now.AddHours(1));

        var suKien = muc.HoanLaiChoGui(NotificationTestData.Now.AddMinutes(5), "timeout");

        Assert.Equal(NotificationQueueEventType.RetryScheduled, suKien.EventType);
        Assert.Equal(1, muc.AttemptCount);
    }

    [Fact]
    public void TC05_hang14_qua_han_thi_NEM_0011()
    {
        var muc = NotificationTestData.ToDangBanGiao(expiresAtUtc: NotificationTestData.Now.AddHours(1));

        Nem0011(() => muc.HoanLaiChoGui(NotificationTestData.Now.AddHours(2), "timeout"));
    }

    [Fact]
    public void TC05_hang14_sai_trang_thai_thi_NEM_0011()
    {
        var muc = NotificationTestData.ToChoGui();

        Nem0011(() => muc.HoanLaiChoGui(NotificationTestData.Now, "timeout"));
    }

    // ── Hàng 15: DangBanGiao → HetHan (Expired), AttemptCount KHÔNG tăng ──────────────────────

    [Fact]
    public void TC05_hang15_HetHanTuDangBanGiao_qua_han_khong_tang_AttemptCount()
    {
        var muc = NotificationTestData.ToDangBanGiao(expiresAtUtc: NotificationTestData.Now.AddHours(1));
        Assert.Equal(0, muc.AttemptCount);

        var suKien = muc.HetHanTuDangBanGiao(NotificationTestData.Now.AddHours(2));

        Assert.Equal(NotificationQueueEventType.Expired, suKien.EventType);
        Assert.Equal(NotificationQueueStatus.DangBanGiao, suKien.FromStatus);
        Assert.Equal(NotificationQueueStatus.HetHan, suKien.ToStatus);
        Assert.Equal(NotificationQueueStatus.HetHan, muc.Status);
        Assert.Equal(0, muc.AttemptCount); // 🚨 KHÔNG tăng
    }

    [Fact]
    public void TC05_hang15_con_han_thi_NEM_0011()
    {
        var muc = NotificationTestData.ToDangBanGiao(expiresAtUtc: NotificationTestData.Now.AddHours(1));

        Nem0011(() => muc.HetHanTuDangBanGiao(NotificationTestData.Now));
    }

    [Fact]
    public void TC05_hang15_tin_khong_han_thi_NEM_0011()
    {
        var muc = NotificationTestData.ToDangBanGiao(expiresAtUtc: null);

        Nem0011(() => muc.HetHanTuDangBanGiao(NotificationTestData.Now.AddDays(30)));
    }

    // ── Hàng 7 vs 14: phân biệt tăng AttemptCount ─────────────────────────────────────────────

    [Fact]
    public void TC05_hang7_va_hang14_khac_nhau_o_AttemptCount()
    {
        var hang7 = NotificationTestData.ToDangBanGiao(leaseExpiresAtUtc: NotificationTestData.Now.AddSeconds(10));
        hang7.KhoiPhucLease(NotificationTestData.Now.AddMinutes(1));

        var hang14 = NotificationTestData.ToDangBanGiao();
        hang14.HoanLaiChoGui(NotificationTestData.Now, "timeout");

        Assert.Equal(0, hang7.AttemptCount); // lease khôi phục KHÔNG tính là lần gửi
        Assert.Equal(1, hang14.AttemptCount); // lỗi tạm thời CÓ tính
    }

    // ── LaHapThu: bốn trạng thái kết thúc ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(NotificationQueueStatus.DaBanGiao, true)]
    [InlineData(NotificationQueueStatus.DaHuy, true)]
    [InlineData(NotificationQueueStatus.HetHan, true)]
    [InlineData(NotificationQueueStatus.Loi, true)]
    [InlineData(NotificationQueueStatus.Moi, false)]
    [InlineData(NotificationQueueStatus.ChoDuyet, false)]
    [InlineData(NotificationQueueStatus.ChoGui, false)]
    [InlineData(NotificationQueueStatus.DangBanGiao, false)]
    public void TC05_LaHapThu_dung_bon_trang_thai_ket_thuc(NotificationQueueStatus trangThai, bool mongDoi)
    {
        Assert.Equal(mongDoi, NotificationQueueItem.LaHapThu(trangThai));
    }
}
