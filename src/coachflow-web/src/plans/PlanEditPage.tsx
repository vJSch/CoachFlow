import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { api, ApiError } from '../api'
import { accountOf, useAuth, useConnected } from '../auth'
import { FocusPicker } from '../exercises/FocusPicker'
import { formatDateTime, levels, type Exercise, type Focus, type Level } from '../exercises/model'
import { refreshCopy } from '../offline/planCopy'
import { SuggestionPanel } from '../suggestions/SuggestionPanel'
import { useAction } from '../useAction'
import { ExercisePicker } from './ExercisePicker'
import { durationWarning, maxPlanFocuses, spreads, type Plan, type Spread } from './model'

type DraftPosition = {
  // Nur für React. Gespeicherte Positionen nutzen ihre Id, neue einen Zähler.
  key: string
  id: string | null
  exerciseId: string | null
  name: string
  description: string
  duration: string
  note: string
}

type FormState = {
  title: string
  participantCount: string
  level: Level | ''
  spread: Spread | ''
  duration: string
  note: string
  focusIds: string[]
  positions: DraftPosition[]
}

const emptyForm: FormState = {
  title: '',
  participantCount: '',
  level: '',
  spread: '',
  duration: '90',
  note: '',
  focusIds: [],
  positions: [],
}

let nextKey = 0

function fromPlan(plan: Plan): FormState {
  return {
    title: plan.title,
    participantCount: String(plan.participantCount),
    level: plan.level,
    spread: plan.spread,
    duration: String(plan.durationMinutes),
    note: plan.note,
    focusIds: plan.focuses.map((f) => f.id),
    positions: plan.positions.map((p) => ({
      key: p.id,
      id: p.id,
      exerciseId: p.exerciseId,
      name: p.name,
      description: p.description,
      duration: String(p.plannedDurationMinutes),
      note: p.note,
    })),
  }
}

function toNumber(value: string) {
  return value.trim() === '' ? null : Number(value)
}

