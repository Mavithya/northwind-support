export const AUTH_SESSION_KEY = 'northwind-support-session';
export const AUTH_EXPIRED_EVENT = 'northwind:auth-expired';

export interface AuthUser {
  id: number;
  email: string;
  fullName: string;
  role: string;
}

export interface AuthSession {
  accessToken: string;
  tokenType: string;
  expiresAtUtc: string;
  user: AuthUser;
}

export function readAuthSession(): AuthSession | null {
  try {
    const raw = sessionStorage.getItem(AUTH_SESSION_KEY);

    if (!raw) return null;

    const session = JSON.parse(raw) as AuthSession;

    if (
      !session.accessToken ||
      !session.expiresAtUtc ||
      !session.user ||
      !Number.isFinite(Date.parse(session.expiresAtUtc)) ||
      Date.parse(session.expiresAtUtc) <= Date.now()
    ) {
      clearAuthSession();
      return null;
    }

    return session;
  } catch {
    clearAuthSession();
    return null;
  }
}

export function saveAuthSession(session: AuthSession): void {
  sessionStorage.setItem(AUTH_SESSION_KEY, JSON.stringify(session));
}

export function clearAuthSession(): void {
  sessionStorage.removeItem(AUTH_SESSION_KEY);
}

export function getAccessToken(): string | null {
  return readAuthSession()?.accessToken ?? null;
}