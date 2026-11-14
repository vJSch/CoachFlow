import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { api, ApiError } from './api'
import { AuthContext, type Account, type AuthState } from './auth'
import { clearCopy, loadCopy, loadTimeoutMs, refreshCopy } from './offline/planCopy'

async function checkSession(): Promise<AuthState> {
  try {
    const account = await api<Account>('/api/account/me', { signal: AbortSignal.timeout(loadTimeoutMs) })
    return { status: 'signedIn', account }
  } catch (error) {
    // Nur eine ausdrückliche 401 beendet die Anmeldung. Alles andere behandelt die App wie fehlende Verbindung.
    if (error instanceof ApiError && error.status === 401) {
      // Nicht oder nicht mehr angemeldet: Die Kopie darf nicht auf dem Gerät bleiben.
      await clearCopy()
      return { status: 'anonymous' }
    }
    const copy = await loadCopy()
    return copy
      ? { status: 'offline', account: { email: copy.email, name: copy.name }, savedAt: copy.savedAt }
      : { status: 'unreachable' }
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>({ status: 'loading' })
  const [attempt, setAttempt] = useState(0)
  const statusRef = useRef(state.status)

  useEffect(() => {
    statusRef.current = state.status
  }, [state.status])

  useEffect(() => {
    let cancelled = false
    void checkSession().then((next) => !cancelled && setState(next))
    return () => {
      cancelled = true
    }
  }, [attempt])

  // Nach jeder Anmeldung und beim Start mit Verbindung die Lesekopie auffrischen.
  const signedInEmail = state.status === 'signedIn' ? state.account.email : null
  const signedInName = state.status === 'signedIn' ? state.account.name : null
  useEffect(() => {
    if (signedInEmail && signedInName !== null) void refreshCopy({ email: signedInEmail, name: signedInName })
  }, [signedInEmail, signedInName])

  // Kehrt die Verbindung zurück, wird die Anmeldung still erneut geprüft. Ohne Ladeanzeige,
  // damit die offline geöffnete Seite nicht kurz verschwindet.
  useEffect(() => {
    function handleOnline() {
      if (statusRef.current === 'offline' || statusRef.current === 'unreachable') setAttempt((value) => value + 1)
    }
    window.addEventListener('online', handleOnline)
    return () => window.removeEventListener('online', handleOnline)
  }, [])

  const signedIn = useCallback((account: Account) => setState({ status: 'signedIn', account }), [])

  // Abmelden braucht den Server, weil nur er das Anmelde-Cookie löschen kann.
  const signOut = useCallback(async () => {
    await api('/api/account/logout', { method: 'POST' })
    await clearCopy()
    setState({ status: 'anonymous' })
  }, [])

  const retry = useCallback(() => {
    setState({ status: 'loading' })
    setAttempt((value) => value + 1)
  }, [])

  const value = useMemo(() => ({ state, signedIn, signOut, retry }), [state, signedIn, signOut, retry])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
