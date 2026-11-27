using System.Diagnostics.Metrics;
using eCommerce.Kernel.Observability;
using eCommerce.Notification;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Timing;

namespace eCommerce.Tests.Notification;

/// <summary>
/// `NOT-002` (`WP-23`, `W23-2` F4 QA) — **test ĐƠN VỊ** cho `NotificationSender` + hợp đồng adapter kênh gửi,
/// theo `NOT-002-arch.md` §13.1.
///
/// Toàn bộ ca dùng **`fake` adapter + `fake` config provider + `fake` clock** ⇒ **0 kết nối mạng**, **0 DB**.
/// Mã ca giữ đúng mã của ARCH: `TC-EN-*`, `TC-FC-*`, `TC-MAP-*`, `TC-KEY-*`, `TC-LOG-*`, `TC-MET-*`.
///
/// 📌 Vì tầng này **không có DB/UoW** (`NotificationSender` ghi 0), phần "**0 `DaBanGiao`/`DaGui`**" của §6.1
/// được chứng minh bằng **0 lần gọi adapter** + `BusinessException(0003)` — không phải bằng đọc bảng.
/// Phần ghi trạng thái thật thuộc `NotificationDispatchWorker` (đã có test riêng) và DB thật (increment sau).
/// </summary>
public class NotificationChannelSenderTests
{
    private const string Ma0003 = "eCommerce:Notification:0003";

    // ── Hạ tầng giả ───────────────────────────────────────────────────────────────────────────

    private sealed class ClockGia : IClock
    {
        public DateTime Now { get; set; } = NotificationTestData.Now;
        public DateTimeKind Kind => DateTimeKind.Utc;
        public bool SupportsMultipleTimezone => false;
        public DateTime Normalize(DateTime dateTime) => dateTime;
        public DateTime ConvertToUserTime(DateTime dateTime) => dateTime;
        public DateTimeOffset ConvertToUserTime(DateTimeOffset dateTime) => dateTime;
        public DateTime ConvertToUtc(DateTime dateTime) => dateTime;
    }

    /// <summary>Adapter giả — đếm số lần bị gọi và giữ lại `SendCommand`; KHÔNG chạm mạng.</summary>
    private sealed class KenhGia : INotificationChannelSender
    {
        public string ProviderCode { get; init; } = "fake";
        public ChannelKind ChannelKind { get; init; } = ChannelKind.Email;
        public IReadOnlyCollection<string> RequiredSettingsKeys { get; init; } = [];

        public int SoLanGoi { get; private set; }
        public SendCommand? LenhNhan { get; private set; }

        /// <summary>Kết quả trả về; mặc định là happy path (hàng 1 §4.5).</summary>
        public SendOutcome? KetQua { get; set; }

        /// <summary>Nếu khác `null` ⇒ adapter NÉM (ca `TC-FC-07`: adapter tự ném `0003`).</summary>
        public Exception? NemRa { get; set; }

        public Task<SendOutcome> SendAsync(SendCommand command, CancellationToken ct = default)
        {
            SoLanGoi++;
            LenhNhan = command;

            if (NemRa is not null)
            {
                throw NemRa;
            }

            return Task.FromResult(KetQua ?? new SendOutcome(true, "msg-1", NotificationFailureKind.None, null));
        }
    }

    private sealed class CauHinhGia : INotificationChannelConfigProvider
    {
        public NotificationChannelConfig? CauHinh { get; set; }
        public int SoLanHoi { get; private set; }
        public ChannelKind? KenhDuocHoi { get; private set; }

        public Task<NotificationChannelConfig?> FindAsync(ChannelKind channel, CancellationToken ct = default)
        {
            SoLanHoi++;
            KenhDuocHoi = channel;
            return Task.FromResult(CauHinh);
        }
    }

    /// <summary>Sink log giả — giữ level + thông điệp đã format để kiểm `ErrorCode` và PII.</summary>
    private sealed class GhiLog<T> : ILogger<T>
    {
        public List<(LogLevel Cap, string ThongDiep)> Muc { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Muc.Add((logLevel, formatter(state, exception)));
    }

    /// <summary>Bắt số đo metric thật phát ra trên `Meter` của test (khuôn `ChannelTokenManagerMetricsTests`).</summary>
    private sealed class BoGhi : IDisposable
    {
        private readonly MeterListener _listener = new();

        public List<(string Ten, long GiaTri, Dictionary<string, object?> Nhan)> Ghi { get; } = [];

