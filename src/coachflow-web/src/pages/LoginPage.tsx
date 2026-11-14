import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { api } from '../api'
import { useAuth, type Account } from '../auth'
import { useAction } from '../useAction'

export function LoginPage() {
  const { signedIn } = useAuth()
  const navigate = useNavigate()
  const { busy, error, run } = useAction()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    void run(async () => {
      const account = await api<Account>('/api/account/login', { body: { email, password } })
      signedIn(account)
      navigate('/', { replace: true })
    })
  }

  return (
    <section className="card">
      <h2>Anmelden</h2>
      <form className="form" onSubmit={handleSubmit}>
        <label>
          E-Mail-Adresse
          <input type="email" autoComplete="username" required value={email} onChange={(e) => setEmail(e.target.value)} />
        </label>
        <label>
          Passwort
          <input
            type="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
        </label>
        {error && <p className="error" role="alert">{error}</p>}
        <button type="submit" disabled={busy}>
          {busy ? 'Wird angemeldet …' : 'Anmelden'}
        </button>
      </form>
      <p>
        <Link to="/passwort-vergessen">Passwort vergessen?</Link>
      </p>
      <p className="hint">Ein Konto bekommst du nur über eine Einladung.</p>
    </section>
  )
}
