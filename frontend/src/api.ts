export const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL || "https://localhost:7261").replace(/\/$/, "");

const ACCESS_KEY = "pm_access_token";
const REFRESH_KEY = "pm_refresh_token";
const ROLE_KEY = "pm_role";

export interface Session {
  accessToken: string;
  refreshToken: string;
  expiresAtUtc?: string;
  organizationId?: string | null;
  role?: string | null;
}

export interface Paged<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages?: number;
}

export class ApiError extends Error {
  constructor(message: string, public readonly status: number) {
    super(message);
    this.name = "ApiError";
  }
}

export function saveSession(session: Session) {
  sessionStorage.setItem(ACCESS_KEY, session.accessToken);
  sessionStorage.setItem(REFRESH_KEY, session.refreshToken);
  if (session.role) sessionStorage.setItem(ROLE_KEY, session.role);
  else sessionStorage.removeItem(ROLE_KEY);
}

export function clearSession() {
  [ACCESS_KEY, REFRESH_KEY, ROLE_KEY].forEach((key) => sessionStorage.removeItem(key));
}

export function hasSession() {
  return Boolean(sessionStorage.getItem(ACCESS_KEY));
}

export function getRole() {
  return sessionStorage.getItem(ROLE_KEY) || "";
}

let refreshPromise: Promise<boolean> | null = null;

async function refreshSession(): Promise<boolean> {
  const refreshToken = sessionStorage.getItem(REFRESH_KEY);
  if (!refreshToken) return false;
  if (refreshPromise) return refreshPromise;

  refreshPromise = (async () => {
    try {
      const response = await fetch(API_BASE_URL + "/api/v1/Auth/refresh-token", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ refreshToken })
      });
      if (!response.ok) {
        clearSession();
        return false;
      }
      const session = await response.json() as Session;
      if (!session.accessToken || !session.refreshToken) {
        clearSession();
        return false;
      }
      saveSession(session);
      return true;
    } catch {
      return false;
    } finally {
      refreshPromise = null;
    }
  })();

  return refreshPromise;
}

async function makeApiError(response: Response): Promise<ApiError> {
  let message = "The API request failed.";
  try {
    const body = await response.json();
    message = body.detail || body.title || body.message || message;
  } catch {
    // Some API responses do not include a JSON body.
  }
  return new ApiError(message, response.status);
}

export async function apiRequest<T>(
  path: string,
  init: RequestInit = {},
  allowRefresh = true
): Promise<T> {
  const headers = new Headers(init.headers || {});
  const token = sessionStorage.getItem(ACCESS_KEY);
  if (token) headers.set("Authorization", "Bearer " + token);
  if (init.body && !(init.body instanceof FormData) && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  let response = await fetch(API_BASE_URL + path, { ...init, headers });

  if (response.status === 401 && allowRefresh && !path.toLowerCase().includes("/auth/")) {
    const refreshed = await refreshSession();
    if (refreshed) {
      const retryHeaders = new Headers(init.headers || {});
      const nextToken = sessionStorage.getItem(ACCESS_KEY);
      if (nextToken) retryHeaders.set("Authorization", "Bearer " + nextToken);
      if (init.body && !(init.body instanceof FormData) && !retryHeaders.has("Content-Type")) {
        retryHeaders.set("Content-Type", "application/json");
      }
      response = await fetch(API_BASE_URL + path, { ...init, headers: retryHeaders });
    } else {
      clearSession();
      window.dispatchEvent(new CustomEvent("pm-session-expired"));
    }
  }

  if (!response.ok) throw await makeApiError(response);
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export async function getPaged<T>(path: string, pageNumber = 1, pageSize = 50): Promise<Paged<T>> {
  const separator = path.includes("?") ? "&" : "?";
  return apiRequest<Paged<T>>(path + separator + new URLSearchParams({
    pageNumber: String(pageNumber),
    pageSize: String(pageSize)
  }).toString());
}