        public BoGhi(Meter meter)
        {
            _listener.InstrumentPublished = (instrument, l) =>
            {
                if (ReferenceEquals(instrument.Meter, meter))
                {
                    l.EnableMeasurementEvents(instrument);
                }
            };

            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            {
                var nhan = new Dictionary<string, object?>();
                foreach (var t in tags)
                {
                    nhan[t.Key] = t.Value;
                }

                Ghi.Add((instrument.Name, value, nhan));
            });

            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();
    }

    /// <summary>Bộ phụ thuộc dùng chung — dựng `NotificationSender` THẬT quanh hạ tầng giả.</summary>
    private sealed class Khung : IDisposable
    {
        public Meter Meter { get; } = new("eCommerce-Test-" + Guid.NewGuid());
        public eCommerceMetrics Metrics { get; }
        public CauHinhGia CauHinh { get; } = new();
        public List<KenhGia> Adapters { get; } = [];
        public GhiLog<NotificationSender> Log { get; } = new();
        public ClockGia DongHo { get; } = new();
        public NotificationChannelFactory Factory { get; }
        public NotificationSender Sender { get; }

        public Khung(params KenhGia[] adapters)
        {
            Metrics = new eCommerceMetrics(Meter);
            Adapters.AddRange(adapters);
            Factory = new NotificationChannelFactory(Adapters);
            Sender = new NotificationSender(CauHinh, Factory, DongHo, Metrics, Log);
        }

        public void Dispose() => Meter.Dispose();
    }

    private static NotificationChannelConfig CauHinh(
        ChannelKind channel = ChannelKind.Email,
        string providerCode = "fake",
        bool isEnabled = true,
        string? credential = "enc-credential",
        DateTime? expiresAtUtc = null,
        IReadOnlyDictionary<string, string>? settings = null,
        Guid? channelAccountId = null)
        => new(
            channelAccountId ?? Guid.NewGuid(),
            channel,
            providerCode,
            isEnabled,
            credential,
            expiresAtUtc,
            settings ?? new Dictionary<string, string>());

    private static NotificationSendRequest YeuCau(
        ChannelKind channel = ChannelKind.Email,
        string recipient = "enc-recipient",
        string subject = "subject",
        string body = "body",
        string? correlationId = "corr-1",
        int priority = NotificationPriorities.Normal)
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "idem-1",
            channel,
            recipient,
            subject,
            body,
            priority,
            correlationId);

    // ── TC-EN: kiểu/mã lỗi một nguồn ───────────────────────────────────────────────────────────

    [Fact]
    public void TC_EN_01_NotificationFailureKind_dung_5_gia_tri_0_den_4_append_only()
    {
        var giaTri = Enum.GetValues<NotificationFailureKind>();

        Assert.Equal(5, giaTri.Length);
        Assert.Equal(
            new[]
            {
                ("None", 0),
                ("Transient", 1),
                ("Permanent", 2),
                ("QuotaExceeded", 3),
                ("Unauthenticated", 4)
            },
            giaTri.Select(v => (v.ToString(), (int)v)).ToArray());

        // Append-only: giá trị là 0..4 liên tục, không có lỗ hổng/đánh số lại.
        Assert.Equal(Enumerable.Range(0, 5).ToArray(), giaTri.Select(v => (int)v).ToArray());
    }

    [Fact]
    public void TC_EN_02_ma_0002_0003_0006_dung_dinh_dang_va_giao_0010_0020_rong()
    {
        var ma = typeof(NotificationErrorCodes)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.Contains("eCommerce:Notification:0002", ma);
        Assert.Contains("eCommerce:Notification:0003", ma);
        Assert.Contains("eCommerce:Notification:0006", ma);

        Assert.All(ma, m => Assert.Matches(@"^eCommerce:Notification:\d{4}$", m));
        Assert.Equal(ma.Count, ma.Distinct(StringComparer.Ordinal).Count());

        var so = ma.Select(m => int.Parse(m[^4..], System.Globalization.CultureInfo.InvariantCulture)).ToHashSet();

        // 3 mã mới của NOT-002 KHÔNG chiếm dải NOT-001 (`0010–0020`).
        var maMoi = new[] { 2, 3, 6 };
        Assert.All(maMoi, m => Assert.Contains(m, so));
        Assert.Empty(maMoi.Intersect(Enumerable.Range(10, 11)));
    }

    [Fact]
    public void TC_EN_03_ProviderCodeToiDa_bang_32_khop_varchar32()
        => Assert.Equal(32, NotificationConsts.ProviderCodeToiDa);

