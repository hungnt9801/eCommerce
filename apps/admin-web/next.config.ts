import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  reactStrictMode: true,
  // Cho phép build trong monorepo pnpm
  outputFileTracingRoot: undefined,
};

export default nextConfig;
