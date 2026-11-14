import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { ApiError } from '../api'
import { accountOf, useAuth, useConnected } from '../auth'
import { formatDateTime, levelLabel } from '../exercises/model'
import { OfflineNotice } from '../offline/OfflineNotice'
import { loadPlans } from '../offline/planCopy'
import type { Plan } from './model'

export function PlansPage() {
  const account = accountOf(useAuth().state)
  const connected = useConnected()
  const [plans, setPlans] = useState<Plan[] | null>(null)
  const [offlineSince, setOfflineSince] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const email = account?.email
  const name = account?.name
  useEffect(() => {
    if (!email || name === undefined) return
    let cancelled = false
    // Mit Verbindung frisch vom Server, das erneuert zugleich die Offline-Kopie. Sonst aus der Kopie.
    loadPlans({ email, name })
      .then((result) => {
        if (cancelled) return
        setPlans(result.plans)
        setOfflineSince(result.offlineSince)
      })
      .catch((caught: unknown) => {
        if (!cancelled) setError(caught instanceof ApiError ? caught.message : 'Die Pläne konnten nicht geladen werden.')
      })
    return () => {
      cancelled = true
    }
  }, [email, name])

  return (
    <>
      <div className="page-header">
        <h2>Trainingspläne</h2>
        {connected && !offlineSince && (
          <Link className="button" to="/plaene/neu">
            Neuer Plan
          </Link>
        )}
      </div>
      <OfflineNotice savedAt={offlineSince} />

      {error && <p className="error" role="alert">{error}</p>}
      {!error && plans === null && <p>Wird geladen …</p>}
      {plans?.length === 0 && <p>Noch keine Pläne. Lege den ersten an.</p>}
      <ul className="exercise-list">
        {plans?.map((plan) => (
          <li key={plan.id} className="exercise-item">
            <Link to={`/plaene/${plan.id}`} className="exercise-name">
              {plan.title}
            </Link>
            <p className="exercise-meta">
              {levelLabel(plan.level)} · {plan.participantCount} Personen · {plan.durationMinutes} Min. ·{' '}
              {plan.positions.length} {plan.positions.length === 1 ? 'Übung' : 'Übungen'}
              {plan.warnings.length > 0 && <span className="warning-inline"> · Dauer weicht ab</span>}
            </p>
            {plan.focuses.length > 0 && (
              <ul className="chips">
                {plan.focuses.map((f) => (
                  <li key={f.id} className="chip">
                    {f.name}
                  </li>
                ))}
              </ul>
            )}
            <p className="hint">
              Geändert {plan.updatedBy ? `von ${plan.updatedBy} ` : ''}am {formatDateTime(plan.updatedAt)}
            </p>
          </li>
        ))}
      </ul>
    </>
  )
}