    // ── TC-FC: 7 điều kiện fail-closed §6.1 ────────────────────────────────────────────────────

    [Fact]
    public async Task TC_FC_01_khong_co_cau_hinh_kenh_thi_NEM_0003_va_0_lan_goi_adapter()
    {
        using var k = new Khung(new KenhGia());
        k.CauHinh.CauHinh = null;

        var ex = await Assert.ThrowsAsync<BusinessException>(() => k.Sender.SendAsync(YeuCau()));

        Assert.Equal(Ma0003, ex.Code);
        Assert.Equal(0, k.Adapters[0].SoLanGoi);
        Assert.Equal(1, k.CauHinh.SoLanHoi);
    }

    [Fact]
    public async Task TC_FC_02_kenh_dang_tat_thi_NEM_0003_va_0_lan_goi_adapter()
    {
        using var k = new Khung(new KenhGia());
        k.CauHinh.CauHinh = CauHinh(isEnabled: false);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => k.Sender.SendAsync(YeuCau()));

        Assert.Equal(Ma0003, ex.Code);
        Assert.Equal(0, k.Adapters[0].SoLanGoi);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TC_FC_03_credential_rong_thi_NEM_0003_va_0_lan_goi_adapter(string? credential)
    {
        using var k = new Khung(new KenhGia());
        k.CauHinh.CauHinh = CauHinh(credential: credential);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => k.Sender.SendAsync(YeuCau()));

