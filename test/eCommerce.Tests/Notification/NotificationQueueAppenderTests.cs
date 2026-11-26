using System.Diagnostics.Metrics;
using System.Reflection;
using eCommerce.Kernel.Observability;
using eCommerce.Notification;
using eCommerce.Notification.Events;
using eCommerce.Tests.Pricing.HaTang;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp;
using Volo.Abp.EventBus;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Guids;
using Volo.Abp.Security.Encryption;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace eCommerce.Tests.Notification;

/// <summary>
/// `TC-01`/`TC-03`/`TC-10` + hàng 2/4 của máy trạng thái — **cổng xếp hàng** `NotificationQueueAppender`
/// (`SRS §3` hàng 1–2, `ARCH §3.4`).
///
/// 🔒 Trùng khoá **cùng payload** ⇒ `AlreadyQueued = true` (**không** ném, **không** ghi lần hai);
/// payload **khác** ⇒ **NÉM** `0010`; mục có `BatchId` ⇒ `ChoDuyet` + event `QueuedForApproval`; khoá
/// idempotency truyền nguyên vẹn sang mục và sang `INotificationSender`.
///
/// 📌 Dựng `NotificationQueueAppender` bằng `new` + `RepoGia&lt;T&gt;` in-memory (`D-229`), **KHÔNG** host ABP,
/// **KHÔNG** PostgreSQL. Luồng **2 job song song / 1.000 lần đua** là **Integration** (W22-4) — ở đây chỉ tầng
/// đơn vị. `TC-01` "4 event" được kiểm bằng **bộ chạy mô phỏng** ba pha của job (mục §7.2) trên **entity thật**
/// + `INotificationSender` giả — không viết lại luật của worker.
/// </summary>
public class NotificationQueueAppenderTests
{
    // ── Bộ đo metric thật (khuôn `DeadLetterPhatLaiTests.BoGhi`) ───────────────────────────────

    private sealed class BoGhi : IDisposable
    {
        private readonly MeterListener _listener = new();

        public List<(string Ten, long GiaTri)> Ghi { get; } = [];

        public BoGhi(Meter meter)
        {
            _listener.InstrumentPublished = (instrument, l) =>
            {
                if (ReferenceEquals(instrument.Meter, meter))
                {
                    l.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
                Ghi.Add((instrument.Name, value)));
            _listener.Start();
        }

        public long Tong(string ten) => Ghi.Where(x => x.Ten == ten).Sum(x => x.GiaTri);

        public void Dispose() => _listener.Dispose();
    }

    // ── Khung kiểm thử: appender + 4 repo + phụ thuộc giả ─────────────────────────────────────

    private sealed class BoKiemThu : IDisposable
    {
        private readonly Meter _meter = new("notification-test-" + Guid.NewGuid());

        public BoKiemThu()
        {
            BoGhi = new BoGhi(_meter);
            Metrics = new eCommerceMetrics(_meter);
            Appender = new NotificationQueueAppender(
                Items, Events, Templates, Versions,
                new NotificationRenderer(), new NotificationMoneyFormatter(),
                Encryption, IdGen, Clock, Uow, Bus, Metrics,
                NullLogger<NotificationQueueAppender>.Instance);
        }

        public RepoGia<NotificationQueueItem> Items { get; } = new();

        public RepoGia<NotificationQueueEvent> Events { get; } = new();

        public RepoGia<NotificationTemplate> Templates { get; } = new();

        public RepoGia<NotificationTemplateVersion> Versions { get; } = new();

        public UowManagerGia Uow { get; } = new();

        public BusGia Bus { get; } = new();

        public SenderGia Sender { get; } = new();

        public ClockGia Clock { get; } = new() { Now = NotificationTestData.Now };

        public GuidGia IdGen { get; } = new();

        public EncryptGia Encryption { get; } = new();

        public eCommerceMetrics Metrics { get; }

        public BoGhi BoGhi { get; }

        public NotificationQueueAppender Appender { get; }

