using System.Text;
using eCommerce.Customers;

namespace eCommerce.Tests.Customers;

/// <summary>
/// 🚨 **TEST CHO BO GHI CSV NHAT KY KIEM TOAN** (`CUS-001` §25) — 📌 va day la **dong no §24.6 #1**
/// (*"chưa có test ở tầng unit"*): 🔑 logic CSV la **ham THUAN** ⇒ ✅ test duoc **nhanh, khong can DB,
/// khong can Web chay**.
///
/// 🔒 **VI SAO PHAI TEST KY:** mot tep CSV **trong don gian** nhung co **ba cai bay**, va moi cai tao ra mot
/// tep **DOC SAI**. ⚠️ Va **mot tep bang chung doc sai con te hon KHONG CO tep** — 📌 no khien nguoi ta
/// **tin** rang minh da kiem.
/// </summary>
public class CsvNhatKyTests
{
    // ── ① BOM — CAI BAY SO MOT VOI TIENG VIET ──────────────────────────────────────────────────

    [Fact]
    public void TC01_BOM_dung_ba_byte_UTF8__thieu_no_thi_Excel_doc_tieng_Viet_thanh_rac()
    {
        // 🔑 Excel (ban pho bien o VN) **khong** tu nhan UTF-8 neu thieu BOM ⇒ 📌 `Nhật` thanh `Nh?t`.
        // ⚠️ Va tep do la thu **duoc gui cho co quan kiem toan**.
        Assert.Equal(3, CsvNhatKy.Bom.Length);
        Assert.Equal(0xEF, CsvNhatKy.Bom[0]);
        Assert.Equal(0xBB, CsvNhatKy.Bom[1]);
        Assert.Equal(0xBF, CsvNhatKy.Bom[2]);
    }

    [Fact]
    public void TC02_Encoding_UTF8_GetBytes_KHONG_tu_them_BOM__nen_phai_ghep_THU_CONG()
    {
        // 🚨 **CA CHONG BAY THAT:** 📌 `Encoding.UTF8.GetBytes("a")` tra **1 byte**, khong phai 4.
        // 🔑 Nghia la: ghi `File(Encoding.UTF8.GetBytes(s), "text/csv", ...)` ⇒ **KHONG CO BOM** ⇒
        // ⚠️ tieng Viet hong trong Excel, **du** `Content-Type` ghi `charset=utf-8`.
        // ✅ Controller phai **ghep BOM thu cong** — test nay khoa su that do lai.
        // 📌 `"Nhật"` = **4 KY TU** (`N`, `h`, `ậ`, `t`) ⇒ 🔑 `ậ` chiem **3 byte** trong UTF-8
        // (⚠️ khong phai 2): 1 + 1 + 3 + 1 = **6 byte**. 📌 Ghi ro phep dem o day vi 🔑 chinh con so nay
        // la bang chung rang "co BOM" va "khong co BOM" **khac nhau that**.
        var than = Encoding.UTF8.GetBytes("Nhật");

        Assert.Equal(6, than.Length);
        Assert.NotEqual(0xEF, than[0]);

        // ➕ Va mot ban ghep dung thi byte dau **phai** la BOM.
        var bytes = new byte[CsvNhatKy.Bom.Length + than.Length];
        CsvNhatKy.Bom.CopyTo(bytes, 0);
        than.CopyTo(bytes, CsvNhatKy.Bom.Length);

        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(9, bytes.Length);
        Assert.Equal("Nhật", Encoding.UTF8.GetString(bytes, 3, 6));
    }

    // ── ② BOC TRUONG (RFC 4180) ───────────────────────────────────────────────────────────────

