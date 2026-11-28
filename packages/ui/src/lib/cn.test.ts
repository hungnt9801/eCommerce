import { describe, expect, it } from "vitest";
import { cn } from "./cn";

describe("cn", () => {
  it("gộp class Tailwind xung đột: p-2 rồi p-4 ⇒ chỉ còn p-4", () => {
    expect(cn("p-2", "p-4")).toBe("p-4");
  });

  it("giữ nguyên class khi không xung đột", () => {
    expect(cn("text-sm", "font-medium")).toBe("text-sm font-medium");
  });

  it("xử lý class có điều kiện (false/undefined/null bị bỏ)", () => {
    const active = false;
    expect(cn("base", active && "active", undefined, null, "extra")).toBe(
      "base extra"
    );
  });
});