export function PlanEditPage() {
  const { id } = useParams()
  const isNew = id === undefined
  const navigate = useNavigate()
  const { busy, error, setError, run } = useAction()
  const account = accountOf(useAuth().state)
  const connected = useConnected()

  const [focuses, setFocuses] = useState<Focus[]>([])
  const [loaded, setLoaded] = useState<Plan | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [form, setForm] = useState<FormState>(emptyForm)
  const [pickerOpen, setPickerOpen] = useState(false)
  // Stand des Servers bei einem Konflikt. Die eigenen Eingaben bleiben im Formular.
  const [conflict, setConflict] = useState<Plan | null>(null)

  useEffect(() => {
    api<Focus[]>('/api/focuses').then(setFocuses, () => setFocuses([]))
  }, [])

  useEffect(() => {
    if (isNew) return
    let cancelled = false
    api<Plan>(`/api/plans/${id}`)
      .then((plan) => {
        if (cancelled) return
        setLoaded(plan)
        setForm(fromPlan(plan))
      })
      .catch((caught: unknown) => {
        if (!cancelled) setLoadError(caught instanceof ApiError ? caught.message : 'Der Plan konnte nicht geladen werden.')
      })
    return () => {
      cancelled = true
    }
  }, [id, isNew])

  function update<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm((current) => ({ ...current, [key]: value }))
  }

  function updatePosition(key: string, changes: Partial<DraftPosition>) {
    setForm((current) => ({
      ...current,
      positions: current.positions.map((p) => (p.key === key ? { ...p, ...changes } : p)),
    }))
  }

  function movePosition(index: number, offset: number) {
    setForm((current) => {
      const target = index + offset
      if (target < 0 || target >= current.positions.length) return current
      const positions = [...current.positions]
      ;[positions[index], positions[target]] = [positions[target], positions[index]]
      return { ...current, positions }
    })
  }

  function removePosition(key: string) {
    setForm((current) => ({ ...current, positions: current.positions.filter((p) => p.key !== key) }))
  }

  function addExercise(exercise: Exercise) {
    // Vorschau aus der Übung. Die verbindliche Kopie erstellt der Server beim Speichern.
    const position: DraftPosition = {
      key: `neu-${++nextKey}`,
      id: null,
      exerciseId: exercise.id,
      name: exercise.name,
      description: exercise.description,
      duration: String(exercise.defaultDurationMinutes),
      note: '',
    }
    setForm((current) => ({ ...current, positions: [...current.positions, position] }))
  }

  function toggleFocus(focusId: string) {
    setForm((current) => {
      if (current.focusIds.includes(focusId)) return { ...current, focusIds: current.focusIds.filter((f) => f !== focusId) }
      if (current.focusIds.length >= maxPlanFocuses) return current
      return { ...current, focusIds: [...current.focusIds, focusId] }
    })
  }

  function addFocus(focus: Focus) {
    setFocuses((current) =>
      current.some((f) => f.id === focus.id) ? current : [...current, focus].sort((a, b) => a.name.localeCompare(b.name, 'de')),
    )
    setForm((current) =>
      current.focusIds.includes(focus.id) || current.focusIds.length >= maxPlanFocuses
        ? current
        : { ...current, focusIds: [...current.focusIds, focus.id] },
    )
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setConflict(null)
    const body = {
      title: form.title,
      participantCount: toNumber(form.participantCount),
      level: form.level || null,
      spread: form.spread || null,
      durationMinutes: toNumber(form.duration),
      note: form.note,
      focusIds: form.focusIds,
      positions: form.positions.map((p) =>
        p.id
          ? { id: p.id, plannedDurationMinutes: toNumber(p.duration), note: p.note }
          : { exerciseId: p.exerciseId, plannedDurationMinutes: toNumber(p.duration), note: p.note },
      ),
      version: loaded?.version,
    }
    void run(async () => {
      try {
        const saved = isNew
          ? await api<Plan>('/api/plans', { body })
          : await api<Plan>(`/api/plans/${id}`, { method: 'PUT', body })
        if (account) void refreshCopy(account)
        navigate(`/plaene/${saved.id}`)
      } catch (caught) {
        if (caught instanceof ApiError && caught.status === 409) setConflict(caught.body.current as Plan)
        throw caught
      }
    })
  }

  function loadCurrent() {
    if (!conflict) return
    setLoaded(conflict)
    setForm(fromPlan(conflict))
    setConflict(null)
    setError(null)
  }

  function handleDelete() {
    if (!loaded) return
    if (!window.confirm(`Plan „${loaded.title}“ endgültig löschen?\n\nWiederherstellen ist nicht möglich.`)) return
    void run(async () => {
      try {
        await api(`/api/plans/${id}`, { method: 'DELETE' })
      } catch (caught) {
        if (!(caught instanceof ApiError && caught.status === 404)) throw caught
      }
      if (account) void refreshCopy(account)
      navigate('/plaene')
    })
  }

  if (loadError) {
    return (
      <section className="card">
        <p className="error">{loadError}</p>
        <Link to="/plaene">Zur Übersicht</Link>
      </section>
    )
  }

  if (!isNew && !loaded) return <p>Wird geladen …</p>

  const warning = durationWarning(
    Number(form.duration),
    form.positions.map((p) => Number(p.duration)),
  )
  const total = form.positions.reduce((sum, p) => sum + (Number(p.duration) || 0), 0)

  return (
    <section className="card card-wide">
      <h2>{isNew ? 'Neuer Plan' : 'Plan bearbeiten'}</h2>
      {loaded && (
        <p className="hint">
          Zuletzt geändert {loaded.updatedBy ? `von ${loaded.updatedBy} ` : ''}am {formatDateTime(loaded.updatedAt)}.
        </p>
      )}
      <form className="form" onSubmit={handleSubmit}>
        <label>
          Titel
          <input required maxLength={120} value={form.title} onChange={(e) => update('title', e.target.value)} />
        </label>
        <div className="form-row">
          <label>
            Teilnehmende
            <input
              type="number"
              inputMode="numeric"
              required
              min={1}
              max={100}
              value={form.participantCount}
              onChange={(e) => update('participantCount', e.target.value)}
            />
          </label>
          <label>
            Dauer (Minuten)
            <input
              type="number"
              inputMode="numeric"
              required
              min={1}
              max={300}
              value={form.duration}
              onChange={(e) => update('duration', e.target.value)}
            />
          </label>
        </div>
        <div className="form-row">
          <label>
            Niveau
            <select required value={form.level} onChange={(e) => update('level', e.target.value as Level | '')}>
              <option value="">Bitte wählen</option>
              {levels.map((l) => (
                <option key={l.value} value={l.value}>
                  {l.label}
                </option>
              ))}
            </select>
          </label>
          <label>
            Streuung
            <select required value={form.spread} onChange={(e) => update('spread', e.target.value as Spread | '')}>
              <option value="">Bitte wählen</option>
              {spreads.map((s) => (
                <option key={s.value} value={s.value}>
                  {s.label}
                </option>
              ))}
            </select>
            <span className="hint">Wie unterschiedlich stark die Gruppe ist.</span>
          </label>
        </div>

        <FocusPicker
          focuses={focuses}
          selected={form.focusIds}
          onToggle={toggleFocus}
          onAdded={addFocus}
          max={maxPlanFocuses}
        />

        <label>
          Notiz
          <textarea rows={3} maxLength={2000} value={form.note} onChange={(e) => update('note', e.target.value)} />
        </label>

        <fieldset className="positions">
          <legend>Ablauf</legend>
          {form.positions.length === 0 && <p>Noch keine Übungen im Plan.</p>}
          <ol className="position-list">
            {form.positions.map((position, index) => (
              <li key={position.key} className="position-item">
                <div className="position-head">
                  <strong>
                    {index + 1}. {position.name}
                  </strong>
                  <div className="position-actions">
                    <button
                      type="button"
                      className="button-small"
                      aria-label={`${position.name} nach oben`}
                      disabled={index === 0}
                      onClick={() => movePosition(index, -1)}
                    >
                      ↑
                    </button>
                    <button
                      type="button"
                      className="button-small"
                      aria-label={`${position.name} nach unten`}
                      disabled={index === form.positions.length - 1}
                      onClick={() => movePosition(index, 1)}
                    >
                      ↓
                    </button>
                    <button
                      type="button"
                      className="button-small button-danger-quiet"
                      aria-label={`${position.name} entfernen`}
                      onClick={() => removePosition(position.key)}
                    >
                      Entfernen
                    </button>
                  </div>
                </div>
                {position.id && !position.exerciseId && (
                  <p className="hint">Die Ursprungsübung wurde gelöscht. Der Text bleibt im Plan erhalten.</p>
                )}
                {position.description && (
                  <details>
                    <summary>Beschreibung</summary>
                    <p className="position-description">{position.description}</p>
                  </details>
                )}
                <div className="form-row">
                  <label>
                    Minuten
                    <input
                      type="number"
                      inputMode="numeric"
                      required
                      min={1}
                      max={180}
                      value={position.duration}
                      onChange={(e) => updatePosition(position.key, { duration: e.target.value })}
                    />
                  </label>
                  <label>
                    Notiz
                    <input
                      maxLength={1000}
                      value={position.note}
                      onChange={(e) => updatePosition(position.key, { note: e.target.value })}
                    />
                  </label>
                </div>
              </li>
            ))}
          </ol>
          <p className={warning ? 'warning' : 'hint'}>
            {warning ?? `Summe ${total} Minuten, passt zur geplanten Dauer.`}
          </p>
          <button type="button" className="button-secondary" onClick={() => setPickerOpen((open) => !open)}>
            {pickerOpen ? 'Übungsauswahl schließen' : 'Übung hinzufügen'}
          </button>
          {pickerOpen && (
            <ExercisePicker level={form.level} participants={form.participantCount} focuses={focuses} onAdd={addExercise} />
          )}
          <SuggestionPanel
            participantCount={form.participantCount}
            level={form.level}
            spread={form.spread}
            duration={form.duration}
            focusIds={form.focusIds}
            onAccepted={addExercise}
          />
        </fieldset>

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
          <Link to={isNew ? '/plaene' : `/plaene/${id}`}>Abbrechen</Link>
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
