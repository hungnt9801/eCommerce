using System.Diagnostics.Metrics;
using eCommerce.Kernel.Observability;
using eCommerce.Messaging;
using eCommerce.Messaging.Idempotency;
using Volo.Abp;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Timing;

namespace eCommerce.Tests.Messaging;

/// <summary>
/// Test cho **`IIdempotencyGuard`** — hợp đồng chống trùng ở MỌI biên vào (`XC-002`, mục (a) của GĐ0).
///
/// 🎯 Đây là lớp chặn những **lỗi im lặng** tốn tiền nhất của hệ thống thương mại điện tử:
///   • webhook bị gửi lại ⇒ **trừ kho hai lần / ghi nhận thanh toán hai lần**;
///   • **cùng khoá nhưng payload khác** (đối tác sửa nội dung) ⇒ nếu im lặng trả kết quả cũ thì
///     yêu cầu **mới** bị coi là đã xử lý;
///   • hai yêu cầu **đồng thời** cùng khoá ⇒ nếu không tuần tự hoá thì cả hai đều chạy.
/// </summary>
public class IdempotencyGuardTests
{
    // ── Test double ─────────────────────────────────────────────────────────

    /// <summary>Khoá phân tán giả: giữ theo tên, có công tắc ép "luôn bận".</summary>
    private sealed class KhoaPhanTanGia : IAbpDistributedLock
    {
        private readonly HashSet<string> _dangGiu = new(StringComparer.Ordinal);
        private readonly Lock _cong = new();

        public bool LuonBan { get; set; }

        public List<string> TenDaXin { get; } = [];

        public Task<IAbpDistributedLockHandle?> TryAcquireAsync(
            string name, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            lock (_cong)
            {
                TenDaXin.Add(name);

                if (LuonBan)
                {
                    return Task.FromResult<IAbpDistributedLockHandle?>(null);
                }

                if (!_dangGiu.Add(name))
                {
                    return Task.FromResult<IAbpDistributedLockHandle?>(null);
                }
            }

            return Task.FromResult<IAbpDistributedLockHandle?>(new Handle(() =>
            {
                lock (_cong)
                {
                    _dangGiu.Remove(name);
                }
            }));
        }

        private sealed class Handle : IAbpDistributedLockHandle
        {
            private readonly Action _giaiPhong;

            public Handle(Action giaiPhong) => _giaiPhong = giaiPhong;

            public ValueTask DisposeAsync()
            {
                _giaiPhong();
                return ValueTask.CompletedTask;
            }
        }
    }

    private static (IdempotencyGuard Guard, InMemoryIdempotencyStore Store, KhoaPhanTanGia Khoa, IClock DongHo) TaoGuard()
    {
        var store = new InMemoryIdempotencyStore();
        var khoa = new KhoaPhanTanGia();
        var dongHo = new DongHoGia();
        return (new IdempotencyGuard(store, khoa, dongHo), store, khoa, dongHo);
    }

    private sealed class DongHoGia : IClock
    {
        public DateTime Now { get; set; } = new(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc);

        public DateTimeKind Kind => DateTimeKind.Utc;

        public bool SupportsMultipleTimezone => false;

        public DateTime Normalize(DateTime dateTime) => dateTime;

        public DateTime ConvertToUserTime(DateTime dateTime) => dateTime;

        public DateTimeOffset ConvertToUserTime(DateTimeOffset dateTime) => dateTime;

        public DateTime ConvertToUtc(DateTime dateTime) => dateTime;
    }

    private static IdempotencyKey Khoa(string value = "GD-0001") => IdempotencyKey.Cua("vnpay:ipn", value);

    // ── Luồng cơ bản ────────────────────────────────────────────────────────

    [Fact]
    public async Task Lan_dau_thi_THUC_THI_va_luu_ket_qua()
    {
        var (guard, _, _, _) = TaoGuard();
        var soLanChay = 0;

        var kq = await guard.ThucThiAsync(Khoa(), """{"amount":100}""", _ =>
        {
            soLanChay++;
            return Task.FromResult<string?>("""{"ok":true}""");
        });

        Assert.Equal(IdempotencyOutcomeKind.DaThucThi, kq.Kind);
        Assert.Equal("""{"ok":true}""", kq.Result);
        Assert.Equal(1, soLanChay);
        Assert.True(kq.DaChayXuLy);
    }

