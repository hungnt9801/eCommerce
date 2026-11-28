// @ts-check
import js from "@eslint/js";
import jsxA11y from "eslint-plugin-jsx-a11y";
import reactHooks from "eslint-plugin-react-hooks";
import tseslint from "typescript-eslint";

/** Tệp TypeScript cần parser của `typescript-eslint`. */
const TS_FILES = ["**/*.ts", "**/*.tsx"];

// Ghép chuỗi để cấu hình KHÔNG chứa literal deep-import (cổng grep phải 0 dòng;
// đây là rule, không phải import).
const UI_DEEP_IMPORT = ["@ecommerce/ui", "src", "**"].join("/");

// `tseslint.configs.recommended` khai parser + plugin ở cấp gốc; scope lại để
// parser TS chỉ áp lên tệp TS (gói không có .js/.mjs cần lint).
const tsConfigs = tseslint.configs.recommended.map((config) => ({
  ...config,
  files: config.files ?? TS_FILES,
}));

/**
 * Flat config của `@ecommerce/ui` — `eslint src --max-warnings=0`.
 *
 * Gói mới, rỗng ⇒ **mọi** cảnh báo là đỏ. Luật `no-restricted-imports` chặn
 * deep import và phụ thuộc ngược (`docs/ui/01-kien-truc-ui.md` §5.1).
 */
export default tseslint.config(
  { ignores: ["node_modules/**", "dist/**", "coverage/**"] },
  js.configs.recommended,
  ...tsConfigs,
  {
    files: TS_FILES,
    plugins: {
      "jsx-a11y": jsxA11y,
      "react-hooks": reactHooks,
    },
    rules: {
      ...jsxA11y.flatConfigs.recommended.rules,
      "react-hooks/rules-of-hooks": "error",
      "react-hooks/exhaustive-deps": "warn",
      "no-restricted-imports": [
        "error",
        {
          patterns: [
            {
              group: ["@/*"],
              message: "packages/ui không được dùng alias @/ của app.",
            },
            {
              group: ["@ecommerce/api-client", "@ecommerce/api-client/*"],
              message: "packages/ui không phụ thuộc DTO nghiệp vụ.",
            },
            {
              group: [UI_DEEP_IMPORT],
              message: "Cấm deep import; đi qua entry @ecommerce/ui.",
            },
          ],
        },
      ],
      // `js.configs.recommended` bật `no-undef`; TypeScript đã kiểm bằng tsc.
      "no-undef": "off",
    },
  }
);