    [Fact]
    public void TC03_dau_phay_trong_du_lieu_KHONG_duoc_tach_thanh_hai_cot()
    {
        // 🚨 **CA QUAN TRONG NHAT.** ⚠️ Mot ly do nhu *"theo yeu cau, qua dien thoai"* co **dau phay** ⇒
        // 🔒 neu khong boc trong `"…"` thi no **tach thanh hai cot** ⇒ **toan bo cac cot sau bi LECH**,
        // 📌 va nguoi doc **khong he biet** (bang van trong hop le).
        var o = CsvNhatKy.Boc("theo yeu cau, qua dien thoai");

        Assert.Equal("\"theo yeu cau, qua dien thoai\"", o);

        // 📌 Va mot dong thi **dem duoc** dung so truong: 2 truong ⇒ **1** dau phay (khong phai 2).
        var dong = CsvNhatKy.Dong("a,b", "c");
        Assert.Equal("\"a,b\",\"c\"", dong);
    }

    [Fact]
    public void TC04_dau_nhay_kep_trong_du_lieu_duoc_NHAN_DOI__theo_RFC_4180()
    {
        // ⚠️ `"` khong duoc nhan doi thi no **dong truong SOM** ⇒ 📌 phan con lai **tran ra ngoai**
        // ⇒ 🔒 dong CSV **vo** va cac cot sau **lech**.
        Assert.Equal("\"noi \"\"x\"\" day\"", CsvNhatKy.Boc("noi \"x\" day"));
    }

    [Fact]
    public void TC05_LUON_boc__ke_ca_khi_khong_can__de_chi_co_MOT_quy_tac()
    {
        // 🔑 **VI SAO LUON BOC:** neu chi boc khi *"thay dau phay"* thi mot gia tri chua **xuong dong**
        // hoac chua `"` se duoc xu ly **khac nhau tuy du lieu** ⇒ ⚠️ do la loai loi **chi xuat hien voi
        // mot so dong** ⇒ 📌 **khong tai hien duoc** o ca kiem binh thuong.
        // ✅ Luon boc ⇒ **mot quy tac duy nhat**, de kiem va de tin.
        Assert.Equal("\"abc\"", CsvNhatKy.Boc("abc"));
        Assert.Equal("\"\"", CsvNhatKy.Boc(""));
    }

    [Fact]
    public void TC06_null_ghi_thanh_O_TRONG__KHONG_phai_chu_null()
    {
        // 🔑 Neu ghi chu `null` thi nguoi doc **khong phan biet duoc** *"khong co du lieu"* voi
        // *"du lieu la chu null"* — 📌 va do la khac biet **co nghia** trong mot bang chung kiem toan.
        Assert.Equal("\"\"", CsvNhatKy.Boc(null));
    }

    [Fact]
    public void TC07_ghep_dong_dung_so_truong()
    {
        var dong = CsvNhatKy.Dong("2026-01-02 03:04:05", "admin", "id-1", "Ten, Khach", "10", "1", "khong", null);

        // 📌 Dem truong bang cach doc **that su** qua mot bo doc CSV toi gian (🔑 khong dem dau phay:
        // ⚠️ dau phay trong `"…"` se lam phep dem sai — dung loi ma test nay sinh ra de chan).
        var truong = DocDongCsv(dong);

        Assert.Equal(8, truong.Count);
        Assert.Equal("Ten, Khach", truong[3]);
        Assert.Equal("", truong[7]);
    }

    // ── ③ XUONG DONG ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TC08_xuong_dong_la_CRLF__khong_phai_LF_tran()
    {
        // 🔒 RFC 4180 quy dinh **CRLF**; ⚠️ mot so trinh doc (ke ca Excel trong vai ngu canh) coi `\n` tran
        // la **ky tu trong o** ⇒ 📌 **toan bo tep thanh MOT o**.
        Assert.Equal("\r\n", CsvNhatKy.XuongDong);
        Assert.Equal(2, CsvNhatKy.XuongDong.Length);
    }

    // ── ④ DONG CANH BAO KHI BI CAT — BAT BUOC ─────────────────────────────────────────────────

