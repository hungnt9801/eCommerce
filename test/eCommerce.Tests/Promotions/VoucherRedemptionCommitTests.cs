using System.Diagnostics.Metrics;
using System.Text.Json;
using eCommerce.Kernel.Observability;
using eCommerce.Messaging;
using eCommerce.Messaging.Idempotency;
using eCommerce.Promotions.Contracts;
using eCommerce.Promotions.Workers;
using eCommerce.Tests.Pricing.HaTang;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace eCommerce.Promotions.Tests;

/// <summary>
/// **`CommitAsync` — chốt chặn `BR-06`/`0111` ở nhánh REPLAY** (`PRO-001-arch` §6.3 bước 1, `D-305`).
///
/// Nhánh replay (`IdempotencyOutcomeKind.TraVeKetQuaCu`) **vẫn phải so** `applied.DiscountAmount` với
/// `input.OrderDiscountTotal`; lệch ⇒ **NÉM** `0111`, KHÔNG phải `Messaging:0001` (payload khoá giữ nguyên
/// `{RedemptionId, OrderId, Code}` — KHÔNG chứa `OrderDiscountTotal`). Đây là phần **đơn vị hoá được** của
/// `BR-06`; nhánh non-replay (UPDATE → `Σ` trong DB) là **integration** (W25-4 arch §10.4).
///
/// 🎯 Dựng service bằng `new` + repo in-memory (`Pricing/HaTang/RepoGia`) + guard giả — KHÔNG host ABP, KHÔNG DB.
/// </summary>
public class VoucherRedemptionCommitTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime T0 = new(2026, 11, 27, 0, 0, 0, DateTimeKind.Utc);

    private sealed class GuardGia : IIdempotencyGuard
    {
        public IdempotencyOutcome KetQua { get; set; } =
            new(IdempotencyOutcomeKind.DangXuLy, ErrorCode: MessagingErrorCodes.DangXuLy);

        public IdempotencyKey? Khoa { get; private set; }
        public string? Payload { get; private set; }
        public bool DaGoiXuLy { get; private set; }

        public Task<IdempotencyOutcome> ThucThiAsync(
            IdempotencyKey key, string? payloadJson,
            Func<CancellationToken, Task<string?>> xuLy, CancellationToken cancellationToken = default)
        {
            Khoa = key;
            Payload = payloadJson;
            // Nhánh replay: KHÔNG gọi `xuLy` (không ghi lại lượt).
            return Task.FromResult(KetQua);
        }
    }

    private sealed class EngineKhongDung : IPromotionEngine
    {
        public Task<PromotionEvaluationResult> EvaluateAsync(
            PromotionEvaluationRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("CommitAsync KHÔNG gọi engine.");
    }

    private sealed class TenantGia : ICurrentTenant
    {
        public bool IsAvailable => true;
        public Guid? Id { get; set; } = TenantId;
        public string? Name => null;
        public IDisposable Change(Guid? id, string? name = null) => new Khong();
        private sealed class Khong : IDisposable { public void Dispose() { } }
    }

    private sealed class GuidGia : IGuidGenerator
    {
        public Guid Create() => Guid.NewGuid();
    }

    private sealed class Bo
    {
        public RepoGia<VoucherCode> Vouchers { get; } = new();
        public RepoGia<Promotion> Promotions { get; } = new();
        public RepoGia<PromotionRedemption> Redemptions { get; } = new();
        public GuardGia Guard { get; } = new();
        public VoucherRedemptionService Sut { get; }
        public Guid RedemptionId { get; }

        public Bo()
        {
            var metrics = new eCommerceMetrics(new Meter("promo-commit-test-" + Guid.NewGuid()));
            Sut = new VoucherRedemptionService(
                Vouchers,
                Promotions,
                Redemptions,
                new EngineKhongDung(),
                Guard,
                new UsageSequenceAllocator(),
                new TenantGia(),
                new DongHoGia(T0),
                new GuidGia(),
                Options.Create(new PromotionOptions()),
                NullLogger<VoucherRedemptionService>.Instance,
                metrics);

            RedemptionId = Guid.NewGuid();
        }

        /// <summary>Thêm một lượt `Reserved` khớp khoá phiên `Checkout:{tenant}:{session}:{code}`.</summary>
        public void ThemLuot(string code, string session, decimal discount = 10_000m)
        {
            var key = PromotionIdempotencyKeys.Checkout(TenantId, session, code);
            Redemptions.Them(PromotionRedemption.TaoMoi(
                RedemptionId, TenantId, Guid.NewGuid(), Guid.NewGuid(), null,
                PromotionRedemptionStatus.Reserved, T0.AddMinutes(15), 1, Guid.NewGuid(),
                key, discount, "VND", "{}", 1, T0));
        }

        public void TraKetQuaCu(decimal discount)
        {
            var cu = new VoucherAppliedDto
            {
                RedemptionId = RedemptionId,
                OrderId = Guid.NewGuid(),
                Status = PromotionRedemptionStatus.Applied,
                DiscountAmount = discount,
                Currency = "VND",
                AlreadyApplied = false
            };
            Guard.KetQua = new IdempotencyOutcome(
                IdempotencyOutcomeKind.TraVeKetQuaCu, JsonSerializer.Serialize(cu));
        }

        public CommitVoucherDto Vao(decimal orderDiscountTotal, string session = "session-1")
            => new(RedemptionId, Guid.NewGuid(), "abc", session, orderDiscountTotal);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════
    //  D-305 — nhánh replay
    // ════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task D305_replay_lech_so__nem_0111_KHONG_phai_Messaging_PayloadConflict()
    {
        var bo = new Bo();
        bo.ThemLuot("ABC", "session-1", discount: 10_000m);
        bo.TraKetQuaCu(discount: 10_000m); // kết quả cũ = 10.000

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => bo.Sut.CommitAsync(bo.Vao(orderDiscountTotal: 9_000m, session: "session-1")));

        Assert.Equal(PromotionErrorCodes.RedemptionSumMismatch, ex.Code);           // 0111
        Assert.NotEqual(MessagingErrorCodes.PayloadConflict, ex.Code);             // KHÔNG Messaging:0001
    }

    [Fact]
    public async Task D305_replay_khop_so__tra_ket_qua_cu_AlreadyApplied_true()
    {
        var bo = new Bo();
        bo.ThemLuot("ABC", "session-1", discount: 10_000m);
        bo.TraKetQuaCu(discount: 10_000m);

        var kq = await bo.Sut.CommitAsync(bo.Vao(orderDiscountTotal: 10_000m, session: "session-1"));

        Assert.True(kq.AlreadyApplied);
        Assert.Equal(PromotionRedemptionStatus.Applied, kq.Status);
        Assert.Equal(10_000m, kq.DiscountAmount);
        Assert.Equal("VND", kq.Currency);
    }

    [Fact]
    public async Task D305_payload_khoa_KHONG_chua_OrderDiscountTotal__van_so_0111()
    {
        // Bất biến D-305 ①(4): payload khoá commit giữ NGUYÊN `{RedemptionId, OrderId, Code}`.
        // Nếu ai thêm `OrderDiscountTotal`, lệch tiền sẽ đổi thành `Messaging:0001` — test này bắt được.
        var bo = new Bo();
        bo.ThemLuot("ABC", "session-1", discount: 10_000m);
        bo.TraKetQuaCu(discount: 10_000m);

        await bo.Sut.CommitAsync(bo.Vao(orderDiscountTotal: 10_000m, session: "session-1"));

        Assert.NotNull(bo.Guard.Payload);
        Assert.DoesNotContain("OrderDiscountTotal", bo.Guard.Payload, StringComparison.Ordinal);
        Assert.Contains("RedemptionId", bo.Guard.Payload, StringComparison.Ordinal);
        Assert.Contains("OrderId", bo.Guard.Payload, StringComparison.Ordinal);
        Assert.Contains("\"Code\":\"ABC\"", bo.Guard.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task D305_khoa_commit_dung_dang_PromotionCommit()
    {
        var bo = new Bo();
        bo.ThemLuot("ABC", "session-1");
        bo.TraKetQuaCu(discount: 10_000m);
        var vao = bo.Vao(orderDiscountTotal: 10_000m, session: "session-1");

        await bo.Sut.CommitAsync(vao);

        Assert.NotNull(bo.Guard.Khoa);
        Assert.Equal("promotion:commit", bo.Guard.Khoa!.Scope);
        Assert.Equal(PromotionIdempotencyKeys.Commit(TenantId, vao.OrderId, "ABC"), bo.Guard.Khoa.Value);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════
    //  D-298 ①(4) — chọn đúng lượt + khoá phiên; 0105 TRƯỚC guard
    // ════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task D298_luot_khong_ton_tai__nem_0105_truoc_guard()
    {
        var bo = new Bo();
        bo.Redemptions.Them(PromotionRedemption.TaoMoi(
            Guid.NewGuid(), TenantId, Guid.NewGuid(), null, null, PromotionRedemptionStatus.Reserved,
            T0.AddMinutes(15), 1, Guid.NewGuid(), "khoa-khac", 10_000m, "VND", "{}", 1, T0));
        // `bo.RedemptionId` chưa được thêm ⇒ FirstOrDefault null.

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => bo.Sut.CommitAsync(bo.Vao(orderDiscountTotal: 10_000m)));

        Assert.Equal(PromotionErrorCodes.RedemptionNotReserved, ex.Code);
        Assert.Null(bo.Guard.Khoa); // chưa chạm idempotency (0 side effect)
    }

    [Fact]
    public async Task D298_khoa_phien_khong_khop_luot__nem_0105_truoc_guard()
    {
        var bo = new Bo();
        bo.ThemLuot("ABC", "session-1");
        bo.TraKetQuaCu(discount: 10_000m);

        // Client gửi phiên khác ⇒ khoá `Checkout:...` không khớp lượt.
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => bo.Sut.CommitAsync(bo.Vao(orderDiscountTotal: 10_000m, session: "session-2")));

        Assert.Equal(PromotionErrorCodes.RedemptionNotReserved, ex.Code);
        Assert.Null(bo.Guard.Khoa);
    }
}
