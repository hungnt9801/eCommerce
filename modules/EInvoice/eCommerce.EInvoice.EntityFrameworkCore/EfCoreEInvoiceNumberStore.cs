using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.EInvoice.EntityFrameworkCore;

/// <summary>
/// Đọc **số hoá đơn lớn nhất đã cấp** trong DB — dùng cho <see cref="EInvoiceNumberIssuer"/>.
///
/// 🔒 Lấy theo **số lớn nhất đã cấp**, KHÔNG theo "hoá đơn còn hiệu lực": hoá đơn **đã huỷ vẫn giữ số**
/// của nó, nên huỷ hoá đơn **không** được giải phóng số (nếu giải phóng thì số hoá đơn sẽ **trùng**
/// với hoá đơn đã thông báo với cơ quan thuế).
/// </summary>
public class EfCoreEInvoiceNumberStore : IEInvoiceNumberStore, ITransientDependency
{
    private readonly IRepository<EInvoice, Guid> _repository;

    public EfCoreEInvoiceNumberStore(IRepository<EInvoice, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<long> SoLonNhatDaCapAsync(string kyHieuMauSo, CancellationToken cancellationToken = default)
    {
        var query = await _repository.GetQueryableAsync();

        // SoHoaDon là chuỗi 8 chữ số có 0 đứng đầu ⇒ so sánh CHUỖI vẫn đúng thứ tự số
        // (cùng độ dài, chỉ gồm chữ số). Dùng Max trên ký tự để tránh phải cast trong SQL.
        var soLonNhat = await query
            .Where(x => x.KyHieuMauSo == kyHieuMauSo)
            .Select(x => x.SoHoaDon)
            .OrderByDescending(x => x)
            .FirstOrDefaultAsync(cancellationToken);

        return soLonNhat is null ? 0 : EInvoiceNumberIssuer.DocSo(soLonNhat);
    }
}
