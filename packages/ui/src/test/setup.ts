import { cleanup } from "@testing-library/react";
import { toHaveNoViolations } from "jest-axe";
import { afterEach, expect } from "vitest";

// `jest-axe` không kèm type; matcher `toHaveNoViolations` được khai báo ở
// `matchers.d.ts` (augment `vitest`) để tsc hiểu `expect(...).toHaveNoViolations()`.
expect.extend(toHaveNoViolations);

afterEach(() => {
  cleanup();
});
