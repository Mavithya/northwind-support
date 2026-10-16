import { Link, NavLink, Outlet } from 'react-router-dom';
import { useAuth } from './auth/AuthContext';


/** Application shell: the header, the navigation, and wherever the router puts the page. */
export function App() {
  const { session, logout } = useAuth();

  return (
    <div className="app">
      <header className="app__header">
        <Link className="app__brand" to="/tickets">
          Northwind Support
        </Link>

        <nav className="app__nav" aria-label="Main">
          <NavLink to="/tickets">Tickets</NavLink>
          <NavLink to="/customers">Customers</NavLink>
        </nav>

        <div className="app__nav">
  <span>{session?.user.fullName}</span>

  <button
    className="button button--ghost"
    type="button"
    onClick={logout}
  >
    Sign out
  </button>
</div>
      </header>

      <main className="app__main">
        <Outlet />
      </main>
    </div>
  );
}
