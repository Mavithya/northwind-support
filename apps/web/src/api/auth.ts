import { request } from './client';
import type { AuthSession } from '../auth/authStorage';

export const authApi = {
  login: (email: string, password: string) =>
    request<AuthSession>('/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    }),
};