        /// <summary>Seed template `Published` + version ghim 1.</summary>
        public (NotificationTemplate Template, NotificationTemplateVersion Version) SeedTemplate(
            string code = "tpl",
            NotificationPurpose purpose = NotificationPurpose.Transactional,
            int coalesceWindowSeconds = 0,
            string subject = "Xin chào {{Ten}}",
            string body = "Nội dung {{Ten}}")
        {
            var template = NotificationTestData.Template(code, purpose, coalesceWindowSeconds);
            var version = NotificationTestData.Version(template.Id, subject: subject, bodyText: body);
            template.PublishVersion(version.Id);

            Templates.Them(template);
            Versions.Them(version);
            return (template, version);
        }

        public void Dispose() => _meter.Dispose();
    }

    private static EnqueueRequest YeuCau(
        string key = "order:1:shipped",
        string code = "tpl",
        IReadOnlyDictionary<string, string>? data = null,
        Guid? batchId = null)
        => new(
            TenantId: null,
            IdempotencyKey: key,
            TemplateCode: code,
            ChannelKind: ChannelKind.Email,
            Locale: "vi",
            Recipient: "a@b.com",
            Data: data ?? new Dictionary<string, string>(StringComparer.Ordinal) { ["Ten"] = "A" },
            Priority: NotificationPriorities.Normal,
            ScheduledAtUtc: NotificationTestData.Now,
            BatchId: batchId,
            CorrelationId: "corr-1");

    // ── TC-01 (phần appender): happy path ─────────────────────────────────────────────────────

    [Fact]
    public async Task TC01_enqueue_happy_path_tao_muc_Moi_va_event_Enqueued()
    {
        using var bo = new BoKiemThu();
        bo.SeedTemplate();

        var ketQua = await bo.Appender.EnqueueAsync(YeuCau(key: "order:1:shipped"));

        Assert.False(ketQua.AlreadyQueued);
        Assert.Equal(nameof(NotificationQueueStatus.Moi), ketQua.Status);

        var muc = Assert.Single(bo.Items.TatCa);
        Assert.Equal(NotificationQueueStatus.Moi, muc.Status);
        Assert.Equal("order:1:shipped", muc.IdempotencyKey);

        var suKien = Assert.Single(bo.Events.TatCa);
        Assert.Equal(NotificationQueueEventType.Enqueued, suKien.EventType);
        Assert.Null(suKien.FromStatus);
        Assert.Equal(NotificationQueueStatus.Moi, suKien.ToStatus);

        Assert.Single(bo.Bus.DaPhat);
        Assert.IsType<NotificationEnqueuedEto>(bo.Bus.DaPhat[0]);

        Assert.Equal(1, bo.BoGhi.Tong(eCommerceMetricNames.NotificationQueuedTotal));
    }

    // ── TC-03: trùng khoá ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TC03_trung_khoa_CUNG_payload_tra_AlreadyQueued_khong_ghi_lan_hai()
    {
        using var bo = new BoKiemThu();
        bo.SeedTemplate();

        var lan1 = await bo.Appender.EnqueueAsync(YeuCau(key: "order:1:shipped"));
        var lan2 = await bo.Appender.EnqueueAsync(YeuCau(key: "order:1:shipped"));

        Assert.False(lan1.AlreadyQueued);
        Assert.True(lan2.AlreadyQueued);
        Assert.Equal(lan1.QueueItemId, lan2.QueueItemId);
        Assert.Single(bo.Items.TatCa);
        Assert.Single(bo.Events.TatCa); // KHÔNG có event thứ hai
    }

    [Fact]
    public async Task TC03_trung_khoa_KHAC_payload_thi_NEM_0010()
    {
        using var bo = new BoKiemThu();
        bo.SeedTemplate();

        await bo.Appender.EnqueueAsync(YeuCau(key: "order:1:shipped"));

        var duLieuKhac = new Dictionary<string, string>(StringComparer.Ordinal) { ["Ten"] = "B" };

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => bo.Appender.EnqueueAsync(YeuCau(key: "order:1:shipped", data: duLieuKhac)));

        Assert.Equal(NotificationErrorCodes.KhoaXungDot, ex.Code);
        Assert.Single(bo.Items.TatCa); // vẫn chỉ một mục
    }

