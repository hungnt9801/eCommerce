# ══════════════════════════════════════════════════════════════════════════════════════════════════
# Web Admin (Next.js 15) — image PRODUCTION (`WP-10`).
#
# 🔒 BUILD CONTEXT = **GỐC REPO**, không phải `apps/admin-web`:
#   pnpm workspace cần `pnpm-lock.yaml` + `packages/api-client` (kiểu DTO sinh từ OpenAPI) ⇒ context hẹp
#   thì `pnpm install --frozen-lockfile` không chạy được. Vì vậy phải có `.dockerignore` ở **gốc repo**,
#   nếu không Docker gửi cả `node_modules`, `.git`, `artifacts/` (hàng GB) sang daemon mỗi lần build.
#
# Cách dùng:
#   docker compose up -d --build admin-web          # qua compose (khuyến nghị)
#   docker build -f apps/admin-web/Dockerfile -t ecommerce-admin-web .   # build trực tiếp
# ══════════════════════════════════════════════════════════════════════════════════════════════════

# ── Tầng 1: cài dependency + build ────────────────────────────────────────────────────────────────
FROM node:22-alpine AS builder

WORKDIR /repo

# 📌 `corepack` đọc `packageManager: pnpm@10.22.0` trong `package.json` gốc ⇒ **đúng** phiên bản pnpm
#    đã sinh `pnpm-lock.yaml`. Dùng pnpm khác phiên bản thì `--frozen-lockfile` có thể báo lệch.
RUN corepack enable && corepack prepare pnpm@10.22.0 --activate

# ① Chỉ manifest trước (tầng cache): sửa mã nguồn KHÔNG phải cài lại dependency.
COPY pnpm-workspace.yaml pnpm-lock.yaml package.json ./
COPY apps/admin-web/package.json apps/admin-web/package.json
COPY packages/api-client/package.json packages/api-client/package.json

# 📌 `--frozen-lockfile`: image phải dựng từ **đúng** lockfile đã commit — cài "trôi" trong image là
#    nguồn gốc của *"máy dev chạy được, container không"*.
RUN pnpm install --frozen-lockfile

# ② Mã nguồn thật.
COPY packages/api-client packages/api-client
COPY apps/admin-web apps/admin-web

# 🚨 `NEXT_PUBLIC_*` được **NHÚNG LÚC BUILD** — truyền bằng `build.args` (compose) hoặc `--build-arg`.
#    Đặt nó ở `environment:` lúc chạy là **no-op im lặng** (xem `lib/public-config.ts`).
ARG NEXT_PUBLIC_APP_NAME=eCommerce
ENV NEXT_PUBLIC_APP_NAME=${NEXT_PUBLIC_APP_NAME}

# 📌 `NEXT_TELEMETRY_DISABLED`: image không nên gọi về Internet lúc build.
ENV NEXT_TELEMETRY_DISABLED=1
RUN pnpm --filter @ecommerce/admin-web build

# ── Tầng 2: runtime tối giản ───────────────────────────────────────────────────────────────────────
FROM node:22-alpine AS runner

WORKDIR /app

# 📌 `NODE_ENV=production` phải đặt **trước** khi chạy server: Next bật cache/optimize theo biến này.
ENV NODE_ENV=production \
    PORT=3000 \
    HOSTNAME=0.0.0.0 \
    NEXT_TELEMETRY_DISABLED=1

# 📌 Chạy bằng user không phải root (image `node` có sẵn user `node`).
USER node

# `.next/standalone` (monorepo) có dạng:
#   /app/server.js?  → không: giữ nguyên cấu trúc `apps/admin-web/server.js` + `node_modules` ở `/app`.
COPY --from=builder --chown=node:node /repo/apps/admin-web/.next/standalone ./
COPY --from=builder --chown=node:node /repo/apps/admin-web/.next/static ./apps/admin-web/.next/static
# ⚠️ KHÔNG copy `public/`: app này không có thư mục đó. Thêm `public/` sau này thì phải thêm một dòng COPY
#    ở đây (nếu không, ảnh/font trong `public/` sẽ 404 trong container dù dev vẫn thấy).

EXPOSE 3000

# 📌 Healthcheck dùng **chính** `/api/health` — endpoint đó trả 503 khi Host chết, nên trạng thái
#    health của container phản ánh **cả chuỗi** (Next → Host), không chỉ "process còn sống".
#    `--start-period` đủ dài cho lần khởi động đầu (Next nạp route lần đầu chậm hơn các lần sau).
HEALTHCHECK --interval=15s --timeout=5s --start-period=60s --retries=3 \
    CMD wget -q -O /dev/null http://127.0.0.1:3000/api/health || exit 1

CMD ["node", "apps/admin-web/server.js"]
