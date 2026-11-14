import { useEffect, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router'
import { api, ApiError } from '../api'
import { useAuth, type Account } from '../auth'
import { readFragment } from '../fragment'
import { useAction } from '../useAction'
import { passwordMismatch } from '../passwords'
import { NewPasswordFields } from './NewPasswordFields'

type Invitation = { status: 'checking' } | { status: 'valid'; email: string } | { status: 'invalid'; message: string }

export function AcceptInvitationPage() {
  const navigate = useNavigate()
  const { signedIn } = useAuth()
  const [token] = useState(() => readFragment().get('token') ?? '')
  const [invitation, setInvitation] = useState<Invitation>(() =>
    token ? { status: 'checking' } : { status: 'invalid', message: 'Der Link ist unvollständig. Bitte öffne ihn direkt aus der E-Mail.' },
  )
  const { busy, error, setError, run } = useAction()
  const [name, setName] = useState('')
  const [password, setPassword] = useState('')
  const [confirmation, setConfirmation] = useState('')

  useEffect(() => {
    // Token aus der Adresszeile entfernen, damit er nicht im Verlauf bleibt.
    navigate('/einladung', { replace: true })
    if (!token) return
    let cancelled = false
    api<{ email: string }>('/api/account/invitation', { body: { token } })
      .then(({ email }) => !cancelled && setInvitation({ status: 'valid', email }))
      .catch((caught: unknown) => {
        if (cancelled) return
        const message = caught instanceof ApiError ? caught.message : 'Die Einladung konnte nicht geprüft werden.'
        setInvitation({ status: 'invalid', message })
      })
    return () => {
      cancelled = true
    }
  }, [navigate, token])

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    const mismatch = passwordMismatch(password, confirmation)
    if (mismatch) {
      setError(mismatch)
      return
    }
    void run(async () => {
      const account = await api<Account>('/api/account/register', { body: { token, name, password } })
      signedIn(account)
      navigate('/', { replace: true })
    })
  }

  return (
    <section className="card">
      <h2>Konto anlegen</h2>
      {invitation.status === 'checking' && <p>Einladung wird geprüft …</p>}
      {invitation.status === 'invalid' && <p className="error">{invitation.message}</p>}
      {invitation.status === 'valid' && (
        <form className="form" onSubmit={handleSubmit}>
          <p>Für {invitation.email}</p>
          <label>
            Name
            <input autoComplete="name" required maxLength={100} value={name} onChange={(e) => setName(e.target.value)} />
            <span className="hint">So sehen dich andere Trainerinnen und Trainer.</span>
          </label>
          <NewPasswordFields
            password={password}
            confirmation={confirmation}
            onPasswordChange={setPassword}
            onConfirmationChange={setConfirmation}
          />
          {error && <p className="error" role="alert">{error}</p>}
          <button type="submit" disabled={busy}>
            {busy ? 'Konto wird angelegt …' : 'Konto anlegen'}
          </button>
        </form>
      )}
    </section>
  )
}
