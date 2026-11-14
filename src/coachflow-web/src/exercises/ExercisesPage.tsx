import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { api, ApiError } from '../api'
import { useConnected } from '../auth'
import { levelLabel, levels, participantsLabel, type Exercise, type Focus } from './model'

export function ExercisesPage() {
  // Filter stehen in der Adresse, damit sie nach dem Zurückgehen erhalten bleiben.
  const [params, setParams] = useSearchParams()
  const connected = useConnected()
  const search = params.get('search') ?? ''
  const level = params.get('level') ?? ''
  const participants = params.get('participants') ?? ''
  const focusId = params.get('focusId') ?? ''

  const [focuses, setFocuses] = useState<Focus[]>([])
  const [exercises, setExercises] = useState<Exercise[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    api<Focus[]>('/api/focuses').then(setFocuses, () => setFocuses([]))
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    const query = new URLSearchParams()
    if (search.trim()) query.set('search', search.trim())
    if (level) query.set('level', level)
    if (participants) query.set('participants', participants)
    if (focusId) query.set('focusId', focusId)

    // Kurze Pause, damit nicht jeder Tastendruck in der Suche eine Anfrage auslöst.
    const timer = setTimeout(() => {
      api<Exercise[]>(`/api/exercises?${query}`, { signal: controller.signal })
        .then((result) => {
          setExercises(result)
          setError(null)
        })
        .catch((caught: unknown) => {
          if (controller.signal.aborted) return
          setError(caught instanceof ApiError ? caught.message : 'Die Übungen konnten nicht geladen werden.')
        })
    }, 250)

    return () => {
      clearTimeout(timer)
      controller.abort()
    }
  }, [search, level, participants, focusId])

  function setFilter(name: string, value: string) {
    setParams(
      (current) => {
        const next = new URLSearchParams(current)
        if (value) next.set(name, value)
        else next.delete(name)
        return next
      },
      { replace: true },
    )
  }

  const filtered = search || level || participants || focusId

  return (
    <>
      <div className="page-header">
        <h2>Übungen</h2>
        {connected && (
          <Link className="button" to="/uebungen/neu">
            Neue Übung
          </Link>
        )}
      </div>

      <form className="filters" role="search" onSubmit={(e) => e.preventDefault()}>
        <label>
          Suche
          <input type="search" value={search} placeholder="Name oder Beschreibung" onChange={(e) => setFilter('search', e.target.value)} />
        </label>
        <label>
          Niveau
          <select value={level} onChange={(e) => setFilter('level', e.target.value)}>
            <option value="">Alle</option>
            {levels.map((l) => (
              <option key={l.value} value={l.value}>
                {l.label}
              </option>
            ))}
          </select>
        </label>
        <label>
          Teilnehmende
          <input
            type="number"
            inputMode="numeric"
            min={1}
            max={100}
            value={participants}
            onChange={(e) => setFilter('participants', e.target.value)}
          />
        </label>
        <label>
          Schwerpunkt
          <select value={focusId} onChange={(e) => setFilter('focusId', e.target.value)}>
            <option value="">Alle</option>
            {focuses.map((f) => (
              <option key={f.id} value={f.id}>
                {f.name}
              </option>
            ))}
          </select>
        </label>
        {filtered && (
          <button type="button" className="button-link" onClick={() => setParams({}, { replace: true })}>
            Filter zurücksetzen
          </button>
        )}
      </form>

      {error && <p className="error" role="alert">{error}</p>}
      {!error && exercises === null && <p>Wird geladen …</p>}
      {exercises?.length === 0 && (
        <p>{filtered ? 'Keine Übung passt zu diesen Filtern.' : 'Noch keine Übungen. Lege die erste an.'}</p>
      )}
      <ul className="exercise-list">
        {exercises?.map((exercise) => (
          <li key={exercise.id} className="exercise-item">
            <Link to={`/uebungen/${exercise.id}`} className="exercise-name">
              {exercise.name}
            </Link>
            {exercise.origin === 'Ai' && <span className="badge">KI</span>}
            <p className="exercise-meta">
              {exercise.defaultDurationMinutes} Min. · {levelLabel(exercise.level)} ·{' '}
              {participantsLabel(exercise.minParticipants, exercise.maxParticipants)}
            </p>
            {exercise.description && <p className="exercise-description">{exercise.description}</p>}
            {exercise.focuses.length > 0 && (
              <ul className="chips">
                {exercise.focuses.map((f) => (
                  <li key={f.id} className="chip">
                    {f.name}
                  </li>
                ))}
              </ul>
            )}
          </li>
        ))}
      </ul>
    </>
  )
}
