/**
 * **BIỂU ĐỒ CỘT XẾP CHỒNG bằng SVG thuần** (`WP-08`) — không thêm thư viện chart.
 *
 * 🔑 **VÌ SAO KHÔNG THÊM `recharts`/`chart.js`:** trang báo cáo chỉ cần **một** loại biểu đồ (doanh thu theo
 * ngày, xếp chồng 2 nguồn). Thêm thư viện chart nghĩa là +100–300 kB JS vào **mọi** trang admin, kèm rủi ro
 * hydration (thư viện đo DOM) — trong khi SVG server-render là **tất định** và in được.
 *
 * 📌 **Trục Y bắt đầu từ 0** (không "cắt trục"): biểu đồ cắt trục làm thay đổi 1% trông như gấp đôi — với báo
 * cáo doanh thu thì đó là **nói dối bằng hình**.
 * 📌 Nhãn trục X **thưa dần** khi nhiều cột (30 ngày không chồng chữ) nhưng **không bỏ cột nào**.
 */
export type BarDiem = {
  label: string;
  a: number;
  b: number;
  aTen: string;
  bTen: string;
};

const RONG = 720;
const CAO = 220;
const LE_TRAI = 8;
const LE_DUOI = 26;
const LE_TREN = 10;

export function StackedBarChart({ points, aMau, bMau }: { points: BarDiem[]; aMau: string; bMau: string }) {
  if (points.length === 0) {
    return (
      <div className="text-[13px] py-8 text-center" style={{ color: "var(--text-muted)" }}>
        Chưa có dữ liệu để vẽ biểu đồ.
      </div>
    );
  }

  const tongLonNhat = Math.max(1, ...points.map((p) => p.a + p.b));
  const vungVe = RONG - LE_TRAI * 2;
  const buoc = vungVe / points.length;
  const rongCot = Math.max(2, Math.min(28, buoc * 0.62));
  const caoVe = CAO - LE_TREN - LE_DUOI;

  // Nhãn X: hiện tối đa ~8 nhãn, chia đều — mỗi nhãn vẫn đại diện cho một cột đã vẽ.
  const buocNhan = Math.max(1, Math.ceil(points.length / 8));

  return (
    <svg viewBox={`0 0 ${RONG} ${CAO}`} role="img" aria-label="Biểu đồ doanh thu" style={{ width: "100%", height: CAO }}>
      {/* Đường đáy + lưới ngang 3 mức (đọc số nhanh mà không cần trục Y chi tiết). */}
      {[0, 0.5, 1].map((ty) => {
        const y = LE_TREN + caoVe * (1 - ty);
        return (
          <line
            key={ty}
            x1={LE_TRAI}
            x2={RONG - LE_TRAI}
            y1={y}
            y2={y}
            stroke="currentColor"
            strokeOpacity={0.12}
            strokeWidth={1}
          />
        );
      })}

      {points.map((p, i) => {
        const x = LE_TRAI + i * buoc + (buoc - rongCot) / 2;
        const caoTong = ((p.a + p.b) / tongLonNhat) * caoVe;
        const caoA = (p.a / tongLonNhat) * caoVe;
        const caoB = (p.b / tongLonNhat) * caoVe;
        const yDay = LE_TREN + caoVe;

        return (
          <g key={`${p.label}-${i}`}>
            {(p.a + p.b) > 0 && (
              <title>
                {`${p.label}\n${p.aTen}: ${Math.round(p.a).toLocaleString("vi-VN")}\n${p.bTen}: ${Math.round(p.b).toLocaleString("vi-VN")}\nTổng: ${Math.round(p.a + p.b).toLocaleString("vi-VN")}`}
              </title>
            )}
            {/* Phần `b` (đơn hàng) ở dưới, phần `a` (bán lẻ) xếp trên. */}
            <rect x={x} y={yDay - caoB} width={rongCot} height={caoB} fill={bMau} rx={1} />
            <rect x={x} y={yDay - caoTong} width={rongCot} height={caoA} fill={aMau} rx={1} />
          </g>
        );
      })}

      {points.map((p, i) =>
        i % buocNhan === 0 ? (
          <text
            key={`nhan-${p.label}-${i}`}
            x={LE_TRAI + i * buoc + buoc / 2}
            y={CAO - 8}
            textAnchor="middle"
            fontSize={10}
            fill="currentColor"
            fillOpacity={0.65}
          >
            {p.label.length > 5 ? p.label.slice(5) : p.label}
          </text>
        ) : null,
      )}
    </svg>
  );
}
