import { useState, type FormEvent } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { toErrorMessage } from '../../api/client';
import { useAuth } from '../../auth/AuthContext';

export function LoginPage() {
  const { login, isAuthenticated } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string>();
  const [isSubmitting, setIsSubmitting] = useState(false);

  if (isAuthenticated) {
    return <Navigate to="/tickets" replace />;
  }

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(undefined);
    setIsSubmitting(true);

    try {
      await login(email.trim(), password);

      const state = location.state as
        | { from?: { pathname?: string } }
        | null;

      const destination = state?.from?.pathname;

      // Do not navigate to the login route itself.
      navigate(
        destination && destination !== '/login'
          ? destination
          : '/tickets',
        { replace: true },
      );
    } catch (caught) {
      setError(toErrorMessage(caught, 'Unable to sign in.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <main className="app__main">
      <section
        className="card"
        style={{ maxWidth: '420px', margin: '3rem auto' }}
      >
        <h1>Northwind Support</h1>
        <p>Sign in to manage support tickets.</p>

        <form onSubmit={handleSubmit}>
          <div className="field">
            <label className="field__label" htmlFor="login-email">
              Email
            </label>
            <input
              id="login-email"
              type="email"
              autoComplete="username"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              required
              disabled={isSubmitting}
            />
          </div>

          <div className="field">
            <label className="field__label" htmlFor="login-password">
              Password
            </label>
            <input
              id="login-password"
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              required
              disabled={isSubmitting}
            />
          </div>

          {error && (
            <p role="alert" className="field__error">
              {error}
            </p>
          )}

          <button
            className="button button--primary"
            type="submit"
            disabled={isSubmitting}
          >
            {isSubmitting ? 'Signing in...' : 'Sign in'}
          </button>
        </form>
      </section>
    </main>
  );
}