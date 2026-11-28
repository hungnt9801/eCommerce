// `jest-axe@11.0.0` không kèm khai báo kiểu. Khai báo tối thiểu phần mà gói
// dùng; matcher được đăng ký trong `setup.ts`.
declare module "jest-axe" {
  import type { AxeResults, RunOptions } from "axe-core";

  export function axe(
    html: Element | string,
    options?: RunOptions
  ): Promise<AxeResults>;

  export const toHaveNoViolations: {
    toHaveNoViolations(results: AxeResults): {
      pass: boolean;
      message: () => string;
      actual: unknown;
    };
  };
}
