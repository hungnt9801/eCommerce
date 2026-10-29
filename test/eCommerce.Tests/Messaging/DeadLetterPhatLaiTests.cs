using System.Diagnostics.Metrics;
using System.Reflection;
using System.Text.Json;
using eCommerce.Kernel.Observability;
using eCommerce.Messaging;
using eCommerce.Messaging.DeadLetter;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Linq;
using Volo.Abp.Uow;
using Volo.Abp.Users;

namespace eCommerce.Tests.Messaging;

/// <summary>Sự kiện GIẢ dùng cho test phát lại — 📌 phải là lớp **top-level** (bộ ánh xạ bỏ qua lớp lồng nhau).</summary>
public class SuKienPhatLaiGia
{
    public Guid OrderId { get; set; }

    public string Number { get; set; } = string.Empty;

    public decimal Total { get; set; }
}

/// <summary>
/// 🚨 **`S16b` — PHÁT LẠI DLQ LÊN BUS: bằng chứng HAI CHIỀU** (`XC-002` / `XC-005` §21).
///
/// 🔑 **VÌ SAO KHÔNG THỂ "CHỈ KIỂM ĐÃ ĐÁNH DẤU":** *"replay"* ở dự án này = **publish lại event gốc lên bus**.
/// Một app service chỉ ghi `DaXuLy = true` rồi trả `200` **trông giống hệt** một lần phát lại thật nếu test
/// chỉ đọc cờ trong DB. 📌 Vì vậy mọi ca dưới đây neo vào **`IDistributedEventBus` giả** (đếm **số lần** và
/// **đối số** publish), **metric thật** (`MeterListener`) và **dấu trong entity** — ba bằng chứng độc lập.
///
/// 🔒 **Ba bất biến được khoá:**
/// <list type="number">
///   <item>phát lại **một** dòng ⇒ publish **đúng MỘT** lần, metric **+1**, `ReplayedAtUtc` được ghi,
///     và publish nằm **TRONG** UoW (`onUnitOfWorkComplete: false` + `useOutbox: true`, **trước** `Complete`);</item>
///   <item>gọi lại **cùng** dòng ⇒ **không** publish lần hai, metric **không** tăng, trả **lý do** rõ ràng
///     (không ném — bấm hai lần không phải lỗi hệ thống);</item>
///   <item>không ánh xạ được loại sự kiện (hoặc payload không khớp) ⇒ **NÉM**, **không** publish,
///     metric **không** tăng, **không** ghi dấu.</item>
/// </list>
/// </summary>
public class DeadLetterPhatLaiTests
{
    // ── Bộ đo metric thật (cùng khuôn `S16MetricTests`) ────────────────────────────────────────

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

        public long Tong(string ten) => Ghi.Where(x => x.Ten == ten).Sum(x => x.GiaTri);

