import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from 'react';
import { authApi } from '../api/auth';
import {
  AUTH_EXPIRED_EVENT,
  clearAuthSession,
  readAuthSession,
  saveAuthSession,
  type AuthSession,
} from './authStorage';

interface AuthContextValue {
  session: AuthSession | null;
  isAuthenticated: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<AuthSession | null>(
    () => readAuthSession(),
  );

  const logout = useCallback(() => {
    clearAuthSession();
    setSession(null);
  }, []);

  const login = useCallback(async (email: string, password: string) => {
    const result = await authApi.login(email, password);

    saveAuthSession(result);
    setSession(result);
  }, []);

  useEffect(() => {
    const handleExpiredSession = () => {
      clearAuthSession();
      setSession(null);
    };

    window.addEventListener(AUTH_EXPIRED_EVENT, handleExpiredSession);

    return () => {
      window.removeEventListener(AUTH_EXPIRED_EVENT, handleExpiredSession);
    };
  }, []);

const isAuthenticated = session !== null;

useEffect(() => {
  if (!session) return;

  const expiresAt = Date.parse(session.expiresAtUtc);
  const delay = Math.max(expiresAt - Date.now(), 0);

  const timeoutId = window.setTimeout(logout, delay);

  return () => {
    window.clearTimeout(timeoutId);
  };
}, [session, logout]);

  return (
    <AuthContext.Provider
      value={{ session, isAuthenticated, login, logout }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);

  if (!context) {
    throw new Error('useAuth must be used inside AuthProvider.');
  }

  return context;
}