import { useState } from 'react'
import { Navigate, NavLink, Outlet, useNavigate } from 'react-router'
import { accountOf, useAuth, useConnected } from './auth'
import { formatDateTime } from './exercises/model'

export function Layout() {
  const { state, signOut } = useAuth()
  const connected = useConnected()
  const navigate = useNavigate()
  const [signOutError, setSignOutError] = useState<string | null>(null)
  const account = accountOf(state)

  async function handleSignOut() {
    setSignOutError(null)
    try {
      await signOut()
      navigate('/anmelden', { replace: true })
    } catch {
      setSignOutError('Abmelden braucht eine Verbindung zum Server. Bitte versuche es erneut, sobald du online bist.')
    }
  }

  return (
    <div className="app">
      <header className="app-header">
        <h1>CoachFlow</h1>
        <div className="app-header-side">
          <span className={connected || !account ? 'status' : 'status status-offline'}>
            {connected || !account ? 'Online' : 'Offline'}
          </span>
          {account && (
            <button
              type="button"
              className="button-link"
              onClick={handleSignOut}
              disabled={!connected}
              title={connected ? undefined : 'Abmelden ist nur mit Verbindung möglich.'}
            >
              Abmelden
            </button>
          )}
        </div>
      </header>
      {signOutError && <p className="error" role="alert">{signOutError}</p>}
      {state.status === 'offline' && (
        <p className="offline-banner" role="status">
          Offline, Stand: {formatDateTime(state.savedAt)}. Du kannst gespeicherte Pläne ansehen, Bearbeiten ist erst wieder mit
          Verbindung möglich.
        </p>
      )}
      {account && (
        <nav className="app-nav">
          <NavLink to="/" end>
            Start
          </NavLink>
          <NavLink to="/plaene">Pläne</NavLink>
          <NavLink to="/uebungen">Übungen</NavLink>
        </nav>
      )}
      <main>
        <Outlet />
      </main>
    </div>
  )
}

// Schützt alle Seiten, die eine Anmeldung brauchen. Offline genügt die Kopie des Kontos.
export function RequireAccount() {
  const { state, retry } = useAuth()

  switch (state.status) {
    case 'loading':
      return <p>Wird geladen …</p>
    case 'unreachable':
      return (
        <section className="card">
          <p>
            Keine Verbindung zum Server. Ohne Verbindung ist keine Anmeldung möglich, und auf diesem Gerät sind noch keine
            Pläne für offline gespeichert.
          </p>
          <button type="button" onClick={retry}>
            Erneut versuchen
          </button>
        </section>
      )
    case 'anonymous':
      return <Navigate to="/anmelden" replace />
    case 'offline':
    case 'signedIn':
      return <Outlet />
  }
}

// Für Anmeldeseiten: Wer schon angemeldet ist, landet auf der Startseite.
export function RequireAnonymous() {
  const { state } = useAuth()
  return state.status === 'signedIn' || state.status === 'offline' ? <Navigate to="/" replace /> : <Outlet />
}