        Assert.Equal(Ma0003, ex.Code);
        Assert.Equal(0, k.Adapters[0].SoLanGoi);
    }

    [Fact]
    public async Task TC_FC_04_credential_het_han_bang_now_thi_NEM_0003_va_0_lan_goi_adapter()
    {
        using var k = new Khung(new KenhGia());
        k.CauHinh.CauHinh = CauHinh(expiresAtUtc: k.DongHo.Now); // biên `<= now`

        var ex = await Assert.ThrowsAsync<BusinessException>(() => k.Sender.SendAsync(YeuCau()));

        Assert.Equal(Ma0003, ex.Code);
        Assert.Equal(0, k.Adapters[0].SoLanGoi);
    }

    [Fact]
    public async Task TC_FC_05_khong_co_adapter_khop_kenh_provider_thi_NEM_0003_va_0_lan_goi_adapter()
    {
        var adapterKhac = new KenhGia { ProviderCode = "khac" };
        using var k = new Khung(adapterKhac);
        k.CauHinh.CauHinh = CauHinh(providerCode: "fake");

        var ex = await Assert.ThrowsAsync<BusinessException>(() => k.Sender.SendAsync(YeuCau()));

        Assert.Equal(Ma0003, ex.Code);
        Assert.Equal(0, adapterKhac.SoLanGoi);
    }

    [Fact]
    public async Task TC_FC_06_thieu_khoa_settings_bat_buoc_thi_NEM_0003_va_0_lan_goi_adapter()
    {
        var adapter = new KenhGia
        {
            RequiredSettingsKeys = ["apiBaseUrl", "unitCostPerMessage", "currency"]
        };
        using var k = new Khung(adapter);
        k.CauHinh.CauHinh = CauHinh(settings: new Dictionary<string, string>
        {
            ["apiBaseUrl"] = "https://fake.local"
            // thiếu `unitCostPerMessage` + `currency`
        });

        var ex = await Assert.ThrowsAsync<BusinessException>(() => k.Sender.SendAsync(YeuCau()));

        Assert.Equal(Ma0003, ex.Code);
        Assert.Equal(0, adapter.SoLanGoi);
    }

    [Fact]
    public async Task TC_FC_07_adapter_tu_nem_0003_thi_nguyen_0003_khong_bi_bien_thanh_ket_qua()
    {
        var adapter = new KenhGia
        {
            NemRa = new BusinessException(Ma0003).WithData("lyDo", "adapter-tu-kiem")
        };
        using var k = new Khung(adapter);
        k.CauHinh.CauHinh = CauHinh();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => k.Sender.SendAsync(YeuCau()));

        Assert.Equal(Ma0003, ex.Code);
        Assert.Equal(1, adapter.SoLanGoi); // adapter ĐÃ được gọi (nó tự kiểm rồi ném)
    }

    // ── TC-MAP: ánh xạ `SendOutcome` → `NotificationSendResult` (§4.5) ─────────────────────────

    private static async Task<NotificationSendResult> ChayVoiKetQuaAsync(SendOutcome outcome)
    {
        var adapter = new KenhGia { KetQua = outcome };
        using var k = new Khung(adapter);
        k.CauHinh.CauHinh = CauHinh();
        return await k.Sender.SendAsync(YeuCau());
    }

    [Fact]
    public async Task TC_MAP_01_hang_1_3_4_6_anh_xa_dung_ket_qua_canonical()
    {
        // Hàng 1: thành công có `ProviderMessageId`.
        var hang1 = await ChayVoiKetQuaAsync(new SendOutcome(true, "msg-1", NotificationFailureKind.None, null));
        Assert.Equal(new NotificationSendResult(true, false, false, null), hang1);

        // Hàng 3: lỗi tạm thời ⇒ `IsTransient = true`, mã "loi-tam-thoi".
        var hang3 = await ChayVoiKetQuaAsync(new SendOutcome(false, null, NotificationFailureKind.Transient, "timeout"));
        Assert.Equal(new NotificationSendResult(false, false, true, "loi-tam-thoi"), hang3);

        // Hàng 4: hết hạn mức ⇒ thử lại cửa sổ sau, mã "quota".
        var hang4 = await ChayVoiKetQuaAsync(new SendOutcome(false, null, NotificationFailureKind.QuotaExceeded, "het-quota"));
        Assert.Equal(new NotificationSendResult(false, false, true, "quota"), hang4);

        // Hàng 6: lỗi vĩnh viễn ⇒ `0002`, KHÔNG tái thử.
        var hang6 = await ChayVoiKetQuaAsync(new SendOutcome(false, null, NotificationFailureKind.Permanent, "sai-dinh-danh"));
        Assert.Equal(
            new NotificationSendResult(false, false, false, NotificationErrorCodes.NguoiNhanKhongHopLe),
            hang6);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task TC_MAP_02_accepted_true_thieu_ProviderMessageId_tra_0006_khong_nem(string? providerMessageId)
    {
        var ketQua = await ChayVoiKetQuaAsync(
            new SendOutcome(true, providerMessageId, NotificationFailureKind.None, null));

        Assert.Equal(
            new NotificationSendResult(false, false, false, NotificationErrorCodes.KhongCoProviderMessageId),
            ketQua);
    }

    [Fact]
    public async Task TC_MAP_03_vi_pham_hop_dong_thi_NEM_InvalidOperationException()
    {
        // `Accepted=false` + `FailureKind=None` ⇒ mâu thuẫn.
        var sai1 = await Assert.ThrowsAsync<InvalidOperationException>(() => ChayVoiKetQuaAsync(
            new SendOutcome(false, null, NotificationFailureKind.None, null)));
        Assert.Contains("Accepted=false", sai1.Message, StringComparison.Ordinal);

        // `Accepted=true` + `FailureKind≠None` ⇒ mâu thuẫn.
        var sai2 = await Assert.ThrowsAsync<InvalidOperationException>(() => ChayVoiKetQuaAsync(
            new SendOutcome(true, "msg-1", NotificationFailureKind.Transient, "timeout")));
        Assert.Contains("Accepted=true", sai2.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TC_MAP_04_unauthenticated_thi_NEM_0003_va_adapter_duoc_goi_dung_mot_lan()
    {
        var adapter = new KenhGia
        {
            KetQua = new SendOutcome(false, null, NotificationFailureKind.Unauthenticated, "credential-bi-tu-choi")
        };
        using var k = new Khung(adapter);
        k.CauHinh.CauHinh = CauHinh();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => k.Sender.SendAsync(YeuCau()));

        Assert.Equal(Ma0003, ex.Code);
        Assert.Equal(1, adapter.SoLanGoi);
    }

    // ── TC-KEY: trường canonical đi NGUYÊN VĂN sang `SendCommand` ──────────────────────────────

    [Fact]
    public async Task TC_KEY_01_khoa_va_noi_dung_di_nguyen_van_sang_SendCommand()
    {
        var adapter = new KenhGia { RequiredSettingsKeys = ["khoa-bat-buoc"] };
        using var k = new Khung(adapter);

        var settings = new Dictionary<string, string> { ["khoa-bat-buoc"] = "gia-tri" };
        var config = CauHinh(settings: settings, channelAccountId: Guid.NewGuid());
        k.CauHinh.CauHinh = config;

        var request = YeuCau(
            recipient: "recipient-ciphertext-nguyen-van",
            subject: "subject-nguyen-van",
            body: "body-nguyen-van",
            correlationId: "corr-nguyen-van",
            priority: NotificationPriorities.High);

        await k.Sender.SendAsync(request);

        var lenh = adapter.LenhNhan;
        Assert.NotNull(lenh);

        Assert.Equal(request.TenantId, lenh!.TenantId);
        Assert.Equal(request.QueueItemId, lenh.QueueItemId);
        Assert.Equal(request.IdempotencyKey, lenh.IdempotencyKey);
        Assert.Equal(request.ChannelKind, lenh.ChannelKind);
        Assert.Equal(request.RecipientEncrypted, lenh.RecipientEncrypted);
        Assert.Equal(request.Subject, lenh.Subject);
        Assert.Equal(request.Body, lenh.Body);
        Assert.Equal(request.Priority, lenh.Priority);
        Assert.Equal(request.CorrelationId, lenh.CorrelationId);

        // Lấy từ cấu hình — credential CHỈ ciphertext, không giải mã ở sender.
        Assert.Equal("fake", lenh.ProviderCode);
        Assert.Equal(config.ChannelAccountId, lenh.ChannelAccountId);
        Assert.Equal("enc-credential", lenh.CredentialEncrypted);
        Assert.Equal("gia-tri", lenh.Settings["khoa-bat-buoc"]);
    }

    // ── TC-LOG: log Warning có mã lỗi, 0 PII ───────────────────────────────────────────────────

    [Fact]
    public async Task TC_LOG_01_fail_closed_phat_Warning_co_ErrorCode_va_0_PII()
    {
        var adapter = new KenhGia { RequiredSettingsKeys = ["apiBaseUrl"] }; // settings rỗng ⇒ fail-closed
        using var k = new Khung(adapter);
        k.CauHinh.CauHinh = CauHinh(
            credential: "CREDENTIAL-CIPHER-999",
            settings: new Dictionary<string, string>());

        var request = YeuCau(
            recipient: "RECIPIENT-CIPHER-888",
            subject: "SUBJECT-PII-777",
            body: "BODY-PII-666");

        var ex = await Assert.ThrowsAsync<BusinessException>(() => k.Sender.SendAsync(request));
        Assert.Equal(Ma0003, ex.Code);

        var dong = Assert.Single(k.Log.Muc);
        Assert.Equal(LogLevel.Warning, dong.Cap);
        Assert.Contains(Ma0003, dong.ThongDiep, StringComparison.Ordinal);

        foreach (var pii in new[] { "CREDENTIAL-CIPHER-999", "RECIPIENT-CIPHER-888", "SUBJECT-PII-777", "BODY-PII-666" })
        {
            Assert.DoesNotContain(pii, dong.ThongDiep, StringComparison.Ordinal);
        }

        // `WithData` cũng chỉ chứa kênh/provider — không credential/recipient/nội dung.
        var data = string.Join("|", ex.Data.Values.Cast<object?>().Select(v => v?.ToString()));
        foreach (var pii in new[] { "CREDENTIAL-CIPHER-999", "RECIPIENT-CIPHER-888", "SUBJECT-PII-777", "BODY-PII-666" })
        {
            Assert.DoesNotContain(pii, data, StringComparison.Ordinal);
        }
    }

    // ── TC-MET: metric `notification_queue_failed_total{channel,reason=0003}` ───────────────────

    [Fact]
    public async Task TC_MET_01_fail_closed_tang_metric_queue_failed_reason_0003()
    {
        using var k = new Khung(new KenhGia());
        using var ghi = new BoGhi(k.Meter);
        k.CauHinh.CauHinh = null;

        await Assert.ThrowsAsync<BusinessException>(() => k.Sender.SendAsync(YeuCau()));

        var doDuoc = Assert.Single(
            ghi.Ghi, x => x.Ten == eCommerceMetricNames.NotificationQueueFailedTotal);

        Assert.Equal(1L, doDuoc.GiaTri);
        Assert.Equal("email", doDuoc.Nhan[eCommerceMetricTags.Channel]);
        Assert.Equal(Ma0003, doDuoc.Nhan[eCommerceMetricTags.Reason]);

        // Nhãn hữu hạn (`XC-005` BR-009) — không PII/cardinality cao.
        Assert.All(doDuoc.Nhan.Keys, khoa => Assert.Contains(khoa, eCommerceMetricTags.Allowed));
        Assert.DoesNotContain(doDuoc.Nhan.Values, v => v?.ToString() is "enc-recipient" or "enc-credential");
    }
}
