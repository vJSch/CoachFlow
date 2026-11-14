import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router'
import { ApiError } from '../api'
import { accountOf, useAuth, useConnected } from '../auth'
import { formatDateTime, levelLabel } from '../exercises/model'
import { OfflineNotice } from '../offline/OfflineNotice'
import { loadPlan } from '../offline/planCopy'
import { spreadLabel, type Plan } from './model'

// Ansicht für die Halle: alles lesbar auf einen Blick, ohne Formularfelder.
export function PlanViewPage() {
  const { id } = useParams()
  const account = accountOf(useAuth().state)
  const connected = useConnected()
  const [plan, setPlan] = useState<Plan | null>(null)
  const [offlineSince, setOfflineSince] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const email = account?.email
  const name = account?.name
  useEffect(() => {
    if (!email || name === undefined || !id) return
    let cancelled = false
    loadPlan({ email, name }, id)
      .then((result) => {
        if (cancelled) return
        setPlan(result.plan)
        setOfflineSince(result.offlineSince)
      })
      .catch((caught: unknown) => {
        if (!cancelled) setError(caught instanceof ApiError ? caught.message : 'Der Plan konnte nicht geladen werden.')
      })
    return () => {
      cancelled = true
    }
  }, [email, name, id])

  if (error) {
    return (
      <section className="card">
        <p className="error">{error}</p>
        <Link to="/plaene">Zur Übersicht</Link>
      </section>
    )
  }

  if (!plan) return <p>Wird geladen …</p>

  // Start- und Endminute jeder Übung, fortlaufend über den Ablauf.
  const windows = plan.positions.reduce<{ from: number; to: number }[]>((all, position) => {
    const from = all.at(-1)?.to ?? 0
    return [...all, { from, to: from + position.plannedDurationMinutes }]
  }, [])

  return (
    <article className="plan-view">
      <div className="page-header">
        <h2>{plan.title}</h2>
        {connected && !offlineSince && (
          <Link className="button" to={`/plaene/${plan.id}/bearbeiten`}>
            Bearbeiten
          </Link>
        )}
      </div>
      <OfflineNotice savedAt={offlineSince} />
      <p className="exercise-meta">
        {levelLabel(plan.level)} · Streuung {spreadLabel(plan.spread)} · {plan.participantCount} Personen ·{' '}
        {plan.durationMinutes} Min.
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
      {plan.note && <p className="plan-note">{plan.note}</p>}
      {plan.warnings.map((warning) => (
        <p key={warning} className="warning">
          {warning}
        </p>
      ))}

      {plan.positions.length === 0 ? (
        <p>Dieser Plan enthält noch keine Übungen.</p>
      ) : (
        <ol className="position-list">
          {plan.positions.map((position, index) => {
            const { from, to } = windows[index]
            return (
              <li key={position.id} className="position-item">
                <div className="position-head">
                  <strong>{position.name}</strong>
                  <span className="position-time">
                    {from}–{to} Min. ({position.plannedDurationMinutes} Min.)
                  </span>
                </div>
                {position.description && <p className="position-description">{position.description}</p>}
                {position.note && <p className="position-note">Notiz: {position.note}</p>}
              </li>
            )
          })}
        </ol>
      )}

      <p className="hint">
        Zuletzt geändert {plan.updatedBy ? `von ${plan.updatedBy} ` : ''}am {formatDateTime(plan.updatedAt)}.
      </p>
      <Link to="/plaene">Zur Übersicht</Link>
    </article>
  )
}
