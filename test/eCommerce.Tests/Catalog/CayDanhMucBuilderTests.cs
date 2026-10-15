using eCommerce.Catalog;
using eCommerce.Catalog.Dtos;

namespace eCommerce.Tests.Catalog;

/// <summary>
/// Test **hàm dựng cây** (`CayDanhMucBuilder`) — hàm **thuần** nên test trực tiếp, không cần DB/DI.
///
/// 🚨 VÌ SAO ĐÁNG TEST: nó là chỗ dễ mắc **hai lỗi im lặng**:
///   1. Sắp xếp theo `Path` (chứa `Guid` ⇒ thứ tự **ngẫu nhiên**) ⇒ menu hiện sai thứ tự mà **vẫn "chạy"**.
///   2. Truyền vào danh sách **đã lọc** ⇒ nút con **mất tích khỏi menu** mà **không có lỗi nào**.
/// Hàm này đã chọn **ném lỗi** cho ca (2) và **sắp theo `SortOrder`/`Name`** cho ca (1) — test khoá cả hai.
/// </summary>
public class CayDanhMucBuilderTests
{
    private static Category Nut(Guid id, Guid? chaId, string code, int thuTu = 0, string ten = "Tên")
    {
        var nut = Category.Tao(id, null, code, ten);
        nut.DatThuTu(thuTu);

        if (chaId is { } cid)
        {
            // Dựng `ParentId`/`Path` đúng như `CategoryTreeService` làm: nạp cha giả rồi `DoiCha`.
            var cha = Category.Tao(cid, null, $"CHA-{cid.ToString("N")[..4]}", "Cha");
            nut.DoiCha(cha);
        }

        return nut;
    }

    [Fact]
    public void Dung_cay_long_nhau_theo_ParentId()
    {
        var idOng = Guid.NewGuid();
        var idCha = Guid.NewGuid();
        var idCon = Guid.NewGuid();

        var phang = new List<Category>
        {
            Nut(idOng, null, "ONG"),
            Nut(idCha, idOng, "CHA"),
            Nut(idCon, idCha, "CON")
        };

        var cay = CayDanhMucBuilder.DungCay(phang);

        Assert.Single(cay);                                   // chỉ 1 nút gốc
        Assert.Equal("ONG", cay[0].Code);
        Assert.Single(cay[0].Children);
        Assert.Equal("CHA", cay[0].Children[0].Code);
        Assert.Single(cay[0].Children[0].Children);
        Assert.Equal("CON", cay[0].Children[0].Children[0].Code);
    }

    [Fact]
    public void Sap_xep_con_theo_SortOrder_ROI_moi_toi_Name__KHONG_theo_Path()
    {
        // 🚨 Nếu ai đổi sang sắp theo `Path` thì thứ tự sẽ theo `Guid` (ngẫu nhiên) ⇒ test này ĐỎ.
        var idGoc = Guid.NewGuid();

        var phang = new List<Category>
        {
            Nut(idGoc, null, "GOC"),
            Nut(Guid.NewGuid(), idGoc, "C-THU-2", thuTu: 2, ten: "B"),
            Nut(Guid.NewGuid(), idGoc, "C-THU-1", thuTu: 1, ten: "Z"),
            Nut(Guid.NewGuid(), idGoc, "C-THU-1B", thuTu: 1, ten: "A")
        };

        var cay = CayDanhMucBuilder.DungCay(phang);

        var con = cay.Single().Children;
        Assert.Equal(3, con.Count);
        Assert.Equal(new[] { "C-THU-1B", "C-THU-1", "C-THU-2" }, con.Select(x => x.Code).ToArray());
    }

    [Fact]
    public void Nut_mo_coi__cha_khong_co_trong_danh_sach__thi_NEM_LOI()
    {
        // 🚨 Bẫy #2: truyền danh sách ĐÃ LỌC ⇒ nút con mất tích khỏi menu mà KHÔNG có lỗi nào.
        // Hàm này chọn NÉM LỖI để lộ ra thay vì che.
        var idChaKhongCo = Guid.NewGuid();

        var phang = new List<Category> { Nut(Guid.NewGuid(), idChaKhongCo, "CON-MO-COI") };

        var loi = Assert.Throws<InvalidOperationException>(() => CayDanhMucBuilder.DungCay(phang));

        Assert.Contains("mồ côi", loi.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Nhieu_nut_goc_thi_deu_duoc_tra_ve()
    {
        var phang = new List<Category>
        {
            Nut(Guid.NewGuid(), null, "GOC-A", thuTu: 2),
            Nut(Guid.NewGuid(), null, "GOC-B", thuTu: 1)
        };

        var cay = CayDanhMucBuilder.DungCay(phang);

        Assert.Equal(2, cay.Count);
        Assert.Equal("GOC-B", cay[0].Code);   // SortOrder 1 trước
        Assert.Equal("GOC-A", cay[1].Code);
    }

    [Fact]
    public void Danh_sach_rong_thi_tra_ve_rong__KHONG_nem_loi()
    {
        Assert.Empty(CayDanhMucBuilder.DungCay(new List<Category>()));
    }

    [Fact]
    public void ToDto_giu_nguyen_Path_Level_va_trang_thai()
    {
        var idGoc = Guid.NewGuid();
        var nut = Nut(idGoc, null, "GOC", thuTu: 3, ten: "Tên gốc");
        nut.NgungDung();
        nut.Slug = "ten-goc";
        nut.MaxVariantCombinations = 50;

        var dto = CayDanhMucBuilder.ToDto(nut);

        Assert.Equal(idGoc, dto.Id);
        Assert.Null(dto.ParentId);
        Assert.Equal("GOC", dto.Code);
        Assert.Equal("Tên gốc", dto.Name);
        Assert.Equal("ten-goc", dto.Slug);
        Assert.Equal($"/{idGoc}/", dto.Path);
        Assert.Equal(0, dto.Level);
        Assert.Equal(3, dto.SortOrder);
        Assert.False(dto.IsActive);
        Assert.Equal(50, dto.MaxVariantCombinations);
        Assert.Empty(dto.Children);
    }

    [Fact]
    public void Deep_copy_khong_bi_chia_se__hai_lan_goi_doc_lap()
    {
        // Mỗi lần gọi phải dựng DTO MỚI: dùng lại cùng instance sẽ làm `Children` **tích luỹ** giữa các lần gọi
        // (lỗi rất khó thấy vì lần gọi đầu vẫn đúng).
        var idGoc = Guid.NewGuid();
        var phang = new List<Category> { Nut(idGoc, null, "GOC"), Nut(Guid.NewGuid(), idGoc, "CON") };

        var lan1 = CayDanhMucBuilder.DungCay(phang);
        var lan2 = CayDanhMucBuilder.DungCay(phang);

        Assert.Single(lan1.Single().Children);
        Assert.Single(lan2.Single().Children);
    }
}
