import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { api, ApiError } from '../api'
import { useConnected } from '../auth'
import { useAction } from '../useAction'
import { FocusPicker } from './FocusPicker'
import { formatDateTime, levels, type Exercise, type Focus, type Level } from './model'

type FormState = {
  name: string
  description: string
  duration: string
  min: string
  max: string
  level: Level | ''
  focusIds: string[]
}

const emptyForm: FormState = { name: '', description: '', duration: '15', min: '', max: '', level: '', focusIds: [] }

function fromExercise(exercise: Exercise): FormState {
  return {
    name: exercise.name,
    description: exercise.description,
    duration: String(exercise.defaultDurationMinutes),
    min: exercise.minParticipants?.toString() ?? '',
    max: exercise.maxParticipants?.toString() ?? '',
    level: exercise.level ?? '',
    focusIds: exercise.focuses.map((f) => f.id),
  }
}

function toNumber(value: string) {
  return value.trim() === '' ? null : Number(value)
}

export function ExerciseEditPage() {
  const { id } = useParams()
  const isNew = id === undefined
  const navigate = useNavigate()
  const { busy, error, setError, run } = useAction()
  const connected = useConnected()

  const [focuses, setFocuses] = useState<Focus[]>([])
  const [loaded, setLoaded] = useState<Exercise | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [form, setForm] = useState<FormState>(emptyForm)
  // Stand des Servers, wenn beim Speichern ein Konflikt auftrat. Die eigenen Eingaben bleiben im Formular.
  const [conflict, setConflict] = useState<Exercise | null>(null)

  useEffect(() => {
    api<Focus[]>('/api/focuses').then(setFocuses, () => setFocuses([]))
  }, [])

  useEffect(() => {
    if (isNew) return
    let cancelled = false
    api<Exercise>(`/api/exercises/${id}`)
      .then((exercise) => {
        if (cancelled) return
        setLoaded(exercise)
        setForm(fromExercise(exercise))
      })
      .catch((caught: unknown) => {
        if (!cancelled) setLoadError(caught instanceof ApiError ? caught.message : 'Die Übung konnte nicht geladen werden.')
      })
    return () => {
      cancelled = true
    }
  }, [id, isNew])

  function update<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm((current) => ({ ...current, [key]: value }))
  }

  function toggleFocus(focusId: string) {
    update('focusIds', form.focusIds.includes(focusId) ? form.focusIds.filter((f) => f !== focusId) : [...form.focusIds, focusId])
  }

  function addFocus(focus: Focus) {
    setFocuses((current) =>
      current.some((f) => f.id === focus.id)
        ? current
        : [...current, focus].sort((a, b) => a.name.localeCompare(b.name, 'de')),
    )
    setForm((current) => (current.focusIds.includes(focus.id) ? current : { ...current, focusIds: [...current.focusIds, focus.id] }))
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setConflict(null)
    const body = {
      name: form.name,
      description: form.description,
      defaultDurationMinutes: toNumber(form.duration),
      minParticipants: toNumber(form.min),
      maxParticipants: toNumber(form.max),
      level: form.level || null,
      focusIds: form.focusIds,
      version: loaded?.version,
    }
    void run(async () => {
      try {
        if (isNew) await api<Exercise>('/api/exercises', { body })
        else await api<Exercise>(`/api/exercises/${id}`, { method: 'PUT', body })
        navigate('/uebungen')
      } catch (caught) {
        if (caught instanceof ApiError && caught.status === 409) setConflict(caught.body.current as Exercise)
        throw caught
      }
    })
  }

  function loadCurrent() {
    if (!conflict) return
    setLoaded(conflict)
    setForm(fromExercise(conflict))
    setConflict(null)
    setError(null)
  }

  function handleDelete() {
    if (!loaded) return
    const confirmed = window.confirm(
      `Übung „${loaded.name}“ endgültig löschen?\n\nBestehende Pläne behalten ihre Kopie. Wiederherstellen ist nicht möglich.`,
    )
    if (!confirmed) return
    void run(async () => {
      try {
        await api(`/api/exercises/${id}`, { method: 'DELETE' })
      } catch (caught) {
        // Schon gelöscht ist für diesen Zweck genauso gut.
        if (!(caught instanceof ApiError && caught.status === 404)) throw caught
      }
      navigate('/uebungen')
    })
  }

  if (loadError) {
    return (
      <section className="card">
        <p className="error">{loadError}</p>
        <Link to="/uebungen">Zur Übersicht</Link>
      </section>
    )
  }

  if (!isNew && !loaded) return <p>Wird geladen …</p>

  return (
    <section className="card card-wide">
      <h2>{isNew ? 'Neue Übung' : 'Übung bearbeiten'}</h2>
      {loaded && (
        <p className="hint">
          {loaded.origin === 'Ai' ? 'Ursprünglich ein KI-Vorschlag. ' : ''}
          Zuletzt geändert {loaded.updatedBy ? `von ${loaded.updatedBy} ` : ''}am {formatDateTime(loaded.updatedAt)}.
        </p>
      )}
      <form className="form" onSubmit={handleSubmit}>
        <label>
          Name
          <input required maxLength={120} value={form.name} onChange={(e) => update('name', e.target.value)} />
        </label>
        <label>
          Beschreibung
          <textarea rows={6} maxLength={4000} value={form.description} onChange={(e) => update('description', e.target.value)} />
        </label>
        <div className="form-row">
          <label>
            Dauer (Minuten)
            <input
              type="number"
              inputMode="numeric"
              required
              min={1}
              max={180}
              value={form.duration}
              onChange={(e) => update('duration', e.target.value)}
            />
          </label>
          <label>
            Niveau
            <select value={form.level} onChange={(e) => update('level', e.target.value as Level | '')}>
              <option value="">Alle Niveaus</option>
              {levels.map((l) => (
                <option key={l.value} value={l.value}>
                  {l.label}
                </option>
              ))}
            </select>
          </label>
        </div>
        <div className="form-row">
          <label>
            Teilnehmende von
            <input type="number" inputMode="numeric" min={1} max={100} value={form.min} onChange={(e) => update('min', e.target.value)} />
          </label>
          <label>
            bis
            <input type="number" inputMode="numeric" min={1} max={100} value={form.max} onChange={(e) => update('max', e.target.value)} />
          </label>
        </div>
        <p className="hint">Leere Felder bedeuten: passt zu jedem Niveau und jeder Gruppengröße.</p>

        <FocusPicker focuses={focuses} selected={form.focusIds} onToggle={toggleFocus} onAdded={addFocus} />

        {error && <p className="error" role="alert">{error}</p>}
        {conflict && (
          <div className="conflict" role="alert">
            <p>Deine Eingaben sind noch da, aber nicht gespeichert. Mit dem aktuellen Stand gehen sie verloren.</p>
            <button type="button" onClick={loadCurrent}>
              Aktuellen Stand laden
            </button>
          </div>
        )}

        {!connected && (
          <p className="warning">Keine Verbindung. Deine Eingaben bleiben erhalten, Speichern geht erst wieder online.</p>
        )}
        <div className="form-actions">
          <button type="submit" disabled={busy || !connected}>
            {busy ? 'Wird gespeichert …' : 'Speichern'}
          </button>
          <Link to="/uebungen">Abbrechen</Link>
          {!isNew && (
            <button type="button" className="button-danger" onClick={handleDelete} disabled={busy || !connected}>
              Löschen
            </button>
          )}
        </div>
      </form>
    </section>
  )
}