    [Fact]
    public async Task Goi_lai_CUNG_khoa_CUNG_payload_thi_tra_ket_qua_cu_va_KHONG_chay_lai()
    {
        // 🚨 Đây chính là ca webhook bị đối tác gửi lại. Nếu chạy lại ⇒ trừ kho hai lần.
        var (guard, _, _, _) = TaoGuard();
        var soLanChay = 0;

        Task<string?> XuLy(CancellationToken _)
        {
            soLanChay++;
            return Task.FromResult<string?>("KET-QUA");
        }

        var lan1 = await guard.ThucThiAsync(Khoa(), """{"amount":100}""", XuLy);
        var lan2 = await guard.ThucThiAsync(Khoa(), """{"amount":100}""", XuLy);

        Assert.Equal(IdempotencyOutcomeKind.DaThucThi, lan1.Kind);
        Assert.Equal(IdempotencyOutcomeKind.TraVeKetQuaCu, lan2.Kind);
        Assert.Equal("KET-QUA", lan2.Result);
        Assert.Equal(1, soLanChay);            // ⬅️ CHỈ chạy MỘT lần
        Assert.True(lan2.LaPhatLai);
    }

    [Fact]
    public async Task CUNG_khoa_KHAC_payload_thi_XUNG_DOT_chu_khong_im_lang_tra_ket_qua_cu()
    {
        var (guard, _, _, _) = TaoGuard();

        await guard.ThucThiAsync(Khoa(), """{"amount":100}""", _ => Task.FromResult<string?>("A"));

        var kq = await guard.ThucThiAsync(Khoa(), """{"amount":999}""", _ => Task.FromResult<string?>("B"));

        Assert.Equal(IdempotencyOutcomeKind.XungDotPayload, kq.Kind);
        Assert.Equal(MessagingErrorCodes.PayloadConflict, kq.ErrorCode);
        Assert.Null(kq.Result);                // ⬅️ KHÔNG trả kết quả cũ
        Assert.True(kq.LaLoi);
    }

    [Fact]
    public async Task Cung_gia_tri_nhung_KHAC_scope_thi_la_HAI_yeu_cau_doc_lap()
    {
        // "12345" của Shopee KHÁC "12345" của VNPay — nếu gộp thì đơn của hệ thống này
        // bị coi là đã xử lý bởi hệ thống khác (lỗi im lặng).
        var (guard, _, _, _) = TaoGuard();
        var soLanChay = 0;

        Task<string?> XuLy(CancellationToken _)
        {
            soLanChay++;
            return Task.FromResult<string?>("KQ");
        }

        var a = await guard.ThucThiAsync(IdempotencyKey.Cua("shopee:order", "12345"), """{"x":1}""", XuLy);
        var b = await guard.ThucThiAsync(IdempotencyKey.Cua("vnpay:ipn", "12345"), """{"x":1}""", XuLy);

        Assert.Equal(IdempotencyOutcomeKind.DaThucThi, a.Kind);
        Assert.Equal(IdempotencyOutcomeKind.DaThucThi, b.Kind);
        Assert.Equal(2, soLanChay);
    }

    // ── Khoá phân tán ───────────────────────────────────────────────────────

    [Fact]
    public async Task Khong_gianh_duoc_khoa_thi_tra_DANG_XU_LY_va_KHONG_chay_xu_ly()
    {
        // 🚨 Ca hai yêu cầu ĐỒNG THỜI. Nếu vẫn chạy xử lý ⇒ trừ kho hai lần.
        var (guard, _, khoa, _) = TaoGuard();
        khoa.LuonBan = true;
        var daChay = false;

        var kq = await guard.ThucThiAsync(Khoa(), "{}", _ =>
        {
            daChay = true;
            return Task.FromResult<string?>("X");
        });

        Assert.Equal(IdempotencyOutcomeKind.DangXuLy, kq.Kind);
        Assert.Equal(MessagingErrorCodes.DangXuLy, kq.ErrorCode);
        Assert.False(daChay);
    }

    [Fact]
    public void Khoa_lock_KHONG_chua_tien_to_ecommerce_de_tranh_TIEN_TO_DOI()
    {
        // 🔒 Liên quan trực tiếp lỗi C13 đã sửa: tiền tố do AbpDistributedLockOptions.KeyPrefix cấp.
        var ten = Khoa().KhoaLock();

        Assert.False(ten.StartsWith("ecommerce:", StringComparison.OrdinalIgnoreCase),
            $"Khoá phải KHÔNG chứa tiền tố (KeyPrefix đã là 'ecommerce:') — nếu không sẽ thành 'ecommerce:{ten}'");
        Assert.StartsWith("messaging:idempotency:", ten, StringComparison.Ordinal);
    }

    // ── Thất bại & thử lại ──────────────────────────────────────────────────

    [Fact]
    public async Task Xu_ly_LOI_thi_ghi_ThatBai_va_NEM_LAI_loi()
    {
        var (guard, store, _, _) = TaoGuard();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            guard.ThucThiAsync(Khoa(), "{}", _ => throw new InvalidOperationException("lỗi mô phỏng")));

