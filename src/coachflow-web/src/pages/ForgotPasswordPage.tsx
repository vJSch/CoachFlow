import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { api } from '../api'
import { useAction } from '../useAction'

export function ForgotPasswordPage() {
  const { busy, error, run } = useAction()
  const [email, setEmail] = useState('')
  const [sent, setSent] = useState(false)

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    void run(async () => {
      await api('/api/account/forgot-password', { body: { email } })
      setSent(true)
    })
  }

  return (
    <section className="card">
      <h2>Passwort vergessen</h2>
      {sent ? (
        <p className="notice" role="status">
          Falls es ein Konto mit dieser Adresse gibt, haben wir eine E-Mail mit einem Link geschickt. Der Link gilt
          2 Stunden.
        </p>
      ) : (
        <form className="form" onSubmit={handleSubmit}>
          <label>
            E-Mail-Adresse
            <input type="email" autoComplete="username" required value={email} onChange={(e) => setEmail(e.target.value)} />
          </label>
          {error && <p className="error" role="alert">{error}</p>}
          <button type="submit" disabled={busy}>
            {busy ? 'Wird gesendet …' : 'Link anfordern'}
          </button>
        </form>
      )}
      <p>
        <Link to="/anmelden">Zurück zur Anmeldung</Link>
      </p>
    </section>
  )
}
