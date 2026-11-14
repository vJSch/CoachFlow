import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { api } from '../api'
import { accountOf, useAuth, useConnected } from '../auth'
import { useAction } from '../useAction'

export function HomePage() {
  const name = accountOf(useAuth().state)?.name ?? ''
  const connected = useConnected()

  return (
    <>
      <section className="card">
        <h2>Hallo {name}</h2>
        <p>
          Zu den <Link to="/plaene">Trainingsplänen</Link> oder den <Link to="/uebungen">Übungen</Link>.
        </p>
      </section>
      {connected ? (
        <InviteForm />
      ) : (
        <p className="hint">Einladungen, Übungen und das Bearbeiten von Plänen brauchen eine Verbindung.</p>
      )}
    </>
  )
}

function InviteForm() {
  const { busy, error, run } = useAction()
  const [email, setEmail] = useState('')
  const [invited, setInvited] = useState<string | null>(null)

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    void run(async () => {
      await api('/api/invitations', { body: { email } })
      setInvited(email)
      setEmail('')
    })
  }

  return (
    <section className="card">
      <h2>Jemanden einladen</h2>
      <form className="form" onSubmit={handleSubmit}>
        <label>
          E-Mail-Adresse
          <input type="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
          <span className="hint">Die Einladung gilt 3 Tage. Alle Konten sehen dieselben Übungen und Pläne.</span>
        </label>
        {error && <p className="error" role="alert">{error}</p>}
        {invited && !error && (
          <p className="notice" role="status">
            Einladung an {invited} verschickt.
          </p>
        )}
        <button type="submit" disabled={busy}>
          {busy ? 'Wird gesendet …' : 'Einladung senden'}
        </button>
      </form>
    </section>
  )
}
