/**
 * Where the backend lives, relative to the page.
 *
 * The app is not served from the host root: Vite builds it at `base: '/snipit/'` and in the
 * cluster the ingress routes `/snipit/api` to this backend, because the host is shared with the
 * other apps. A request to `/api/...` would therefore reach a *different* application, not a 404 —
 * which is why this cannot be left to chance.
 *
 * Derived from Vite's `BASE_URL` rather than hard-coded so the two can never disagree; the
 * trailing slash is dropped because `fetchBaseQuery` joins `base + url` and every generated URL
 * already starts with `/api`.
 */
export const API_BASE = import.meta.env.BASE_URL.replace(/\/$/, '');

/** Prefixes an app-absolute path (`/api/...`) for use outside RTK Query — e.g. a `<video src>`. */
export function apiUrl(path: string): string {
  return `${API_BASE}${path}`;
}
