"use client";

import { useMemo, useState, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import { docLoiApi } from "@/lib/api-error";
import {
  type PromotionConditionGroupType,
  type PromotionDetailDto,
  type PromotionDto,
  type PromotionRuleCatalogItemDto,
  type PromotionStatus,
  type PromotionTreeNodeInput,
  type PromotionTreeValidationResultDto,
  NHAN_COD_CAN_XAC_THUC,
  canhBaoChiPhiSeed,
  dongHoGioiHan,
  laDichSetStatusHopLe,
  moTaLoiPromotion,
  mucCanhBao,
  nhanNhom,
  nhanRule,
  nhanTrangThai,
  thieuNhanRule,
} from "@/lib/promotion";

/**
 * **DỰNG CÂY ĐIỀU KIỆN + DUYỆT + ĐỔI TRẠNG THÁI** — `PRO-002` / `W24-3` (SRS §10.1).
 *
 * 🚨 **ĐỒNG HỒ GIỚI HẠN** (`D-288`): trần lấy **duy nhất** từ `promotion.limits` (đọc từ server). **KHÔNG**
 * fallback hằng số `64/8/32/1024`. Số đếm client (`demCay`) chỉ bị **số chính thức** của server thay **sau**
 * khi lưu **thành công**.
 *
 * 🚨 Khi `save-tree` lỗi `0001`/`0002`: **bắt buộc** tải lại `GET /api/v1/promotion/{id}` để cập nhật
 * `limits` (trần có thể đã đổi giữa chừng) rồi mới hiện lỗi.
 *
 * 🚨 **Nhãn lá** = `label` từ `rule-catalog` (không hiện `SystemName` thô); `label === systemName` ⇒ coi là
 * **thiếu nhãn** (`D-290`). 4 rule v1 **không** có form tham số ⇒ `configJson` để trống (`D-287`).
 *
 * 🚨 **Chống bấm lặp** mọi nút ghi; `set-status` chỉ gửi `Paused`/`Active`/`Cancelled`.
 */
export function TreeBuilder({
  business,
  promotion,
  catalog,
  coQuyenManage,
  coQuyenDuyet,
}: {
  business: string;
  promotion: PromotionDetailDto;
  catalog: PromotionRuleCatalogItemDto[];
  coQuyenManage: boolean;
  coQuyenDuyet: boolean;
}) {
  const router = useRouter();
  const id = promotion.id ?? "";

  const [status, setStatus] = useState<PromotionStatus>((promotion.status ?? 0) as PromotionStatus);
  const [limits, setLimits] = useState(promotion.limits ?? {});
  const [nodes, setNodes] = useState<PromotionTreeNodeInput[]>(() =>
    (promotion.nodes ?? []).map(tuDto),
  );
  const [serverSoDo, setServerSoDo] = useState<PromotionTreeValidationResultDto | null>(null);

  const [dangLuu, setDangLuu] = useState(false);
  const [dangDuyet, setDangDuyet] = useState(false);
  const [dangDoiTrangThai, setDangDoiTrangThai] = useState(false);
  const [loi, setLoi] = useState<string | null>(null);
  const [thongBao, setThongBao] = useState<string | null>(null);

  // ── Số đếm + đồng hồ giới hạn ─────────────────────────────────────────────────────────────────────
  const dongHo = useMemo(() => dongHoGioiHan(limits, nodes, catalog), [limits, nodes, catalog]);
  const goc = useMemo(() => nodes.filter((n) => (n.parentId ?? null) === null), [nodes]);
  const dung1Goc = goc.length === 1;

  // 📌 Số chính thức của server chỉ thay số client **sau** khi lưu thành công (serverSoDo bị xoá khi sửa cây).
  const hienThi = serverSoDo
    ? {
        nodeCount: serverSoDo.nodeCount ?? 0,
        depth: serverSoDo.depth ?? 0,
        leafCount: serverSoDo.leafCount ?? 0,
        declaredCost: serverSoDo.declaredCost ?? 0,
      }
    : dongHo.soDo;

  const lyDoKhoa = useMemo<string | null>(() => {
    if (status !== 0) return "Chỉ sửa được cây khi khuyến mãi ở trạng thái Nháp (Draft).";
    if (!coQuyenManage) return "Cần quyền Promotions.Manage để lưu cây.";
    if (nodes.length === 0) return "Cây chưa có điều kiện — thêm nhóm gốc.";
    if (!dung1Goc) return "Cây phải có đúng MỘT node gốc.";
    if (dongHo.khoa) return dongHo.lyDo;
    return null;
  }, [status, coQuyenManage, nodes.length, dung1Goc, dongHo]);

  const coTheLuu = lyDoKhoa === null && !dangLuu;

  // ── Sửa cây (xoá serverSoDo ⇒ quay lại số client) ─────────────────────────────────────────────────
  function suaCay(fn: (cu: PromotionTreeNodeInput[]) => PromotionTreeNodeInput[]) {
    setNodes((cu) => fn(cu));
    setServerSoDo(null);
    setThongBao(null);
    setLoi(null);
  }

  function themNhom(parentId: string | null) {
    suaCay((cu) => [
      ...cu,
      {
        id: uuid(),
        parentId,
        isGroup: true,
        interactionType: 0 as PromotionConditionGroupType,
        ruleSystemName: null,
        configJson: null,
        sortOrder: demCon(cu, parentId),
      },
    ]);
  }

  function themLa(parentId: string | null) {
    const macDinh = catalog[0]?.systemName ?? null;
    suaCay((cu) => [
      ...cu,
      {
        id: uuid(),
        parentId,
        isGroup: false,
        interactionType: undefined,
        ruleSystemName: macDinh,
        configJson: null,
        sortOrder: demCon(cu, parentId),
      },
    ]);
  }

  function xoaNode(nodeId: string) {
    suaCay((cu) => {
      // Xoá node + toàn bộ hậu duệ.
      const xoa = new Set<string>([nodeId]);
      let thayDoi = true;
      while (thayDoi) {
        thayDoi = false;
        for (const n of cu) {
          if (n.parentId && xoa.has(n.parentId) && n.id && !xoa.has(n.id)) {
            xoa.add(n.id);
            thayDoi = true;
          }
        }
      }
      return cu.filter((n) => !n.id || !xoa.has(n.id));
    });
  }

  function doiNhom(nodeId: string, kieu: PromotionConditionGroupType) {
    suaCay((cu) => cu.map((n) => (n.id === nodeId ? { ...n, interactionType: kieu } : n)));
  }

  function doiRule(nodeId: string, systemName: string) {
    suaCay((cu) => cu.map((n) => (n.id === nodeId ? { ...n, ruleSystemName: systemName } : n)));
  }

  // ── Gọi API ───────────────────────────────────────────────────────────────────────────────────────
  async function taiLaiLimits() {
    try {
      const res = await fetch(`/api/proxy/v1/promotion/${encodeURIComponent(id)}`);
      if (res.ok) {
        const d = (await res.json()) as PromotionDetailDto;
        if (d.limits) setLimits(d.limits);
      }
    } catch {
      /* bỏ qua — đã có thông báo lỗi chính */
    }
  }

  async function luuCay() {
    if (dangLuu || lyDoKhoa !== null) return;
    setDangLuu(true);
    setLoi(null);
    setThongBao(null);

    const payload = {
      nodes: nodes.map((n) => ({
        id: n.id,
        parentId: n.parentId ?? null,
        isGroup: !!n.isGroup,
        interactionType: n.isGroup ? (n.interactionType ?? 0) : undefined,
        ruleSystemName: n.isGroup ? null : n.ruleSystemName ?? null,
        configJson: null, // 4 rule v1 không có tham số — D-287
        sortOrder: n.sortOrder ?? 0,
      })),
    };

    try {
      const res = await fetch(`/api/proxy/v1/promotion/${encodeURIComponent(id)}/save-tree`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });

      if (!res.ok) {
        const failure = docLoiApi(await res.json().catch(() => null), res.status);
        // 🚨 0001/0002: trần có thể đã đổi giữa chừng ⇒ BẮT BUỘC tải lại chi tiết để cập nhật limits.
        if (
          failure.code === "eCommerce:Promotion:0001" ||
          failure.code === "eCommerce:Promotion:0002"
        ) {
          await taiLaiLimits();
        }
        setLoi(moTaLoiPromotion(failure.code) ?? failure.message);
        return;
      }

      const kq = (await res.json()) as PromotionTreeValidationResultDto;
      setServerSoDo(kq);
      setThongBao(
        `Đã lưu cây — ${kq.nodeCount ?? 0} điều kiện, độ sâu ${kq.depth ?? 0}, ${kq.leafCount ?? 0} lá, ${kq.declaredCost ?? 0} tổ hợp khai báo.`,
      );
    } catch {
      setLoi("Không gọi được API (mất mạng?).");
    } finally {
      setDangLuu(false);
    }
  }

  async function duyet() {
    if (dangDuyet) return;
    setDangDuyet(true);
    setLoi(null);
    setThongBao(null);
    try {
      const res = await fetch(`/api/proxy/v1/promotion/${encodeURIComponent(id)}/approve`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
      });
      if (!res.ok) {
        const failure = docLoiApi(await res.json().catch(() => null), res.status);
        setLoi(moTaLoiPromotion(failure.code) ?? failure.message);
        return;
      }
      const p = (await res.json()) as PromotionDto;
      if (p.status != null) setStatus(p.status);
      setThongBao("Đã duyệt khuyến mãi — cây đã duyệt là BẤT BIẾN; muốn đổi phải tạo khuyến mãi mới.");
      router.refresh();
    } catch {
      setLoi("Không gọi được API (mất mạng?).");
    } finally {
      setDangDuyet(false);
    }
  }

  async function doiTrangThai(dich: PromotionStatus) {
    if (dangDoiTrangThai) return;
    if (!laDichSetStatusHopLe(dich)) {
      setLoi("Chỉ chuyển sang Tạm dừng / Đang chạy / Đã huỷ.");
      return;
    }
    setDangDoiTrangThai(true);
    setLoi(null);
    setThongBao(null);
    try {
      const res = await fetch(`/api/proxy/v1/promotion/${encodeURIComponent(id)}/set-status`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ status: dich }),
      });
      if (!res.ok) {
        const failure = docLoiApi(await res.json().catch(() => null), res.status);
        setLoi(moTaLoiPromotion(failure.code) ?? failure.message);
        return;
      }
      const p = (await res.json()) as PromotionDto;
      if (p.status != null) setStatus(p.status);
      setThongBao(`Đã chuyển trạng thái sang «${nhanTrangThai(p.status)}».`);
      router.refresh();
    } catch {
      setLoi("Không gọi được API (mất mạng?).");
    } finally {
      setDangDoiTrangThai(false);
    }
  }

  // ── Cảnh báo seed/COD theo rule đang dùng ─────────────────────────────────────────────────────────
  const ruleDangDung = useMemo(
    () =>
      Array.from(new Set(nodes.filter((n) => !n.isGroup).map((n) => n.ruleSystemName ?? ""))).filter(
        (x) => x !== "",
      ),
    [nodes],
  );
  const canhBaoSeed = ruleDangDung
    .map((ten) => catalog.find((r) => (r.systemName ?? "") === ten))
    .map((r) => (r ? canhBaoChiPhiSeed(r) : null))
    .filter((x): x is string => x !== null);
  const coCod = ruleDangDung.some(
    (ten) => catalog.find((r) => (r.systemName ?? "") === ten)?.requiresVerifiedCod === true,
  );

  // ── Duyệt cây ─────────────────────────────────────────────────────────────────────────────────────
  const conCua = useMemo(() => {
    const m = new Map<string, PromotionTreeNodeInput[]>();
    for (const n of nodes) {
      const k = n.parentId ?? "";
      if (!m.has(k)) m.set(k, []);
      m.get(k)!.push(n);
    }
    return m;
  }, [nodes]);

  function renderNode(node: PromotionTreeNodeInput, level: number): ReactNode {
    const con = conCua.get(node.id ?? "") ?? [];
    const pad = { paddingLeft: level * 18 } as const;

    if (node.isGroup) {
      const kieu = (node.interactionType ?? 0) as PromotionConditionGroupType;
      return (
        <div key={node.id} className="border rounded p-2 my-1" style={{ ...pad, borderColor: "var(--border)" }}>
          <div className="flex flex-wrap items-center gap-2">
            <span
              className="text-[11px] font-semibold px-2 py-0.5 rounded"
              style={{ background: kieu === 1 ? "#eef2ff" : "#ecfdf5", color: "var(--primary)" }}
            >
              NHÓM {nhanNhom(kieu)}
            </span>
            <label className="text-[11px] flex items-center gap-1">
              <span style={{ color: "var(--text-muted)" }}>Kiểu nhóm</span>
              <select
                className="nh-input"
                value={String(kieu)}
                disabled={!coQuyenManage || status !== 0}
                onChange={(e) => doiNhom(node.id ?? "", Number(e.target.value) as PromotionConditionGroupType)}
                style={{ width: 90 }}
              >
                <option value="0">AND</option>
                <option value="1">OR</option>
              </select>
            </label>
            {coQuyenManage && status === 0 && (
              <div className="ml-auto flex items-center gap-1">
                <button type="button" className="nh-btn text-[12px]" onClick={() => themNhom(node.id ?? null)}>
                  + Nhóm con
                </button>
                <button type="button" className="nh-btn text-[12px]" onClick={() => themLa(node.id ?? null)}>
                  + Điều kiện
                </button>
                <button
                  type="button"
                  className="nh-btn text-[12px]"
                  style={{ color: "var(--danger)" }}
                  onClick={() => xoaNode(node.id ?? "")}
                >
                  Xoá
                </button>
              </div>
            )}
          </div>

          {con.length === 0 ? (
            <div className="text-[11px] mt-1" style={{ color: "var(--text-muted)" }}>
              Nhóm rỗng — thêm nhóm con hoặc điều kiện.
            </div>
          ) : (
            <div className="mt-1">{con.map((c) => renderNode(c, level + 1))}</div>
          )}
        </div>
      );
    }

    // Lá
    const rule = catalog.find((r) => (r.systemName ?? "") === (node.ruleSystemName ?? ""));
    const thongTin = rule ?? { systemName: node.ruleSystemName ?? "", label: node.ruleSystemName ?? "" };
    const thieuNhan = thieuNhanRule(thongTin);
    const seed = rule ? canhBaoChiPhiSeed(rule) : null;
    const cod = rule?.requiresVerifiedCod === true;

    return (
      <div
        key={node.id}
        className="flex flex-wrap items-center gap-2 border rounded p-2 my-1"
        style={{ ...pad, borderColor: "var(--border)" }}
      >
        <span className="text-[11px]" style={{ color: "var(--text-muted)" }}>
          Điều kiện:
        </span>
        <select
          className="nh-input"
          value={node.ruleSystemName ?? ""}
          disabled={!coQuyenManage || status !== 0}
          onChange={(e) => doiRule(node.id ?? "", e.target.value)}
          style={{ minWidth: 240 }}
        >
          {!rule && <option value={node.ruleSystemName ?? ""}>{node.ruleSystemName ?? "—"}</option>}
          {catalog.map((r) => (
            <option key={r.systemName ?? ""} value={r.systemName ?? ""}>
              {nhanRule(r)}
            </option>
          ))}
        </select>

        {thieuNhan && (
          <span className="text-[11px] px-2 py-0.5 rounded" style={{ background: "#fff7ed", color: "var(--warning)" }}>
            ⚠️ thiếu nhãn
          </span>
        )}
        {cod && (
          <span className="text-[11px]" style={{ color: "var(--warning)" }} title={NHAN_COD_CAN_XAC_THUC}>
            {NHAN_COD_CAN_XAC_THUC}
          </span>
        )}

        {coQuyenManage && status === 0 && (
          <button
            type="button"
            className="ml-auto nh-btn text-[12px]"
            style={{ color: "var(--danger)" }}
            onClick={() => xoaNode(node.id ?? "")}
          >
            Xoá
          </button>
        )}

        {seed && (
          <div className="basis-full text-[11px]" style={{ color: "var(--warning)" }}>
            ⚠️ {seed}
          </div>
        )}
      </div>
    );
  }

  const gocNodes = conCua.get("") ?? [];

  // ── Nút đổi trạng thái theo trạng thái hiện tại ───────────────────────────────────────────────────
  const nutTrangThai: Array<{ nhan: string; dich: PromotionStatus; nguyHiem?: boolean }> = (() => {
    if (status === 0) return [{ nhan: "Huỷ", dich: 5 as PromotionStatus, nguyHiem: true }];
    if (status === 1)
      return [
        { nhan: "Kích hoạt", dich: 2 as PromotionStatus },
        { nhan: "Huỷ", dich: 5 as PromotionStatus, nguyHiem: true },
      ];
    if (status === 2)
      return [
        { nhan: "Tạm dừng", dich: 3 as PromotionStatus },
        { nhan: "Huỷ", dich: 5 as PromotionStatus, nguyHiem: true },
      ];
    if (status === 3)
      return [
        { nhan: "Tiếp tục", dich: 2 as PromotionStatus },
        { nhan: "Huỷ", dich: 5 as PromotionStatus, nguyHiem: true },
      ];
    return [];
  })();

  return (
    <div className="space-y-3">
      {/* Trạng thái + nút duyệt / đổi trạng thái */}
      <div className="nh-card p-3 space-y-2">
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-[13px] font-semibold">{promotion.code} — {promotion.name}</span>
          <span
            className="text-[11px] px-2 py-0.5 rounded-full"
            style={{ background: "var(--bg-subtle)", color: "var(--text-muted)" }}
          >
            Trạng thái: <b>{nhanTrangThai(status)}</b>
          </span>

          <div className="ml-auto flex items-center gap-2">
            {coQuyenDuyet && status === 0 && (
              <button
                type="button"
                className="nh-btn nh-btn-primary"
                onClick={() => void duyet()}
                disabled={dangDuyet || dangLuu}
                title="Duyệt cây (Draft → Approved). Cây sai ⇒ hiện lỗi theo mã."
              >
                {dangDuyet ? "Đang duyệt…" : "Duyệt"}
              </button>
            )}
            {coQuyenManage &&
              nutTrangThai.map((b) => (
                <button
                  key={b.nhan}
                  type="button"
                  className="nh-btn"
                  style={b.nguyHiem ? { color: "var(--danger)" } : undefined}
                  onClick={() => void doiTrangThai(b.dich)}
                  disabled={dangDoiTrangThai || dangLuu}
                >
                  {b.nhan}
                </button>
              ))}
          </div>
        </div>

        {status !== 0 && (
          <div className="text-[11px]" style={{ color: "var(--text-muted)" }}>
            📌 Cây chỉ sửa được khi khuyến mãi ở <b>Nháp</b> (<code>D-159</code>). Muốn đổi cây ⇒ tạo khuyến
            mãi mới.
          </div>
        )}
        {!coQuyenDuyet && (
          <div className="text-[11px]" style={{ color: "var(--text-muted)" }}>
            Cần quyền <span className="font-mono">Promotions.Approve</span> để duyệt khuyến mãi.
          </div>
        )}

        {loi && (
          <div className="text-[12px]" style={{ color: "var(--danger)" }}>
            {loi}
          </div>
        )}
        {thongBao && (
          <div className="text-[12px]" style={{ color: "var(--success)" }}>
            {thongBao}
          </div>
        )}
      </div>

      {/* Đồng hồ giới hạn — trần DUY NHẤT từ promotion.limits */}
      <div className="nh-card p-3 space-y-2">
        <div className="flex items-center gap-2">
          <span className="text-[13px] font-semibold">Đồng hồ giới hạn cây</span>
          {serverSoDo && (
            <span className="text-[11px]" style={{ color: "var(--text-muted)" }}>
              (số chính thức từ server sau lần lưu)
            </span>
          )}
        </div>
        <div className="grid grid-cols-2 md:grid-cols-4 gap-2">
          {[
            { nhan: "Điều kiện", dung: hienThi.nodeCount, tran: limits.maxNodes ?? 0 },
            { nhan: "Độ sâu", dung: hienThi.depth, tran: limits.maxDepth ?? 0 },
            { nhan: "Lá", dung: hienThi.leafCount, tran: limits.maxLeaves ?? 0 },
            { nhan: "Tổ hợp khai báo", dung: hienThi.declaredCost, tran: limits.maxDeclaredCombinations ?? 0 },
          ].map((m) => {
            const muc = mucCanhBao(m.dung, m.tran);
            const mau =
              muc === "do" ? "var(--danger)" : muc === "vang" ? "var(--warning)" : "var(--text)";
            return (
              <div key={m.nhan} className="rounded p-2" style={{ background: "var(--bg-subtle)" }}>
                <div className="text-[11px]" style={{ color: "var(--text-muted)" }}>
                  {m.nhan}
                </div>
                <div className="text-[15px] font-semibold tabular" style={{ color: mau }}>
                  {m.dung} <span className="text-[11px]" style={{ color: "var(--text-muted)" }}>/ {m.tran}</span>
                </div>
              </div>
            );
          })}
        </div>
        {dongHo.canhBaoToHop !== "binh-thuong" && (
          <div
            className="text-[12px]"
            style={{ color: dongHo.canhBaoToHop === "do" ? "var(--danger)" : "var(--warning)" }}
          >
            ⚠️ Cây sắp chạm trần tổ hợp — nên tách thành nhiều khuyến mãi nhỏ hơn.
          </div>
        )}
      </div>

      {/* Cảnh báo seed / COD */}
      {(canhBaoSeed.length > 0 || coCod) && (
        <div className="nh-card p-3 space-y-1 text-[12px]" style={{ borderColor: "var(--warning)" }}>
          {coCod && <div style={{ color: "var(--warning)" }}>{NHAN_COD_CAN_XAC_THUC}</div>}
          {canhBaoSeed.map((c) => (
            <div key={c} style={{ color: "var(--warning)" }}>
              ⚠️ {c}
            </div>
          ))}
          <div className="text-[11px]" style={{ color: "var(--text-muted)" }}>
            📌 Cảnh báo cấu hình chi phí <b>không</b> khoá nút Lưu (<code>D-289</code>).
          </div>
        </div>
      )}

      {/* Cây điều kiện */}
      <div className="nh-card p-3 space-y-2">
        <div className="flex items-center gap-2">
          <span className="text-[13px] font-semibold">Cây điều kiện</span>
          {coQuyenManage && status === 0 && (
            <div className="ml-auto flex items-center gap-1">
              {nodes.length === 0 && (
                <button type="button" className="nh-btn nh-btn-primary" onClick={() => themNhom(null)}>
                  + Thêm nhóm gốc
                </button>
              )}
            </div>
          )}
        </div>

        {nodes.length === 0 ? (
          <div className="text-[12px] py-6 text-center" style={{ color: "var(--text-muted)" }}>
            Cây chưa có điều kiện. Bấm <b>“Thêm nhóm gốc”</b> để bắt đầu (mỗi cây cần đúng một gốc).
          </div>
        ) : (
          <div>{gocNodes.map((n) => renderNode(n, 0))}</div>
        )}

        {ruleDangDung.length > 0 && (
          <div className="text-[11px]" style={{ color: "var(--text-muted)" }}>
            📌 4 điều kiện v1 (<span className="font-mono">MinOrderAmount</span>,{" "}
            <span className="font-mono">ChannelScope</span>, <span className="font-mono">CustomerGroup</span>,{" "}
            <span className="font-mono">FirstOrderOnly</span>) lấy tham số từ khuyến mãi/ngữ cảnh khách — lá{" "}
            <b>không có</b> form tham số riêng (<code>D-287</code>).
          </div>
        )}
      </div>

      {/* Lưu cây */}
      <div className="nh-card p-3 flex flex-wrap items-center gap-2">
        <button
          type="button"
          className="nh-btn nh-btn-primary"
          onClick={() => void luuCay()}
          disabled={!coTheLuu}
          title={lyDoKhoa ?? "Lưu cây điều kiện"}
        >
          {dangLuu ? "Đang lưu…" : "Lưu cây"}
        </button>
        {lyDoKhoa && (
          <span className="text-[12px]" style={{ color: "var(--danger)" }}>
            Không lưu được: {lyDoKhoa}
          </span>
        )}
        <span className="ml-auto text-[11px]" style={{ color: "var(--text-muted)" }}>
          {serverSoDo ? "Số đo chính thức đã cập nhật." : "Số đo đang là ước tính phía client."}
        </span>
      </div>
    </div>
  );
}

function tuDto(n: NonNullable<PromotionDetailDto["nodes"]>[number]): PromotionTreeNodeInput {
  return {
    id: n.id,
    parentId: n.parentId ?? null,
    isGroup: n.isGroup,
    interactionType: n.isGroup ? n.interactionType ?? (0 as PromotionConditionGroupType) : undefined,
    ruleSystemName: n.isGroup ? null : n.ruleSystemName ?? null,
    configJson: null,
    sortOrder: n.sortOrder ?? 0,
  };
}

function demCon(cu: PromotionTreeNodeInput[], parentId: string | null): number {
  return cu.filter((n) => (n.parentId ?? null) === parentId).length;
}

function uuid(): string {
  const c = globalThis.crypto;
  if (c && typeof c.randomUUID === "function") return c.randomUUID();
  return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (ch) => {
    const r = (Math.random() * 16) | 0;
    const v = ch === "x" ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}