    [Fact]
    public async Task TC03_thieu_khoa_idempotency_thi_NEM_0010_truoc_moi_ghi()
    {
        using var bo = new BoKiemThu();
        bo.SeedTemplate();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => bo.Appender.EnqueueAsync(YeuCau(key: "   ")));

        Assert.Equal(NotificationErrorCodes.KhoaXungDot, ex.Code);
        Assert.Empty(bo.Items.TatCa);
        Assert.Empty(bo.Events.TatCa);
    }

    // ── TC-10: khoá idempotency nguyên vẹn + truyền sang sender ──────────────────────────────

    [Fact]
    public async Task TC10_khoa_idempotency_nguyen_ven_sang_muc_va_sender()
    {
        using var bo = new BoKiemThu();
        bo.SeedTemplate();

        var ketQua = await bo.Appender.EnqueueAsync(YeuCau(key: "order:O-1:shipped:sms"));
        var muc = Assert.Single(bo.Items.TatCa);

        Assert.Equal("order:O-1:shipped:sms", muc.IdempotencyKey);

        // Bộ chạy mô phỏng 3 pha: sẵn sàng → giành → gọi sender → đánh dấu bàn giao.
        muc.DanhDauSanSang(NotificationTestData.Now);
        muc.GianhBanGiao(NotificationTestData.Now.AddSeconds(120));

        var yeuCauGui = new NotificationSendRequest(
            muc.TenantId, muc.Id, muc.IdempotencyKey, muc.ChannelKind,
            muc.RecipientEncrypted, muc.RenderedSubject ?? string.Empty,
            muc.RenderedBodyMasked ?? string.Empty, muc.Priority);

        var ketQuaGui = await bo.Sender.SendAsync(yeuCauGui);

        Assert.True(ketQuaGui.Accepted);
        Assert.Equal(1, bo.Sender.SoLanGui);
        Assert.Equal(ketQua.QueueItemId, bo.Sender.LanCuoi!.QueueItemId);
        Assert.Equal("order:O-1:shipped:sms", bo.Sender.LanCuoi.IdempotencyKey);
    }

    // ── TC-01 (bộ chạy mô phỏng): đủ 4 event Enqueued → ReadyToDispatch → Dispatching → HandedOff

    [Fact]
    public async Task TC01_happy_path_du_bon_event_va_mot_lan_goi_sender()
    {
        using var bo = new BoKiemThu();
        bo.SeedTemplate();

        await bo.Appender.EnqueueAsync(YeuCau(key: "order:1:shipped"));

        var muc = Assert.Single(bo.Items.TatCa);
        var cacEvent = new List<NotificationQueueEventType>
        {
            Assert.Single(bo.Events.TatCa).EventType // Enqueued (do appender ghi)
        };

        // Pha (C1): Moi → ChoGui.
        var suKienSanSang = muc.DanhDauSanSang(NotificationTestData.Now);
        cacEvent.Add(suKienSanSang.EventType);
        Assert.Equal(NotificationQueueStatus.ChoGui, muc.Status);

        // Pha (C2a): ChoGui → DangBanGiao (trạng thái TRUNG GIAN phải tồn tại).
        var suKienGianh = muc.GianhBanGiao(NotificationTestData.Now.AddSeconds(120));
        cacEvent.Add(suKienGianh.EventType);
        Assert.Equal(NotificationQueueStatus.DangBanGiao, muc.Status);

        // Pha (C2b): gọi sender THẬT SỰ một lần.
        var yeuCauGui = new NotificationSendRequest(
            muc.TenantId, muc.Id, muc.IdempotencyKey, muc.ChannelKind,
            muc.RecipientEncrypted, muc.RenderedSubject ?? string.Empty,
            muc.RenderedBodyMasked ?? string.Empty, muc.Priority);
        var ketQuaGui = await bo.Sender.SendAsync(yeuCauGui);
        Assert.True(ketQuaGui.Accepted);

        // Pha (C3): DangBanGiao → DaBanGiao.
        var suKienBanGiao = muc.DanhDauDaBanGiao(NotificationTestData.Now);
        cacEvent.Add(suKienBanGiao.EventType);

        Assert.Equal(
            new[]
            {
                NotificationQueueEventType.Enqueued,
                NotificationQueueEventType.ReadyToDispatch,
                NotificationQueueEventType.Dispatching,
                NotificationQueueEventType.HandedOff
            },
            cacEvent);

        Assert.Equal(1, bo.Sender.SoLanGui);
        Assert.Equal(NotificationQueueStatus.DaBanGiao, muc.Status);
    }

    // ── Hàng 2: mục thuộc lô ⇒ ChoDuyet + event QueuedForApproval ────────────────────────────

    [Fact]
    public async Task Hang2_muc_co_BatchId_vao_ChoDuyet_va_phat_QueuedForApproval()
    {
        using var bo = new BoKiemThu();
        bo.SeedTemplate();
        var batchId = Guid.NewGuid();

        var ketQua = await bo.Appender.EnqueueAsync(YeuCau(key: "lo:1", batchId: batchId));

        Assert.Equal(nameof(NotificationQueueStatus.ChoDuyet), ketQua.Status);
        var muc = Assert.Single(bo.Items.TatCa);
        Assert.Equal(NotificationQueueStatus.ChoDuyet, muc.Status);
        Assert.Equal(batchId, muc.BatchId);

        var loai = bo.Events.TatCa.Select(x => x.EventType).ToList();
        Assert.Contains(NotificationQueueEventType.Enqueued, loai);
        Assert.Contains(NotificationQueueEventType.QueuedForApproval, loai);
        Assert.Equal(2, loai.Count);
    }

    [Fact]
    public async Task Hang4_DuyetLo_chuyen_ChoDuyet_sang_ChoGui()
    {
        using var bo = new BoKiemThu();
        bo.SeedTemplate();
        await bo.Appender.EnqueueAsync(YeuCau(key: "lo:2", batchId: Guid.NewGuid()));
        var muc = Assert.Single(bo.Items.TatCa);

        var suKien = muc.DuyetLo(Guid.NewGuid());

        Assert.Equal(NotificationQueueEventType.BatchApproved, suKien.EventType);
        Assert.Equal(NotificationQueueStatus.ChoGui, muc.Status);
    }

    // ── Lỗi trước khi ghi: template chưa sẵn sàng / render thiếu trường ──────────────────────

    [Fact]
    public async Task Template_chua_san_sang_thi_NEM_0012_truoc_moi_ghi()
    {
        using var bo = new BoKiemThu();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => bo.Appender.EnqueueAsync(YeuCau(key: "x", code: "khong-co")));

        Assert.Equal(NotificationErrorCodes.TemplateChuaSanSang, ex.Code);
        Assert.Empty(bo.Items.TatCa);
        Assert.Empty(bo.Events.TatCa);
    }

    [Fact]
    public async Task Render_thieu_truong_bat_buoc_thi_NEM_0013_truoc_moi_ghi()
    {
        using var bo = new BoKiemThu();
        bo.SeedTemplate();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => bo.Appender.EnqueueAsync(YeuCau(
                key: "x",
                data: new Dictionary<string, string>(StringComparer.Ordinal))));

        Assert.Equal(NotificationErrorCodes.ThieuTruongBatBuoc, ex.Code);
        Assert.Empty(bo.Items.TatCa);
        Assert.Equal(1, bo.BoGhi.Tong(eCommerceMetricNames.NotificationRenderFailedTotal));
    }

    // ── TC-30: khử trùng cửa sổ (marketing) — tuần tự ─────────────────────────────────────────

    [Fact]
    public async Task TC30_khu_trung_cua_so_marketing_lan_hai_thanh_DaHuy_bucket_NULL_va_0019()
    {
        using var bo = new BoKiemThu();
        bo.SeedTemplate(purpose: NotificationPurpose.Marketing, coalesceWindowSeconds: 300);

        var lan1 = await bo.Appender.EnqueueAsync(YeuCau(key: "mk:1"));
        var lan2 = await bo.Appender.EnqueueAsync(YeuCau(key: "mk:2")); // khoá khác, NỘI DUNG trùng

        // Lần 1 vào hàng đợi bình thường + có bucket (nằm TRONG partial UNIQUE index).
        Assert.False(lan1.AlreadyQueued);
        Assert.Equal(nameof(NotificationQueueStatus.Moi), lan1.Status);
        var muc1 = Assert.Single(bo.Items.TatCa, x => x.Id == lan1.QueueItemId);
        Assert.NotNull(muc1.CoalesceBucketStartUtc);

        // Lần 2 bị khử trùng: mục `DaHuy`, bucket = NULL (ra ngoài index), trả `0019`.
        Assert.False(lan2.AlreadyQueued);
        Assert.Equal(nameof(NotificationQueueStatus.DaHuy), lan2.Status);
        Assert.Equal(NotificationErrorCodes.BiKhudupCuaSo, lan2.ErrorCode);

        var muc2 = Assert.Single(bo.Items.TatCa, x => x.Id == lan2.QueueItemId);
        Assert.Equal(NotificationQueueStatus.DaHuy, muc2.Status);
        Assert.Null(muc2.CoalesceBucketStartUtc);

        // ĐÚNG MỘT event `Suppressed` trên mục bị khử trùng.
        Assert.Single(bo.Events.TatCa, e =>
            e.QueueItemId == muc2.Id && e.EventType == NotificationQueueEventType.Suppressed);

        // Mục lần 1 KHÔNG bị đổi; metric `window_suppressed` tăng 1.
        Assert.Equal(NotificationQueueStatus.Moi, bo.Items.TatCa.Single(x => x.Id == lan1.QueueItemId).Status);
        Assert.Equal(1, bo.BoGhi.Tong(eCommerceMetricNames.NotificationWindowSuppressedTotal));
    }

    // ── BR-007: QueueItem + QueueEvent + outbox trong CÙNG một UoW ────────────────────────────

    [Fact]
    public async Task BR007_ghi_muc_va_event_va_outbox_trong_MOT_uow()
    {
        using var bo = new BoKiemThu();
        bo.SeedTemplate();

        await bo.Appender.EnqueueAsync(YeuCau(key: "uow:1"));

        // Một lần mở UoW cho cả ba lần ghi ⇒ cùng transaction (atomicity trên DB là hệ quả).
        Assert.Equal(1, bo.Uow.SoLanBegin);
        Assert.Single(bo.Items.TatCa);
        Assert.Single(bo.Events.TatCa);
        Assert.Single(bo.Bus.DaPhat); // outbox publish TRONG cùng UoW
    }

    [Fact]
    public async Task BR007_render_loi_thi_KHONG_mo_uow_va_khong_ghi_gi()
    {
        using var bo = new BoKiemThu();
        bo.SeedTemplate();

        await Assert.ThrowsAsync<BusinessException>(() => bo.Appender.EnqueueAsync(YeuCau(
            key: "uow:loi", data: new Dictionary<string, string>(StringComparer.Ordinal))));

        Assert.Equal(0, bo.Uow.SoLanBegin);
        Assert.Empty(bo.Items.TatCa);
        Assert.Empty(bo.Events.TatCa);
        Assert.Empty(bo.Bus.DaPhat);
    }

    // ── BR-025: lô chỉ marketing, fail-closed — template khác vẫn `ChoDuyet` (phải duyệt) ─────

    [Fact]
    public async Task BR025_lo_bang_template_Transactional_van_vao_ChoDuyet_KHONG_gui_thang()
    {
        using var bo = new BoKiemThu();
        bo.SeedTemplate(purpose: NotificationPurpose.Transactional); // KHÔNG phải Marketing

        var ketQua = await bo.Appender.EnqueueAsync(YeuCau(key: "lo:tx", batchId: Guid.NewGuid()));

        // ❌ fail-closed: KHÔNG gửi thẳng, KHÔNG mã lỗi mới — vẫn `ChoDuyet`, phải duyệt (D-198).
        Assert.Equal(nameof(NotificationQueueStatus.ChoDuyet), ketQua.Status);
        Assert.Equal(NotificationQueueStatus.ChoDuyet, Assert.Single(bo.Items.TatCa).Status);
    }

    [Fact]
    public void BR025_ETO_nghiep_vu_KHONG_mang_BatchId()
    {
        Assert.Null(typeof(NotificationEnqueuedEto).GetProperty("BatchId"));
    }

    // ── D-269: mask MỘT lần sau render + fingerprint trên giá trị ĐÃ LƯU ────────────────────

    /// <summary>
    /// `TC-14` / `D-269` ①(2): appender mask **một lần** ngay sau render rồi dùng **cùng** giá trị đã mask
    /// cho `ContentFingerprint.TinhMuc(...)`, `TaoMuc` và `GhiKhudupAsync` — không có cửa ghi thứ hai.
    /// Body chứa email gốc ⇒ cột `RenderedBodyMasked` KHÔNG còn email gốc, và fingerprint khớp **giá trị đã lưu**.
    /// </summary>
    [Fact]
    public async Task TC14_appender_mask_than_va_fingerprint_tren_gia_tri_da_luu()
    {
        using var bo = new BoKiemThu();
        var (_, version) = bo.SeedTemplate(
            subject: "Xin chào {{Ten}}",
            body: "Lien he {{Ten}} — tong 1.500.000 ₫");

        await bo.Appender.EnqueueAsync(YeuCau(
            key: "tc14:1",
            data: new Dictionary<string, string>(StringComparer.Ordinal) { ["Ten"] = "a@b.com" }));

        var muc = Assert.Single(bo.Items.TatCa);

        // (a) cột đã-che KHÔNG chứa PII gốc; tiền giữ nguyên (D-269 ①(2)(v)).
        Assert.DoesNotContain("a@b.com", muc.RenderedBodyMasked);
        Assert.Contains("a***@b.com", muc.RenderedBodyMasked);
        Assert.Contains("1.500.000 ₫", muc.RenderedBodyMasked);
        Assert.DoesNotContain("a@b.com", muc.RenderedSubject);

        // (b) fingerprint tính trên ĐÚNG giá trị đã lưu (cùng công thức SRS §5.2).
        var recipientHash = NotificationRecipient.TinhHash("a@b.com");
        var subjectMasked = NotificationMasker.CheBiMat("Xin chào a@b.com");
        var bodyMasked = NotificationMasker.CheBiMat("Lien he a@b.com — tong 1.500.000 ₫");

        Assert.Equal(
            ContentFingerprint.TinhMuc(version.Id, recipientHash, subjectMasked, bodyMasked).Sha256,
            muc.ContentFingerprint);
    }

    // ── Phụ thuộc giả ─────────────────────────────────────────────────────────────────────────

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

    private sealed class GuidGia : IGuidGenerator
    {
        public Guid Create() => Guid.NewGuid();
    }

    private sealed class EncryptGia : IStringEncryptionService
    {
        public string? Encrypt(string? plainText, string? passPhrase = null, byte[]? salt = null)
            => plainText is null ? null : "enc:" + plainText;

        public string? Decrypt(string? cipherText, string? passPhrase = null, byte[]? salt = null)
            => cipherText;
    }

    private sealed class UowManagerGia : IUnitOfWorkManager
    {
        public int SoLanBegin { get; private set; }

        public IUnitOfWork? Current => null;

        public IUnitOfWork Begin(AbpUnitOfWorkOptions options, bool requiresNew = false)
        {
            Assert.True(requiresNew, "UoW ghi mục Notification PHẢI độc lập (requiresNew:true) — BR-007.");
            Assert.True(options.IsTransactional, "UoW ghi mục Notification PHẢI có transaction.");
            SoLanBegin++;
            return UowGia.Tao();
        }

        public Task<IUnitOfWork> BeginAsync(AbpUnitOfWorkOptions options, bool requiresNew = false,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IUnitOfWork> BeginAsync(bool requiresNew = false, bool isTransactional = false,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public IUnitOfWork Reserve(string reservationKey, bool requiresNew = false)
            => throw new NotSupportedException();

        public void BeginReserved(string reservationKey, AbpUnitOfWorkOptions options)
            => throw new NotSupportedException();

        public bool TryBeginReserved(string reservationKey, AbpUnitOfWorkOptions options)
            => throw new NotSupportedException();
    }

    private class UowGia : DispatchProxy
    {
        public static IUnitOfWork Tao() => (IUnitOfWork)Create<IUnitOfWork, UowGia>();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => targetMethod?.Name switch
            {
                nameof(IUnitOfWork.CompleteAsync) => Task.CompletedTask,
                nameof(IUnitOfWork.SaveChangesAsync) => Task.CompletedTask,
                "Dispose" => null,
                _ => throw new NotSupportedException($"UoW giả không hỗ trợ `{targetMethod?.Name}`.")
            };
    }

    private sealed class BusGia : IDistributedEventBus
    {
        public List<object> DaPhat { get; } = [];

        public Task PublishAsync<TEvent>(TEvent eventData, bool onUnitOfWorkComplete = true, bool useOutbox = true)
            where TEvent : class
        {
            DaPhat.Add(eventData);
            return Task.CompletedTask;
        }

        public Task PublishAsync(Type eventType, object eventData, bool onUnitOfWorkComplete = true,
            bool useOutbox = true) => throw new NotSupportedException("Appender phải dùng overload generic.");

        public Task PublishAsync(Type eventType, object eventData, bool onUnitOfWorkComplete = true)
            => throw new NotSupportedException();

        public Task PublishAsync(string eventName, object eventData, bool onUnitOfWorkComplete = true)
            => throw new NotSupportedException();

        public Task PublishAsync(string eventName, object eventData, bool onUnitOfWorkComplete = true,
            bool useOutbox = true) => throw new NotSupportedException();

        public Task PublishAsync<TEvent>(TEvent eventData, bool onUnitOfWorkComplete = true) where TEvent : class
            => throw new NotSupportedException();

        public IDisposable Subscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class
            => throw new NotSupportedException();

        public IDisposable Subscribe<TEvent, THandler>() where TEvent : class where THandler : IEventHandler, new()
            => throw new NotSupportedException();

        public IDisposable Subscribe(Type eventType, IEventHandler handler) => throw new NotSupportedException();

        public IDisposable Subscribe(string eventName, IEventHandler handler) => throw new NotSupportedException();

        public IDisposable Subscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class
            => throw new NotSupportedException();

        public IDisposable Subscribe(Type eventType, IEventHandlerFactory factory) => throw new NotSupportedException();

        public IDisposable Subscribe(string eventName, IEventHandlerFactory factory)
            => throw new NotSupportedException();

        public IDisposable Subscribe<TEvent>(IDistributedEventHandler<TEvent> handler) where TEvent : class
            => throw new NotSupportedException();

        public IDisposable Subscribe(string eventName, IDistributedEventHandler<DynamicEventData> handler)
            => throw new NotSupportedException();

        public void Unsubscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class
            => throw new NotSupportedException();

        public void Unsubscribe<TEvent>(ILocalEventHandler<TEvent> handler) where TEvent : class
            => throw new NotSupportedException();

        public void Unsubscribe(Type eventType, IEventHandler handler) => throw new NotSupportedException();

        public void Unsubscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class
            => throw new NotSupportedException();

        public void Unsubscribe(Type eventType, IEventHandlerFactory factory) => throw new NotSupportedException();

        public void Unsubscribe(string eventName, IEventHandler handler) => throw new NotSupportedException();

        public void Unsubscribe(string eventName, IEventHandlerFactory factory) => throw new NotSupportedException();

        public void UnsubscribeAll<TEvent>() where TEvent : class => throw new NotSupportedException();

        public void UnsubscribeAll(Type eventType) => throw new NotSupportedException();

        public void UnsubscribeAll(string eventName) => throw new NotSupportedException();

        public void Dispose()
        {
        }
    }

    private sealed class SenderGia : INotificationSender
    {
        public int SoLanGui { get; private set; }

        public NotificationSendRequest? LanCuoi { get; private set; }

        public Task<NotificationSendResult> SendAsync(NotificationSendRequest request,
            CancellationToken ct = default)
        {
            SoLanGui++;
            LanCuoi = request;
            return Task.FromResult(new NotificationSendResult(true, false, false, null));
        }
    }
}
