import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

/**
 * Ghép class CSS có điều kiện và gộp class Tailwind xung đột.
 *
 * `cn("p-2", "p-4")` → `"p-4"` (mục đích của `tailwind-merge`): lớp sau thắng
 * theo thứ tự Tailwind, không phụ thuộc thứ tự chuỗi.
 */
export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}
