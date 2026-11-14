import { useState } from 'react'
import { ApiError } from './api'

// Gemeinsamer Ablauf für Formulare: Sperre während der Anfrage und verständliche Fehlermeldung.
export function useAction() {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function run(action: () => Promise<void>) {
    setBusy(true)
    setError(null)
    try {
      await action()
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Etwas ist schiefgelaufen. Bitte versuche es erneut.')
    } finally {
      setBusy(false)
    }
  }

  return { busy, error, setError, run }
}
