using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using eCommerce.Notification;
using eCommerce.Tests.Pricing.HaTang;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Users;

namespace eCommerce.Tests.Notification;

/// <summary>
/// `TC-37` — `BR-NOT-001-014` / biên vào #4 (`SRS §3` hàng 11, `§8`): **phát lại tạo mục MỚI** với khoá
/// `{khoá gốc}:replay-{n}`, `RootItemId` trỏ mục gốc; mục gốc **ở lại `Loi`** (hấp thụ) và nhận event `Replayed`.
/// Phần **cấm dùng lại khoá gốc** được bảo chứng bởi unique `(TenantId, IdempotencyKey)` (xem `TC-19` integration).
///
/// 📌 Dựng `NotificationQueueAppService` bằng `new` + `RepoGia&lt;T&gt;` + gán tay `LazyServiceProvider` (`D-229`),
/// **KHÔNG** host ABP. `_items.GetListAsync(x =&gt; x.RootItemId == goc.Id)` chạy trên repository in-memory.
/// Phần **đua 2 luồng phát lại** + `23505` trên DB là **chưa kiểm được** ở hạ tầng này (testplan §"Chưa kiểm được").
/// </summary>
public class NotificationQueueAppServiceTests
{
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

    private sealed class NguoiDungGia : ICurrentUser
    {
        public Guid? Id { get; init; } = Guid.Parse("22222222-2222-2222-2222-222222222222");
        public bool IsAuthenticated => Id is not null;
        public string? UserName => "qa";
        public string? Name => "QA";
        public string? SurName => null;
        public string? Email => null;
        public string? PhoneNumber => null;
        public string[] Roles => [];
        public Guid? TenantId => null;
        public bool PhoneNumberVerified => false;
        public bool EmailVerified => false;
        public bool IsInRole(string role) => false;
        public string? FindClaimValue(string claimType) => null;
        public System.Security.Claims.Claim? FindClaim(string claimType) => null;
        public System.Security.Claims.Claim[] FindClaims(string claimType) => [];
        public System.Security.Claims.Claim[] GetAllClaims() => [];
    }

    private sealed class TenantGia : ICurrentTenant
    {
        public bool IsAvailable => true;
        public Guid? Id => null;
        public string? Name => null;
        public IDisposable Change(Guid? id, string? name = null) => new KhongDoi();

        private sealed class KhongDoi : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed class KhoaGia : IAbpDistributedLock
    {
        public bool LuonBan { get; set; }

        public Task<IAbpDistributedLockHandle?> TryAcquireAsync(
            string name, TimeSpan timeout, CancellationToken cancellationToken = default)
            => Task.FromResult<IAbpDistributedLockHandle?>(
                LuonBan ? null : new Handle());

        private sealed class Handle : IAbpDistributedLockHandle
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    /// <summary>Appender giả — `RetryAsync` KHÔNG đi qua `EnqueueAsync`.</summary>
    private sealed class AppenderGia : INotificationQueueAppender
    {
        public Task<EnqueueResult> EnqueueAsync(EnqueueRequest request, CancellationToken ct = default)
            => throw new NotSupportedException("RetryAsync không được gọi EnqueueAsync.");
    }

    private sealed class Bo
    {
        public RepoGia<NotificationQueueItem> Items { get; } = new();
        public RepoGia<NotificationQueueEvent> Events { get; } = new();
        public KhoaGia Khoa { get; } = new();
        public NotificationQueueAppService Sut { get; }

        public Bo()
        {
            Sut = new NotificationQueueAppService(
                new AppenderGia(), Items, Events, new GuidGia(), new ClockGia(), new NguoiDungGia(), Khoa);

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGuidGenerator>(new GuidGia());
            services.AddSingleton<ILogger>(NullLogger.Instance);
            services.AddSingleton<ICurrentTenant>(new TenantGia());
            Sut.LazyServiceProvider = new AbpLazyServiceProvider(services.BuildServiceProvider());
        }
    }

    private static NotificationQueueItem MucLoi()
    {
        var muc = NotificationTestData.ToDangBanGiao(); // IdempotencyKey mặc định `idem-1`
        muc.DanhDauLoi("NCC-0002", "ly-do-che");
        return muc;
    }