        public void Dispose() => _listener.Dispose();
    }

    // ── UoW giả: ghi lại THỨ TỰ "publish" vs "complete" ───────────────────────────────────────

    private sealed class NhatKy : List<string>
    {
        public int ViTri(string viec) => IndexOf(viec);
    }

    private class UowGia : DispatchProxy
    {
        private NhatKy _nhatKy = new();

        private readonly Guid _id = Guid.NewGuid();

        public bool DaComplete { get; private set; }

        public bool DaDispose { get; private set; }

        /// <summary>Tạo proxy + trả `IUnitOfWork`. 🔒 Chỉ `CompleteAsync`/`Dispose`/`SaveChangesAsync` được phép.</summary>
        public static IUnitOfWork Tao(NhatKy nhatKy)
        {
            var proxy = Create<IUnitOfWork, UowGia>();
            ((UowGia)(object)proxy)._nhatKy = nhatKy;

            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case nameof(IUnitOfWork.CompleteAsync):
                    _nhatKy.Add("complete");
                    DaComplete = true;
                    return Task.CompletedTask;

                case nameof(IUnitOfWork.SaveChangesAsync):
                    return Task.CompletedTask;

                // 📌 `using var uow` ⇒ dispose qua `IDisposable.Dispose` (KHÔNG phải `DisposeAsync`).
                case nameof(IDisposable.Dispose):
                    DaDispose = true;
                    return null;

                case "get_IsCompleted":
                    return DaComplete;

                case "get_IsDisposed":
                    return DaDispose;

                case "get_Id":
                    return _id;

                case "get_Outer":
                    return null;

                default:
                    throw new NotSupportedException(
                        "🚨 Test phát lại KHÔNG được gọi `IUnitOfWork." + targetMethod?.Name + "` — "
                        + "nếu app service cần thêm thành viên, hãy khai báo nó ở đây (và ghi lý do).");
            }
        }
    }

    private sealed class UowManagerGia : IUnitOfWorkManager
    {
        public NhatKy NhatKy { get; } = [];

        public IUnitOfWork? UowDaMo { get; private set; }

        public IUnitOfWork? Current => UowDaMo;

        /// <summary>
        /// 📌 App service gọi `_unitOfWorkManager.Begin(requiresNew: true, isTransactional: true)` — đó là
        /// **extension** `UnitOfWorkManagerExtensions.Begin` ⇒ cuối cùng rơi vào **overload này** với
        /// `AbpUnitOfWorkOptions`. 🔒 Khẳng định ngay TẠI ĐÂY để một thay đổi đặc tính UoW làm test ĐỎ,
        /// thay vì lặng lẽ dùng UoW của request.
        /// </summary>
        public IUnitOfWork Begin(AbpUnitOfWorkOptions options, bool requiresNew = false)
        {
            Assert.True(requiresNew, "🚨 UoW của thao tác phát lại PHẢI độc lập (`requiresNew: true`) — " +
                "nếu dùng UoW của request thì một lỗi ở bước sau có thể đã commit dấu phát lại.");
            Assert.True(options.IsTransactional, "🚨 UoW của thao tác phát lại PHẢI có transaction.");

            UowDaMo = UowGia.Tao(NhatKy);
            return UowDaMo;
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

    // ── Repository giả: chỉ đúng 2 thành viên mà luồng phát lại được phép dùng ────────────────

    private sealed class RepoGia : IRepository<DeadLetterMessage, Guid>
    {
        private readonly Dictionary<Guid, DeadLetterMessage> _duLieu = [];

        public int SoLanUpdate { get; private set; }

        public string? EntityName { get; set; } = nameof(DeadLetterMessage);

        public string ProviderName => "Test";

        public bool? IsChangeTrackingEnabled => false;

        public IAsyncQueryableExecuter AsyncExecuter => null!;

        public void Them(DeadLetterMessage ban) => _duLieu[ban.Id] = ban;

        public Task<DeadLetterMessage?> FindAsync(Guid id, bool includeDetails = true,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_duLieu.TryGetValue(id, out var ban) ? ban : null);

        public Task<DeadLetterMessage> UpdateAsync(DeadLetterMessage entity, bool autoSave = false,
            CancellationToken cancellationToken = default)
        {
            SoLanUpdate++;
            _duLieu[entity.Id] = entity;

            return Task.FromResult(entity);
        }

        // 🔒 MỌI thành viên khác ⇒ NÉM. Nếu app service lỡ dùng (ví dụ `InsertAsync` để "tạo lại" DLQ) thì
        // test nổ NGAY, thay vì lặng lẽ đi qua một hành vi không ai thiết kế.
        public IQueryable<DeadLetterMessage> GetQueryable() => throw new NotSupportedException();

        public Task<IQueryable<DeadLetterMessage>> GetQueryableAsync() => throw new NotSupportedException();

        public Task<List<DeadLetterMessage>> GetListAsync(bool includeDetails = false,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<List<DeadLetterMessage>> GetListAsync(
            System.Linq.Expressions.Expression<Func<DeadLetterMessage, bool>> predicate,
            bool includeDetails = false, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<long> GetCountAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<long> GetCountAsync(System.Linq.Expressions.Expression<Func<DeadLetterMessage, bool>> predicate,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DeadLetterMessage> GetAsync(Guid id, bool includeDetails = true,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DeadLetterMessage> GetAsync(
            System.Linq.Expressions.Expression<Func<DeadLetterMessage, bool>> predicate, bool includeDetails = true,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DeadLetterMessage?> FindAsync(
            System.Linq.Expressions.Expression<Func<DeadLetterMessage, bool>> predicate, bool includeDetails = true,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DeadLetterMessage> InsertAsync(DeadLetterMessage entity, bool autoSave = false,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task InsertManyAsync(IEnumerable<DeadLetterMessage> entities, bool autoSave = false,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task UpdateManyAsync(IEnumerable<DeadLetterMessage> entities, bool autoSave = false,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task DeleteAsync(DeadLetterMessage entity, bool autoSave = false,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task DeleteAsync(Guid id, bool autoSave = false, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(System.Linq.Expressions.Expression<Func<DeadLetterMessage, bool>> predicate,
            bool autoSave = false, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteManyAsync(IEnumerable<DeadLetterMessage> entities, bool autoSave = false,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task DeleteDirectAsync(System.Linq.Expressions.Expression<Func<DeadLetterMessage, bool>> predicate,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IQueryable<DeadLetterMessage>> WithDetailsAsync()
            => throw new NotSupportedException();

        public Task<IQueryable<DeadLetterMessage>> WithDetailsAsync(
            System.Linq.Expressions.Expression<Func<DeadLetterMessage, object>> propertySelector)
            => throw new NotSupportedException();

        public Task<IQueryable<DeadLetterMessage>> WithDetailsAsync(
            params System.Linq.Expressions.Expression<Func<DeadLetterMessage, object>>[] propertySelectors)
            => throw new NotSupportedException();

        public IQueryable<DeadLetterMessage> WithDetails() => throw new NotSupportedException();

        public IQueryable<DeadLetterMessage> WithDetails(
            params System.Linq.Expressions.Expression<Func<DeadLetterMessage, object>>[] propertySelectors)
            => throw new NotSupportedException();

        public Task<List<DeadLetterMessage>> GetPagedListAsync(int skipCount, int maxResultCount, string sorting,
            bool includeDetails = false, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteManyAsync(IEnumerable<Guid> ids, bool autoSave = false,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    // ── Bus giả: đếm và ghi lại ĐỐI SỐ publish ────────────────────────────────────────────────

    private sealed record LanPhatLai(Type Loai, object? DuLieu, bool OnUnitOfWorkComplete, bool UseOutbox);

    private sealed class BusGia : IDistributedEventBus
    {
        private readonly NhatKy _nhatKy;

        public BusGia(NhatKy nhatKy)
        {
            _nhatKy = nhatKy;
        }

        public List<LanPhatLai> DaPhat { get; } = [];

        public Task PublishAsync(Type eventType, object eventData, bool onUnitOfWorkComplete = true,
            bool useOutbox = true)
        {
            _nhatKy.Add("publish");
            DaPhat.Add(new LanPhatLai(eventType, eventData, onUnitOfWorkComplete, useOutbox));

            return Task.CompletedTask;
        }

        public Task PublishAsync<TEvent>(TEvent eventData, bool onUnitOfWorkComplete = true, bool useOutbox = true)
            where TEvent : class => throw new NotSupportedException("App service phải gọi overload `(Type, object)`.");

        public Task PublishAsync(Type eventType, object eventData, bool onUnitOfWorkComplete = true)
            => throw new NotSupportedException("App service phải gọi overload `(Type, object, bool, bool)`.");

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

    // ── Người dùng giả: KIỂM luôn phần audit "ai đã phát lại" ───────────────────────────────

    private sealed class NguoiDungGia : ICurrentUser
    {
        public string? Ten { get; init; }

        public bool IsAuthenticated => Ten is not null;

        public Guid? Id { get; init; }

        public string? UserName => Ten;

        public string? Name => Ten;

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

    // ── Dựng cả bộ ────────────────────────────────────────────────────────────────────────────

    private sealed class BoDo : IDisposable
    {
        public Meter Meter { get; }

        public BoGhi Ghi { get; }

        public eCommerceMetrics Metrics { get; }

        public RepoGia Repo { get; } = new();

        public UowManagerGia Uow { get; } = new();

        public BusGia Bus { get; }

        public DeadLetterAppService App { get; }

        public BoDo(string? nguoiDung = null)
        {
            Meter = new Meter("eCommerce-S16b-" + Guid.NewGuid());
            Ghi = new BoGhi(Meter);              // 🔑 nghe TRƯỚC khi tạo instrument
            Metrics = new eCommerceMetrics(Meter);
            Bus = new BusGia(Uow.NhatKy);

            App = new DeadLetterAppService(
                Repo,
                new MayGiaiGia(typeof(SuKienPhatLaiGia)),
                Bus,
                Uow,
                Metrics,
                new NguoiDungGia { Ten = nguoiDung },
                NullLogger<DeadLetterAppService>.Instance);
        }

        public void Dispose()
        {
            Meter.Dispose();
            Ghi.Dispose();
        }
    }

    /// <summary>Bộ ánh xạ với **tập loại xác định** — không phụ thuộc assembly nào đã nạp.</summary>
    private sealed class MayGiaiGia : DeadLetterEventTypeResolver
    {
        private readonly IReadOnlyList<Type> _ungVien;

        public MayGiaiGia(params Type[] ungVien)
        {
            _ungVien = ungVien;
        }

        protected override IReadOnlyList<Type> LayUngVien() => _ungVien;
    }

    private static DeadLetterMessage TaoDlq(
        string tenSuKien,
        string payloadJson,
        Guid? id = null)
        => new(
            id ?? Guid.NewGuid(),
            tenSuKien,
            "shopee:order:12345",
            payloadJson,
            new string('a', 64),
            LyDoVaoDlq.XungDotPayload,
            "Cùng khoá nhưng nội dung khác — probe S16b",
            DateTime.UtcNow.AddMinutes(-5));

    private static string PayloadCua(SuKienPhatLaiGia suKien) => JsonSerializer.Serialize(suKien);

    // ── (a) PHÁT LẠI THẬT ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Phat_lai_MOT_lan__publish_DUNG_MOT_lan_metric_1_va_ghi_dau()
    {
        using var bo = new BoDo("nguyen.van.a");

        var suKien = new SuKienPhatLaiGia { OrderId = Guid.NewGuid(), Number = "K9S16B-001", Total = 123_456m };
        var ban = TaoDlq(nameof(SuKienPhatLaiGia), PayloadCua(suKien));
        bo.Repo.Them(ban);

        var ketQua = await bo.App.PhatLaiAsync(ban.Id);

        // ① publish đúng MỘT lần, đúng LOẠI và đúng DỮ LIỆU gốc
        var lan = Assert.Single(bo.Bus.DaPhat);
        Assert.Equal(typeof(SuKienPhatLaiGia), lan.Loai);

        var daGui = Assert.IsType<SuKienPhatLaiGia>(lan.DuLieu);
        Assert.Equal(suKien.OrderId, daGui.OrderId);
        Assert.Equal("K9S16B-001", daGui.Number);
        Assert.Equal(123_456m, daGui.Total);

        // ② `useOutbox: true` ⇒ KHÔNG mất event; `onUnitOfWorkComplete: false` ⇒ ghi outbox NGAY trong UoW
        Assert.True(lan.UseOutbox, "🚨 Phát lại PHẢI qua outbox — publish trực tiếp là mất event khi process chết.");
        Assert.False(lan.OnUnitOfWorkComplete,
            "🚨 Phải ghi outbox TRONG UoW hiện tại (cùng transaction với dấu phát lại), không phải sau khi UoW đóng.");

        // ③ publish nằm TRONG UoW: xảy ra TRƯỚC `Complete`
        Assert.Contains("publish", bo.Uow.NhatKy);
        Assert.Contains("complete", bo.Uow.NhatKy);
        Assert.True(bo.Uow.NhatKy.ViTri("publish") < bo.Uow.NhatKy.ViTri("complete"),
            "🚨 Thứ tự sai: publish phải nằm TRƯỚC `CompleteAsync` (nếu sau thì dấu đã commit mà event chưa đi).");

        // ④ dấu phát lại trong entity (bằng chứng trong DB)
        Assert.True(ban.DaPhatLai);
        Assert.NotNull(ban.ReplayedAtUtc);
        Assert.Equal(1, ban.ReplayCount);
        Assert.Equal("nguyen.van.a", ban.ReplayedBy);            // 🔒 audit: AI đã quyết định gửi lại
        Assert.True(ban.DaXuLy);
        Assert.Equal("nguyen.van.a", ban.NguoiXuLy);
        Assert.Contains("Phát lại lên bus thành công", ban.LyDoXuLy!, StringComparison.Ordinal);
        Assert.Equal(1, bo.Repo.SoLanUpdate);

        // ⑤ metric +1 SAU khi thành công, kèm nhãn hữu hạn
        Assert.Equal(1L, bo.Ghi.Tong(eCommerceMetricNames.MessagingDlqReplayedTotal));
        var doDuoc = Assert.Single(bo.Ghi.Ghi, x => x.Ten == eCommerceMetricNames.MessagingDlqReplayedTotal);
        Assert.Equal("ok", doDuoc.Nhan[eCommerceMetricTags.Result]);
        Assert.Equal(nameof(SuKienPhatLaiGia), doDuoc.Nhan[eCommerceMetricTags.EventName]);

        // ⑥ DTO nói ĐÚNG sự thật
        Assert.True(ketQua.DaPhatLai);
        Assert.Equal(1, ketQua.SoLanPhatLai);
        Assert.Equal(ban.ReplayedAtUtc, ketQua.PhatLaiLucUtc);
    }

    // ── (b) IDEMPOTENT ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Goi_LAI_cung_dong__KHONG_publish_lan_hai__metric_KHONG_tang__tra_LY_DO()
    {
        using var bo = new BoDo();

        var ban = TaoDlq(nameof(SuKienPhatLaiGia), PayloadCua(new SuKienPhatLaiGia { Number = "K9S16B-002" }));
        bo.Repo.Them(ban);

        var lan1 = await bo.App.PhatLaiAsync(ban.Id);
        var mocSauLan1 = ban.ReplayedAtUtc;

        var lan2 = await bo.App.PhatLaiAsync(ban.Id);

        // ① KHÔNG publish lần hai
        Assert.Single(bo.Bus.DaPhat);

        // ② metric KHÔNG tăng
        Assert.Equal(1L, bo.Ghi.Tong(eCommerceMetricNames.MessagingDlqReplayedTotal));

        // ③ dấu KHÔNG bị ghi đè (bằng chứng của lần đầu còn nguyên)
        Assert.Equal(mocSauLan1, ban.ReplayedAtUtc);
        Assert.Equal(1, ban.ReplayCount);
        Assert.Equal(1, bo.Repo.SoLanUpdate);

        // ④ DTO trả LÝ DO rõ ràng — không ném, không im lặng
        Assert.True(lan1.DaPhatLai);
        Assert.False(lan2.DaPhatLai);
        Assert.Contains("BỎ QUA", lan2.LyDo, StringComparison.Ordinal);
        Assert.Contains("MỘT lần", lan2.LyDo, StringComparison.Ordinal);
        Assert.Equal(1, lan2.SoLanPhatLai);
        Assert.Equal(mocSauLan1, lan2.PhatLaiLucUtc);
    }

    // ── (c) FAIL-CLOSED khi KHÔNG ánh xạ được loại ───────────────────────────────────────────

    [Fact]
    public async Task TenSuKien_khong_anh_xa_duoc__NEM__KHONG_publish__metric_khong_tang__khong_ghi_dau()
    {
        using var bo = new BoDo();

        var ban = TaoDlq("LoaiSuKienKhongTonTai", PayloadCua(new SuKienPhatLaiGia()));
        bo.Repo.Them(ban);

        var loi = await Assert.ThrowsAsync<BusinessException>(() => bo.App.PhatLaiAsync(ban.Id));

        Assert.Equal(MessagingErrorCodes.KhongXacDinhDuocLoaiSuKien, loi.Code);

        Assert.Empty(bo.Bus.DaPhat);
        Assert.Equal(0L, bo.Ghi.Tong(eCommerceMetricNames.MessagingDlqReplayedTotal));
        Assert.Null(ban.ReplayedAtUtc);
        Assert.False(ban.DaPhatLai);
        Assert.False(ban.DaXuLy);
        Assert.Equal(0, bo.Repo.SoLanUpdate);
        Assert.DoesNotContain("complete", bo.Uow.NhatKy);
    }

    [Fact]
    public async Task Payload_khong_khop_loai__NEM__KHONG_publish__metric_khong_tang()
    {
        using var bo = new BoDo();

        // 🚨 JSON hợp lệ nhưng KHÔNG phải object của `SuKienPhatLaiGia` (mảng thay vì object).
        var ban = TaoDlq(nameof(SuKienPhatLaiGia), "[1,2,3]");
        bo.Repo.Them(ban);

        var loi = await Assert.ThrowsAsync<BusinessException>(() => bo.App.PhatLaiAsync(ban.Id));

        Assert.Equal(MessagingErrorCodes.PayloadKhongKhopLoaiSuKien, loi.Code);
        Assert.Empty(bo.Bus.DaPhat);
        Assert.Equal(0L, bo.Ghi.Tong(eCommerceMetricNames.MessagingDlqReplayedTotal));
        Assert.Null(ban.ReplayedAtUtc);
        Assert.Equal(0, bo.Repo.SoLanUpdate);
    }

    [Fact]
    public async Task Dong_khong_ton_tai__NEM_0007__khong_publish()
    {
        using var bo = new BoDo();

        var loi = await Assert.ThrowsAsync<BusinessException>(() => bo.App.PhatLaiAsync(Guid.NewGuid()));

        Assert.Equal(MessagingErrorCodes.KhongTimThay, loi.Code);
        Assert.Empty(bo.Bus.DaPhat);
        Assert.Equal(0L, bo.Ghi.Tong(eCommerceMetricNames.MessagingDlqReplayedTotal));
    }

    // ── Bộ ánh xạ: chấp nhận HAI dạng tên, NHẬP NHẰNG ⇒ NÉM ──────────────────────────────────

    [Theory]
    [InlineData("SuKienPhatLaiGia", true)]
    [InlineData("eCommerce.Tests.Messaging.SuKienPhatLaiGia", true)]
    [InlineData("eCommerce.Tests.Messaging.SuKienPhatLaiGia, eCommerce.Tests", true)]
    [InlineData("SuKienPhatLaiGiaX", false)]
    [InlineData("", false)]
    public void Bo_anh_xa_chap_nhan_ten_ngan_va_ten_day_du__con_lai_NEM(string ten, bool phaiTimThay)
    {
        var may = new MayGiaiGia(typeof(SuKienPhatLaiGia));

        if (phaiTimThay)
        {
            Assert.Equal(typeof(SuKienPhatLaiGia), may.Giai(ten));
        }
        else
        {
            var loi = Assert.Throws<BusinessException>(() => may.Giai(ten));
            Assert.Equal(MessagingErrorCodes.KhongXacDinhDuocLoaiSuKien, loi.Code);
        }
    }

    [Fact]
    public void Ten_NGAN_nhap_nhang__NEM__khong_chon_bua()
    {
        // 🚨 Hai loại KHÁC namespace nhưng CÙNG tên ngắn ⇒ không được chọn bừa (chọn sai = gửi sai loại).
        var may = new MayGiaiGia(typeof(NhapNhang.A.TrungTenEto), typeof(NhapNhang.B.TrungTenEto));

        var loi = Assert.Throws<BusinessException>(() => may.Giai("TrungTenEto"));

        Assert.Equal(MessagingErrorCodes.KhongXacDinhDuocLoaiSuKien, loi.Code);

        // 🔑 Thông điệp của `BusinessException` là **mã lỗi** (bản địa hoá ở tầng HTTP); lý do đọc được nằm
        // ở `Data["lyDo"]` — kiểm ở đó để chứng minh đây là ca **NHẬP NHẰNG**, không phải "không tìm thấy".
        Assert.Contains("NHẬP NHẰNG", loi.Data["lyDo"]?.ToString(), StringComparison.Ordinal);
        Assert.Contains("NHIỀU loại cùng TÊN NGẮN", loi.Data["lyDo"]?.ToString(), StringComparison.Ordinal);

        // ✅ Nhưng tên ĐẦY ĐỦ thì phân biệt được — đây là lý do bộ ánh xạ ưu tiên `FullName`.
        Assert.Equal(typeof(NhapNhang.A.TrungTenEto), may.Giai(typeof(NhapNhang.A.TrungTenEto).FullName!));
    }
}

/// <summary>Hai namespace để tạo ca **tên ngắn nhập nhằng** — 📌 chỉ phục vụ test, không phải mã sản phẩm.</summary>
public static class NhapNhang
{
    public static class A
    {
        public class TrungTenEto
        {
        }
    }

    public static class B
    {
        public class TrungTenEto
        {
        }
    }
}
