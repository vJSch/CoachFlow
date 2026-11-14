import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { api } from '../api'
import { readFragment } from '../fragment'
import { useAction } from '../useAction'
import { passwordMismatch } from '../passwords'
import { NewPasswordFields } from './NewPasswordFields'

export function ResetPasswordPage() {
  const navigate = useNavigate()
  const [link] = useState(() => {
    const fragment = readFragment()
    return { email: fragment.get('email') ?? '', token: fragment.get('token') ?? '' }
  })
  const { busy, error, setError, run } = useAction()
  const [password, setPassword] = useState('')
  const [confirmation, setConfirmation] = useState('')
  const [done, setDone] = useState(false)

  // Token aus der Adresszeile entfernen, damit er nicht im Verlauf bleibt.
  useEffect(() => {
    navigate('/passwort-neu', { replace: true })
  }, [navigate])

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    const mismatch = passwordMismatch(password, confirmation)
    if (mismatch) {
      setError(mismatch)
      return
    }
    void run(async () => {
      await api('/api/account/reset-password', { body: { ...link, password } })
      setDone(true)
    })
  }

  return (
    <section className="card">
      <h2>Neues Passwort festlegen</h2>
      {!link.token ? (
        <p className="error">Der Link ist unvollständig. Bitte öffne ihn direkt aus der E-Mail.</p>
      ) : done ? (
        <p className="notice" role="status">
          Dein Passwort ist geändert. Du kannst dich jetzt damit anmelden.
        </p>
      ) : (
        <form className="form" onSubmit={handleSubmit}>
          <p>Für {link.email}</p>
          <NewPasswordFields
            password={password}
            confirmation={confirmation}
            onPasswordChange={setPassword}
            onConfirmationChange={setConfirmation}
          />
          {error && <p className="error" role="alert">{error}</p>}
          <button type="submit" disabled={busy}>
            {busy ? 'Wird gespeichert …' : 'Passwort speichern'}
          </button>
        </form>
      )}
      <p>
        <Link to="/anmelden">Zur Anmeldung</Link>
      </p>
    </section>
  )
}
