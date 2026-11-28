import { render } from "@testing-library/react";
import { axe } from "jest-axe";
import { describe, expect, it } from "vitest";

/**
 * Ca khung: chứng minh stack Vitest + Testing Library + axe chạy được trong
 * `packages/ui` (jsdom). Không phải test component nghiệp vụ.
 */
describe("nền test (Vitest + Testing Library + axe)", () => {
  it("render nút và không có vi phạm axe", async () => {
    const { container } = render(<button type="button">Lưu</button>);

    const results = await axe(container);

    expect(results).toHaveNoViolations();
  });
});
