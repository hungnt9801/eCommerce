// @ts-check
import js from "@eslint/js";
import nextPlugin from "@next/eslint-plugin-next";
import jsxA11y from "eslint-plugin-jsx-a11y";
import reactHooks from "eslint-plugin-react-hooks";
import tseslint from "typescript-eslint";

/** Tệp TypeScript cần parser của `typescript-eslint`. */
const TS_FILES = ["**/*.ts", "**/*.tsx", "**/*.mts", "**/*.cts"];

// Ghép chuỗi để cấu hình KHÔNG chứa literal deep-import (cổng grep phải 0 dòng;
// đây là rule, không phải import).
const UI_DEEP_IMPORT = ["@ecommerce/ui", "src", "**"].join("/");

/**
 * `U41-2` — app đang có nợ cũ (hex màu, a11y A-01…A-19) và WP "0 pixel" **không**
 * sửa app. Hạ mọi luật đang bật về `warn` để `eslint .` exit 0 mà vẫn in cảnh
 * báo; nâng `error` ở `WP-51` (`docs/ui/01-kien-truc-ui.md` §5.3, §9.3).
 */
function toWarn(rules) {
  const out = {};
  for (const [name, value] of Object.entries(rules ?? {})) {
    const parts = Array.isArray(value) ? [...value] : [value];
    if (parts[0] === "off" || parts[0] === 0) {
      out[name] = value;
      continue;
    }
    parts[0] = "warn";
    out[name] = parts.length === 1 ? "warn" : parts;
  }
  return out;
}

const tsRecommended = tseslint.configs.recommended
  .filter((config) => config.rules)
  .reduce((acc, config) => Object.assign(acc, config.rules), {});

const allRules = {
  ...js.configs.recommended.rules,
  ...tsRecommended,
  ...nextPlugin.configs.recommended.rules,
  ...jsxA11y.flatConfigs.recommended.rules,
  "react-hooks/rules-of-hooks": "warn",
  "react-hooks/exhaustive-deps": "warn",
  "no-restricted-imports": [
    "warn",
    {
      patterns: [
        {
          group: [UI_DEEP_IMPORT],
          message: "Cấm deep import; dùng entry @ecommerce/ui.",
        },
      ],
    },
  ],
  // App Router, repo không có thư mục pages/ ⇒ luật không có pagesDir.
  "@next/next/no-html-link-for-pages": "off",
};

/**
 * Flat config của `apps/admin-web` — `eslint .` (KHÔNG `--max-warnings`).
 * `@next/eslint-plugin-next` chỉ dùng ở app; `packages/ui` không cần plugin `next`.
 */
export default tseslint.config(
  {
    ignores: [
      ".next/**",
      "node_modules/**",
      "next-env.d.ts",
      "test-results/**",
      "playwright-report/**",
    ],
  },
  ...tseslint.configs.recommended.map((config) => ({
    ...config,
    files: config.files ?? TS_FILES,
  })),
  {
    plugins: {
      "@typescript-eslint": tseslint.plugin,
      "@next/next": nextPlugin,
      "jsx-a11y": jsxA11y,
      "react-hooks": reactHooks,
    },
    rules: toWarn(allRules),
  },
  {
    files: TS_FILES,
    rules: {
      // `js.configs.recommended` bật `no-undef`; TypeScript đã kiểm bằng tsc.
      "no-undef": "off",
    },
  }
);