    [Fact]
    public void TC09_dong_canh_bao_cat_co_CA_HAI_con_so__va_co_chu_CANH_BAO()
    {
        // 🚨 **VI SAO BAT BUOC:** tep CSV **roi khoi he thong** — no duoc **gui, luu, va dinh kem bao cao**.
        // ⚠️ Mot tep **im lang cat bot** se duoc doc o **mot noi khac, vao mot thoi diem khac, boi mot nguoi
        // khac** ⇒ 📌 nguoi do **khong co cach nao** biet bang chung da bi cat.
        var dong = CsvNhatKy.DongCanhBaoCat(dangHienThi: 100, tongSo: 437, new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc));

        Assert.Contains("CANH BAO", dong, StringComparison.Ordinal);
        Assert.Contains("100", dong, StringComparison.Ordinal);
        Assert.Contains("437", dong, StringComparison.Ordinal);
        Assert.Contains("2026-10-17 12:00:00", dong, StringComparison.Ordinal);
    }

    [Fact]
    public void TC10_dong_tien_de_noi_ro_AI_xuat__KHI_NAO__va_KHONG_chua_PII()
    {
        var dong = CsvNhatKy.DongTienDe("admin", new DateTime(2026, 10, 17, 1, 2, 3, DateTimeKind.Utc), 2, 2);

        Assert.Contains("91/2025/QH15", dong, StringComparison.Ordinal);
        Assert.Contains("admin", dong, StringComparison.Ordinal);
        Assert.Contains("2026-10-17 01:02:03", dong, StringComparison.Ordinal);
        // 🔑 Khang dinh **TEP KHONG CHUA PII** — 📌 de nguoi nhan biet co the luu/truyen **khac** ban xuat PII.
        Assert.Contains("KHONG chua noi dung du lieu ca nhan", dong, StringComparison.Ordinal);
    }

    [Fact]
    public void TC11_moi_truong_trong_dong_tien_de_va_canh_bao_deu_DUOC_BOC()
    {
        // 🔑 Chong *"bo quen boc mot truong"* — 📌 do la loi **khong trieu chung** neu truong do
        // **tinh co khong co dau phay** o ca kiem.
        var dong = CsvNhatKy.DongCanhBaoCat(1, 2, DateTime.UtcNow);

        var truong = DocDongCsv(dong);

        Assert.Equal(4, truong.Count);
        // 📌 Moi truong phai duoc boc ⇒ 🔑 khong truong nao chua `"` o giua sau khi doc.
        Assert.All(truong, t => Assert.DoesNotContain("\"", t, StringComparison.Ordinal));
    }

    // ── ⑤ DẤU PHÂN CÁCH `;` CHO EXCEL LOCALE VIỆT (`S10`, nợ §27.6 #5) ───────────────────────

    [Fact]
    public void TC12_cham_phay_la_dau_phan_cach_THAT__khong_chi_doi_ten()
    {
        // 🚨 **NỢ §27.6 #5:** ở locale dùng `,` làm **dấu thập phân** (tiếng Việt), Excel coi `;` là
        // **dấu ngăn cách danh sách** ⇒ 🔒 tệp `,` mở ra thành **ĐÚNG MỘT CỘT**.
        // ⚠️ **BOM không cứu được** — BOM là **bảng mã**, không phải **dấu phân cách** (hai việc khác nhau;
        // 📌 đây chính là chỗ dễ kết luận sai: *"đã có BOM nên Excel đọc được"* — không đúng).
        var dong = CsvNhatKy.DongVoi(CsvNhatKy.PhanCachChamPhay, "a", "b,c", "d");

        Assert.Equal("\"a\";\"b,c\";\"d\"", dong);

        // 🔑 Và **đếm bằng bộ đọc thật** (không đếm dấu `;`): 3 trường, trường 2 giữ nguyên `b,c`.
        var truong = DocDongCsv(dong, CsvNhatKy.PhanCachChamPhay);
        Assert.Equal(3, truong.Count);
        Assert.Equal("b,c", truong[1]);

        // ➕ Và **cùng dữ liệu, hai dấu khác nhau ⇒ đầu ra PHẢI khác nhau.** 📌 Nếu không kiểm phép này thì
        // một `DongVoi` **bỏ qua tham số** (luôn dùng `,`) vẫn cho kết quả "trông đúng" ở mọi ca khác.
        Assert.NotEqual(CsvNhatKy.Dong("a", "b"), CsvNhatKy.DongVoi(CsvNhatKy.PhanCachChamPhay, "a", "b"));
    }

    [Fact]
    public void TC13_gia_tri_CHUA_dau_phan_cach_dang_dung_van_duoc_boc__ca_HAI_che_do()
    {
        // 🔑 **ĐO HAI CHIỀU:** một tệp `;` mà dữ liệu có `;` thì phải bọc — nếu không, cột bị **lệch**.
        // 📌 Và luật bọc là **MỘT** (`Boc` luôn bọc) cho cả hai chế độ, nên ca này phải xanh ở **cả hai**.
        // ⚠️ Nếu ai "tối ưu" bằng cách chỉ bọc khi **thấy** dấu phân cách của chế độ hiện tại thì dữ liệu
        // chứa `;` trong tệp `,` sẽ **không** được bọc ⇒ 🔒 cột lệch, và người đọc **không hề biết**.
        var coChamPhay = "a;b";
        var coPhay = "a,b";

        Assert.Equal("\"a;b\"", CsvNhatKy.Boc(coChamPhay));
        Assert.Equal("\"a,b\"", CsvNhatKy.Boc(coPhay));

        // Trong tệp `;`, trường chứa `;` vẫn là **MỘT** trường.
        var dongChamPhay = CsvNhatKy.DongVoi(CsvNhatKy.PhanCachChamPhay, coChamPhay, "x");
        Assert.Equal(2, DocDongCsv(dongChamPhay, CsvNhatKy.PhanCachChamPhay).Count);

        // ➕ **Chiều ngược lại:** trong tệp `,`, trường chứa `,` cũng là **MỘT** trường (hành vi cũ, không bị
        // hồi quy bởi thay đổi `S10`).
        var dongPhay = CsvNhatKy.Dong(coPhay, "x");
        Assert.Equal(2, DocDongCsv(dongPhay, CsvNhatKy.PhanCachPhay).Count);
    }

    [Fact]
    public void TC14_dau_phan_cach_LA_thi_NEM__de_tep_muon_khong_bao_gio_duoc_tao()
    {
        // ⚠️ Một dấu lạ (vd `|`) tạo ra tệp **hợp lệ về cú pháp** mà **không trình đọc nào** hiểu
        // ⇒ 📌 cùng họ với "tệp bằng chứng đọc sai": người nhận tưởng dữ liệu có 1 cột.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CsvNhatKy.DongVoi('|', "a", "b"));
    }

    [Fact]
    public void TC15_dong_tien_de_TU_KHAI_dau_phan_cach__vi_tep_roi_khoi_he_thong()
    {
        var luc = new DateTime(2026, 10, 21, 2, 3, 4, DateTimeKind.Utc);

        var tienDePhay = CsvNhatKy.DongTienDe("admin", luc, 2, 2);
        var tienDeChamPhay = CsvNhatKy.DongTienDe("admin", luc, 2, 2, CsvNhatKy.PhanCachChamPhay);

        Assert.Contains("dau-phay", tienDePhay, StringComparison.Ordinal);
        Assert.Contains("cham-phay", tienDeChamPhay, StringComparison.Ordinal);

        // 🔑 Dòng tiền đề của tệp `;` phải **thực sự** dùng `;` — 📌 nếu không thì chính dòng mô tả cũng
        // bị đọc thành một cột trong Excel, và lời mô tả trở nên vô dụng.
        Assert.Contains("\";\"", tienDeChamPhay, StringComparison.Ordinal);
        Assert.DoesNotContain("\";\"", tienDePhay, StringComparison.Ordinal);
    }

    [Fact]
    public void TC16_doc_tham_so_phan_cach__va_gia_tri_LA_tra_NULL__khong_im_lang_roi_ve_phay()
    {
        // 📌 Không truyền ⇒ **dấu phẩy** (tương thích ngược với 11 ca E2E `D15*` và mọi tệp đã xuất).
        Assert.Equal(CsvNhatKy.PhanCachPhay, CsvNhatKy.DocPhanCach(null));
        Assert.Equal(CsvNhatKy.PhanCachPhay, CsvNhatKy.DocPhanCach(""));
        Assert.Equal(CsvNhatKy.PhanCachPhay, CsvNhatKy.DocPhanCach("phay"));
        Assert.Equal(CsvNhatKy.PhanCachChamPhay, CsvNhatKy.DocPhanCach("cham-phay"));

        // 🔑 Không phân biệt hoa/thường — 📌 người dùng gõ tay trên URL.
        Assert.Equal(CsvNhatKy.PhanCachChamPhay, CsvNhatKy.DocPhanCach("CHAM-PHAY"));

        // 🚨 **CHIỀU QUAN TRỌNG NHẤT:** giá trị **lạ** ⇒ `null` (controller trả **400**).
        // ⚠️ Nếu rơi im lặng về dấu phẩy thì một lần gõ sai (`champhay`) tạo ra **tệp SAI** mà **không ai
        // biết** — 📌 và người kiểm toán sẽ **tin** rằng họ đang cầm bản `;`.
        Assert.Null(CsvNhatKy.DocPhanCach("champhay"));
        Assert.Null(CsvNhatKy.DocPhanCach(";"));
        Assert.Null(CsvNhatKy.DocPhanCach("dau-cham-phay"));
    }

    [Fact]
    public void TC17_ten_tep_PHAI_khac_nhau_giua_hai_dau_phan_cach()
    {
        // 🔒 Hai bản khác dấu phân cách mà **cùng tên** ⇒ tải về cùng thư mục thì trình duyệt thêm `(1)`
        // ⇒ 📌 không còn cách nào biết **bản nào là bản nào** (đúng loại lỗi bằng chứng).
        Assert.Equal(string.Empty, CsvNhatKy.HauToTenTep(CsvNhatKy.PhanCachPhay));
        Assert.Equal("-cham-phay", CsvNhatKy.HauToTenTep(CsvNhatKy.PhanCachChamPhay));

        // 📌 Và **giữ nguyên** tên tệp cũ cho dấu phẩy (neo E2E `D15d` đang khẳng định chuỗi đó).
        Assert.DoesNotContain("-cham-phay", "nhat-ky-xuat-du-lieu-ca-nhan-20261021-120000"
            + CsvNhatKy.HauToTenTep(CsvNhatKy.PhanCachPhay), StringComparison.Ordinal);
    }

    // ── BO DOC CSV TOI GIAN (chi de KIEM, khong phai san pham) ────────────────────────────────

    /// <summary>
    /// 🔑 **BO DOC CSV TOI GIAN** theo RFC 4180 — 📌 chi de **kiem**, khong phai ma san pham.
    /// 🔒 **VI SAO CAN:** neu test chi **dem dau phay** thi no se **dem sai** khi du lieu co dau phay
    /// trong `"…"` ⇒ ⚠️ tuc la test **tu vo hieu** dung o cho no can nhat (`TC07`).
    /// ➕ `S10`: nhận **dấu phân cách** — ⚠️ nếu bộ đọc luôn tách theo `,` thì các ca dấu `;` sẽ **xanh giả**
    /// (chúng sẽ "thấy" một trường duy nhất và mọi phép đếm đều bằng 1).
    /// </summary>
    private static List<string> DocDongCsv(string dong, char phanCach = ',')
    {
        var ketQua = new List<string>();
        var sb = new StringBuilder();
        var trongBoc = false;

        for (var i = 0; i < dong.Length; i++)
        {
            var c = dong[i];

            if (trongBoc)
            {
                if (c == '"')
                {
                    // `""` ⇒ mot dau `"` that; `"` don ⇒ dong truong.
                    if (i + 1 < dong.Length && dong[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        trongBoc = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else if (c == '"')
            {
                trongBoc = true;
            }
            else if (c == phanCach)
            {
                ketQua.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        ketQua.Add(sb.ToString());

        return ketQua;
    }
}