        var banGhi = await store.TimAsync(Khoa());
        Assert.NotNull(banGhi);
        Assert.Equal(IdempotencyState.ThatBai, banGhi.State);
        Assert.Contains("InvalidOperationException", banGhi.ErrorCode);
    }

    [Fact]
    public async Task Sau_ThatBai_thi_cho_THU_LAI_va_lan_sau_chay_that()
    {
        var (guard, _, _, _) = TaoGuard();
        var soLanChay = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            guard.ThucThiAsync(Khoa(), "{}", _ =>
            {
                soLanChay++;
                throw new InvalidOperationException("lần 1 hỏng");
            }));

        var lan2 = await guard.ThucThiAsync(Khoa(), "{}", _ =>
        {
            soLanChay++;
            return Task.FromResult<string?>("OK");
        });

        Assert.Equal(IdempotencyOutcomeKind.DaThucThi, lan2.Kind);
        Assert.Equal("OK", lan2.Result);
        Assert.Equal(2, soLanChay);
    }

    [Fact]
    public async Task Ban_ghi_HET_HAN_thi_duoc_dung_lai_voi_payload_moi_khong_bao_xung_dot()
    {
        var (guard, store, _, dongHo) = TaoGuard();

        await guard.ThucThiAsync(Khoa(), """{"v":1}""", _ => Task.FromResult<string?>("cu"));

        // Đẩy đồng hồ qua mốc hết hạn (mặc định 7 ngày).
        ((DongHoGia)dongHo).Now = ((DongHoGia)dongHo).Now.AddDays(8);

        var kq = await guard.ThucThiAsync(Khoa(), """{"v":2}""", _ => Task.FromResult<string?>("moi"));

        Assert.Equal(IdempotencyOutcomeKind.DaThucThi, kq.Kind);   // KHÔNG báo xung đột
        Assert.Equal("moi", kq.Result);

        var banGhi = await store.TimAsync(Khoa());
        Assert.Equal("moi", banGhi!.Result);
    }

    [Fact]
    public async Task Xoa_ban_ghi_het_han_tra_ve_so_da_xoa()
    {
        var (guard, store, _, dongHo) = TaoGuard();

        await guard.ThucThiAsync(Khoa("A"), "{}", _ => Task.FromResult<string?>("x"));

        var soXoa = await store.XoaHetHanAsync(((DongHoGia)dongHo).Now.AddDays(8), 10);

        Assert.Equal(1, soXoa);
    }

    // ── 📊 KIỂM CHỨNG ĐIỂM PHÁT METRIC THẬT ────────────────────────────────

    [Fact]
    public async Task Xu_ly_loi_thi_PHAT_metric_messaging_inbox_failed_total()
    {
        // 🎯 Test này là BẰNG CHỨNG cho việc sửa LỖI TẦNG (XC-005 §16): trước đây 12 metric đã đăng ký
        // nhưng KHÔNG nơi nào phát được, vì registry nằm ở tầng APP mà quy ước #3 CẤM module tham chiếu app.
        // Sau khi chuyển sang shared/eCommerce.Kernel, module Messaging phát được metric thật.
        using var meter = new Meter("eCommerce-Test-" + Guid.NewGuid());
        var metrics = new eCommerceMetrics(meter);

        var ghiDuoc = new List<(string Ten, long GiaTri, string? Module)>();
        using var listener = new MeterListener();

        listener.InstrumentPublished = (instrument, l) =>
        {
            if (ReferenceEquals(instrument.Meter, meter))
            {
                l.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            string? module = null;
            foreach (var t in tags)
            {
                if (t.Key == eCommerceMetricTags.Module)
                {
                    module = t.Value?.ToString();
                }
            }

            ghiDuoc.Add((instrument.Name, value, module));
        });

        listener.Start();

        var guard = new IdempotencyGuard(
            new InMemoryIdempotencyStore(), new KhoaPhanTanGia(), new DongHoGia(), metrics);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            guard.ThucThiAsync(Khoa(), "{}", _ => throw new InvalidOperationException("hỏng")));

        var doDuoc = Assert.Single(ghiDuoc, x => x.Ten == eCommerceMetricNames.MessagingInboxFailedTotal);
        Assert.Equal(1L, doDuoc.GiaTri);
        Assert.Equal("messaging", doDuoc.Module);
    }

    [Fact]
    public async Task Xu_ly_THANH_CONG_thi_KHONG_phat_metric_loi()
    {
        // Đối chứng: đường thành công KHÔNG được phát metric lỗi (nếu phát thì cảnh báo sẽ nhiễu vô ích).
        using var meter = new Meter("eCommerce-Test-" + Guid.NewGuid());
        var metrics = new eCommerceMetrics(meter);

        var dem = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (ReferenceEquals(instrument.Meter, meter))
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((i, _, _, _) => Interlocked.Increment(ref dem));
        listener.Start();

        var guard = new IdempotencyGuard(
            new InMemoryIdempotencyStore(), new KhoaPhanTanGia(), new DongHoGia(), metrics);

        await guard.ThucThiAsync(Khoa(), "{}", _ => Task.FromResult<string?>("OK"));

        Assert.Equal(0, dem);
    }
}