    [Fact]
    public async Task TC37_phat_lai_tao_muc_MOI_khoa_replay_1_goc_o_lai_Loi()
    {
        var bo = new Bo();
        var goc = MucLoi();
        bo.Items.Them(goc);

        var dto = await bo.Sut.RetryAsync(goc.Id);

        var moi = bo.Items.TatCa.Single(x => x.Id == dto.Id);
        Assert.NotEqual(goc.Id, moi.Id);
        Assert.Equal("idem-1:replay-1", moi.IdempotencyKey);
        Assert.Equal(goc.Id, moi.RootItemId);
        Assert.Equal(NotificationQueueStatus.Moi, moi.Status);

        // Mục gốc Ở LẠI `Loi` (hấp thụ) — phát lại KHÔNG phải một cạnh trạng thái của mục cũ (SRS §3).
        Assert.Equal(NotificationQueueStatus.Loi, goc.Status);

        // Event: `Enqueued` trên mục MỚI + `Replayed` trên mục GỐC.
        Assert.Contains(bo.Events.TatCa, e =>
            e.QueueItemId == moi.Id && e.EventType == NotificationQueueEventType.Enqueued);
        Assert.Contains(bo.Events.TatCa, e =>
            e.QueueItemId == goc.Id && e.EventType == NotificationQueueEventType.Replayed);
    }

    [Fact]
    public async Task TC37_phat_lai_lan_hai_la_replay_2()
    {
        var bo = new Bo();
        var goc = MucLoi();
        bo.Items.Them(goc);

        var lan1 = await bo.Sut.RetryAsync(goc.Id);
        var lan2 = await bo.Sut.RetryAsync(goc.Id);

        var khoa1 = bo.Items.TatCa.Single(x => x.Id == lan1.Id).IdempotencyKey;
        var khoa2 = bo.Items.TatCa.Single(x => x.Id == lan2.Id).IdempotencyKey;

        Assert.Equal("idem-1:replay-1", khoa1);
        Assert.Equal("idem-1:replay-2", khoa2);
        Assert.Equal(2, bo.Items.TatCa.Count(x => x.RootItemId == goc.Id));
    }

    [Fact]
    public async Task TC37_khong_gianh_duoc_khoa_phat_lai_thi_NEM_Lock0001_va_KHONG_tao_muc()
    {
        var bo = new Bo();
        bo.Khoa.LuonBan = true;
        var goc = MucLoi();
        bo.Items.Them(goc);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => bo.Sut.RetryAsync(goc.Id));

        Assert.Equal(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa, ex.Code);
        Assert.Single(bo.Items.TatCa); // không tạo mục nào
        Assert.Empty(bo.Events.TatCa); // không ghi event nào
    }

    // ── D-269: lý do huỷ chứa SĐT bị mask ở MỤC và EVENT ───────────────────────────────────

    /// <summary>
    /// `TC-14` / `D-269`: `CancelAsync` mask **một lần** lý do người dùng gõ rồi dùng **cùng** giá trị cho
    /// `LastReasonMasked` của mục và `ReasonMasked` của event — không lộ SĐT thô ở cả hai cửa ghi.
    /// </summary>
    [Fact]
    public async Task TC14_CancelAsync_ly_do_chua_SDT_bi_mask_o_muc_va_event()
    {
        var bo = new Bo();
        var muc = NotificationTestData.QueueItem();
        bo.Items.Them(muc);

        var dto = await bo.Sut.CancelAsync(muc.Id,
            new CancelQueueItemRequest { LyDo = "Khach 0912345678 goi huy don" });

        Assert.Equal(NotificationQueueStatus.DaHuy, muc.Status);
        Assert.DoesNotContain("0912345678", muc.LastReasonMasked);
        Assert.Contains("09***678", muc.LastReasonMasked);
        Assert.Equal(muc.LastReasonMasked, dto.LastReasonMasked);

        var suKien = Assert.Single(bo.Events.TatCa);
        Assert.Equal(NotificationQueueEventType.Cancelled, suKien.EventType);
        Assert.DoesNotContain("0912345678", suKien.ReasonMasked);
        Assert.Equal(muc.LastReasonMasked, suKien.ReasonMasked);
    }
}
