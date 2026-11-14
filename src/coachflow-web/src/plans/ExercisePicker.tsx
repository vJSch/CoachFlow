import { useEffect, useState } from 'react'
import { api, ApiError } from '../api'
import { levelLabel, participantsLabel, type Exercise, type Focus, type Level } from '../exercises/model'

type Props = {
  level: Level | ''
  participants: string
  focuses: Focus[]
  onAdd: (exercise: Exercise) => void
}

export function ExercisePicker({ level, participants, focuses, onAdd }: Props) {
  const [search, setSearch] = useState('')
  const [matching, setMatching] = useState(true)
  const [focusId, setFocusId] = useState('')
  const [exercises, setExercises] = useState<Exercise[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [lastAdded, setLastAdded] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    const query = new URLSearchParams()
    if (search.trim()) query.set('search', search.trim())
    // „Passend“ nutzt die Parameter des Plans, damit die Liste kurz bleibt.
    if (matching && level) query.set('level', level)
    if (matching && participants) query.set('participants', participants)
    if (focusId) query.set('focusId', focusId)

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
  }, [search, matching, level, participants, focusId])

  return (
    <div className="picker">
      <div className="filters">
        <label>
          Suche
          <input type="search" value={search} placeholder="Name oder Beschreibung" onChange={(e) => setSearch(e.target.value)} />
        </label>
        <label>
          Schwerpunkt
          <select value={focusId} onChange={(e) => setFocusId(e.target.value)}>
            <option value="">Alle</option>
            {focuses.map((f) => (
              <option key={f.id} value={f.id}>
                {f.name}
              </option>
            ))}
          </select>
        </label>
        <label className="checkbox">
          <input type="checkbox" checked={matching} onChange={(e) => setMatching(e.target.checked)} />
          Nur passend zu Niveau und Teilnehmerzahl
        </label>
      </div>

      {error && <p className="error" role="alert">{error}</p>}
      {lastAdded && (
        <p className="notice" role="status">
          „{lastAdded}“ hinzugefügt.
        </p>
      )}
      {exercises?.length === 0 && <p>Keine passende Übung gefunden.</p>}
      <ul className="picker-list">
        {exercises?.map((exercise) => (
          <li key={exercise.id} className="picker-item">
            <div>
              <strong>{exercise.name}</strong>
              <p className="exercise-meta">
                {exercise.defaultDurationMinutes} Min. · {levelLabel(exercise.level)} ·{' '}
                {participantsLabel(exercise.minParticipants, exercise.maxParticipants)}
              </p>
            </div>
            <button
              type="button"
              onClick={() => {
                onAdd(exercise)
                setLastAdded(exercise.name)
              }}
            >
              Hinzufügen
            </button>
          </li>
        ))}
      </ul>
    </div>
  )
}
