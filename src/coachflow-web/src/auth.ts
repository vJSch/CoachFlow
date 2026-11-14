import { createContext, useContext } from 'react'
import { useOnlineStatus } from './useOnlineStatus'

export type Account = { email: string; name: string }

export type AuthState =
  | { status: 'loading' }
  | { status: 'anonymous' }
  // Server nicht erreichbar und keine Offline-Kopie vorhanden.
  | { status: 'unreachable' }
  // Server nicht erreichbar, aber eine Offline-Kopie dieses Kontos liegt vor: nur Lesen.
  | { status: 'offline'; account: Account; savedAt: string }
  | { status: 'signedIn'; account: Account }

export type AuthContextValue = {
  state: AuthState
  signedIn: (account: Account) => void
  signOut: () => Promise<void>
  retry: () => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth braucht einen AuthProvider.')
  return context
}

export function accountOf(state: AuthState): Account | null {
  return state.status === 'signedIn' || state.status === 'offline' ? state.account : null
}

// Bearbeiten, Einladen und KI brauchen eine Verbindung und eine gültige Anmeldung.
export function useConnected() {
  const online = useOnlineStatus()
  const { state } = useAuth()
  return online && state.status === 'signedIn'
}
