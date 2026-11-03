/**
 * Cấu hình môi trường của BFF.
 *
 * 🚨 `API_BASE_URL` phải trỏ tới **HTTP 54275** ở dev (D-16), không phải https 54274:
 * cert dev là self-signed ⇒ `fetch` của Node sẽ từ chối (`UNABLE_TO_VERIFY_LEAF_SIGNATURE`).
 */
export const API_BASE_URL = process.env.API_BASE_URL ?? "http://localhost:54275";

export const OAUTH_CLIENT_ID = process.env.OAUTH_CLIENT_ID ?? "eCommerce_App";
export const OAUTH_CLIENT_SECRET = process.env.OAUTH_CLIENT_SECRET ?? "1q2w3e*";
export const OAUTH_SCOPE = process.env.OAUTH_SCOPE ?? "eCommerce offline_access";

export const DEFAULT_BUSINESS = process.env.DEFAULT_BUSINESS ?? "230222";
