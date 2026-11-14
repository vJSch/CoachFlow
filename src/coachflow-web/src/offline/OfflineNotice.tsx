import { useAuth } from '../auth'
import { formatDateTime } from '../exercises/model'

// Für Seiten, die während der Nutzung auf die Kopie zurückfallen. Startet die App offline,
// zeigt das Layout den Hinweis bereits und hier erscheint nichts doppelt.
export function OfflineNotice({ savedAt }: { savedAt: string | null }) {
  const { state } = useAuth()
  if (!savedAt || state.status === 'offline') return null
  return (
    <p className="offline-banner" role="status">
      Offline, Stand: {formatDateTime(savedAt)}. Bearbeiten ist erst wieder mit Verbindung möglich.
    </p>
  )
}
