import { minPasswordLength } from '../passwords'

type Props = {
  password: string
  confirmation: string
  onPasswordChange: (value: string) => void
  onConfirmationChange: (value: string) => void
}

export function NewPasswordFields({ password, confirmation, onPasswordChange, onConfirmationChange }: Props) {
  return (
    <>
      <label>
        Neues Passwort
        <input
          type="password"
          autoComplete="new-password"
          required
          minLength={minPasswordLength}
          value={password}
          onChange={(e) => onPasswordChange(e.target.value)}
        />
        <span className="hint">Mindestens {minPasswordLength} Zeichen. Ein längerer Satz ist sicherer als Sonderzeichen.</span>
      </label>
      <label>
        Passwort wiederholen
        <input
          type="password"
          autoComplete="new-password"
          required
          value={confirmation}
          onChange={(e) => onConfirmationChange(e.target.value)}
        />
      </label>
    </>
  )
}
