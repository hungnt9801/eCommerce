using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <summary>
    /// 🚨 `S16b` (29/10/2026) — **BẰNG CHỨNG PHÁT LẠI DLQ** (`XC-002`).
    ///
    /// 🔑 **VÌ SAO PHẢI CÓ CỘT (không suy từ `DaXuLy`):** `DaXuLy` = *"đã xử lý"* — người trực có thể đánh dấu
    /// **bỏ qua có lý do** mà **không hề** phát lại. 🔒 Nếu dùng `DaXuLy` làm khoá chống phát trùng thì
    /// *"đã bỏ qua"* và *"đã phát lại"* bị gộp làm một, và lần phát lại **đầu tiên** của một dòng đã đánh dấu
    /// sẽ bị **chặn** (hoặc tệ hơn: phát **hai lần** ⇒ consumer nhận trùng).
    ///
    /// 📌 **KHÔNG** thêm unique index/`CHECK` mới ⇒ số index `NULLS NOT DISTINCT` **giữ nguyên 64**
    /// (đếm bằng `PostgreSqlSchemaTests`). Bất biến *"mỗi dòng chỉ phát lại một lần"* được giữ ở **entity**
    /// (`DeadLetterMessage.DanhDauDaPhatLai` ném `eCommerce:Messaging:0008`) + **app service** kiểm trước.
    /// ⚠️ Cố ý **không** đặt `CHECK` ở DB cho `ReplayCount`: nó sẽ khoá luôn khả năng *"phát lại sau khi sửa
    /// lỗi gốc"* nếu chính sách đó thay đổi — và 📌 một `CHECK` không đúng chính sách tương lai là **cái bẫy**
    /// cho người sau (phải migration để gỡ).
    /// </summary>
    public partial class Xc002_DlqReplay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 📌 Bộ đếm bắt đầu từ 0 cho **mọi** dòng cũ ⇒ *"chưa phát lại"* là trạng thái **đúng** lịch sử
            // (trước `S16b` không có đường phát lại nào). 🔒 Đặt `defaultValue: 1` sẽ **bịa** ra một lần
            // phát lại chưa từng xảy ra.
            migrationBuilder.AddColumn<int>(
                name: "ReplayCount",
                table: "MsgDeadLetters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReplayedAtUtc",
                table: "MsgDeadLetters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplayedBy",
                table: "MsgDeadLetters",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReplayCount",
                table: "MsgDeadLetters");

            migrationBuilder.DropColumn(
                name: "ReplayedAtUtc",
                table: "MsgDeadLetters");

            migrationBuilder.DropColumn(
                name: "ReplayedBy",
                table: "MsgDeadLetters");
        }
    }
}
