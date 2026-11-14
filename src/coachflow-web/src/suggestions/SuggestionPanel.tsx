import { useEffect, useRef, useState } from 'react'
import { api, ApiError } from '../api'
import { useConnected } from '../auth'
import type { Exercise, Level } from '../exercises/model'
import type { Spread } from '../plans/model'
import { requestSuggestions, slowMessage, type Suggestion } from './requestSuggestions'

type Props = {
  participantCount: string
  level: Level | ''
  spread: Spread | ''
  duration: string
  focusIds: string[]
  // Übernommene Vorschläge sind gespeicherte Übungen und kommen direkt in den Plan.
  onAccepted: (exercise: Exercise) => void
}

type Draft = {
  key: number
  name: string
  description: string
  duration: string
  minParticipants: number | null
  maxParticipants: number | null
  error: string | null
  saving: boolean
}

type Status = { kind: 'idle' } | { kind: 'loading'; slow: boolean } | { kind: 'error'; message: string }

let nextKey = 0

function toDraft(suggestion: Suggestion): Draft {
  return {
    key: ++nextKey,
    name: suggestion.name,
    description: suggestion.description,
    duration: String(suggestion.defaultDurationMinutes),
    minParticipants: suggestion.minParticipants,
    maxParticipants: suggestion.maxParticipants,
    error: null,
    saving: false,
  }
}

export function SuggestionPanel({ participantCount, level, spread, duration, focusIds, onAccepted }: Props) {
  const connected = useConnected()
  const [enabled, setEnabled] = useState<boolean | null>(null)
  const [status, setStatus] = useState<Status>({ kind: 'idle' })
  const [drafts, setDrafts] = useState<Draft[]>([])
  const [isTest, setIsTest] = useState(false)
  const controllerRef = useRef<AbortController | null>(null)

  useEffect(() => {
    api<{ enabled: boolean }>('/api/exercise-suggestions/status').then(
      (result) => setEnabled(result.enabled),
      () => setEnabled(false),
    )
  }, [])

  // Verlässt man den Editor während einer Anfrage, wird sie abgebrochen.
  useEffect(() => () => controllerRef.current?.abort(), [])

  if (enabled === false) {
    return <p className="hint">KI-Vorschläge sind auf diesem Server noch nicht eingerichtet.</p>
  }

  const missing = !participantCount || !level || !spread || !duration
  const loading = status.kind === 'loading'

  async function start() {
    if (missing || !level || !spread) return
    const controller = new AbortController()
    controllerRef.current = controller
    setStatus({ kind: 'loading', slow: false })
    const outcome = await requestSuggestions(
      {
        participantCount: Number(participantCount),
        level,
        spread,
        durationMinutes: Number(duration),
        focusIds,
      },
      { signal: controller.signal, onSlow: () => setStatus({ kind: 'loading', slow: true }) },
    )
    controllerRef.current = null
    if (controller.signal.aborted && outcome.kind === 'cancelled') {
      setStatus({ kind: 'idle' })
    } else if (outcome.kind === 'ok') {
      setDrafts(outcome.suggestions.map(toDraft))
      setIsTest(outcome.isTestProvider)
      setStatus({ kind: 'idle' })
    } else if (outcome.kind === 'error') {
      setStatus({ kind: 'error', message: outcome.message })
    }
  }

  function updateDraft(key: number, changes: Partial<Draft>) {
    setDrafts((current) => current.map((d) => (d.key === key ? { ...d, ...changes } : d)))
  }

  function discard(key: number) {
    setDrafts((current) => current.filter((d) => d.key !== key))
  }

  async function accept(draft: Draft) {
    updateDraft(draft.key, { saving: true, error: null })
    try {
      // Niveau und Schwerpunkte kommen aus dem Plan, für den der Vorschlag erstellt wurde.
      const exercise = await api<Exercise>('/api/exercises', {
        body: {
          name: draft.name,
          description: draft.description,
          defaultDurationMinutes: draft.duration.trim() === '' ? null : Number(draft.duration),
          minParticipants: draft.minParticipants,
          maxParticipants: draft.maxParticipants,
          level: level || null,
          origin: 'Ai',
          focusIds,
        },
      })
      onAccepted(exercise)
      discard(draft.key)
    } catch (caught) {
      updateDraft(draft.key, {
        saving: false,
        error: caught instanceof ApiError ? caught.message : 'Die Übung konnte nicht gespeichert werden.',
      })
    }
  }

  return (
    <div className="suggestions">
      <div className="suggestions-actions">
        <button type="button" className="button-secondary" onClick={() => void start()} disabled={!connected || missing || loading}>
          Übungen vorschlagen (KI)
        </button>
        {loading && (
          <button type="button" className="button-link" onClick={() => controllerRef.current?.abort()}>
            Anfrage abbrechen
          </button>
        )}
      </div>
      {!connected && <p className="hint">Vorschläge brauchen eine Verbindung.</p>}
      {connected && missing && <p className="hint">Für Vorschläge zuerst Teilnehmende, Dauer, Niveau und Streuung angeben.</p>}

      {loading && (
        <div role="status" aria-live="polite">
          <p>Vorschläge werden erstellt …</p>
          {status.slow && <p className="hint">{slowMessage}</p>}
        </div>
      )}
      {status.kind === 'error' && (
        <p className="error" role="alert">
          {status.message}
        </p>
      )}

      {drafts.length > 0 && (
        <>
          {isTest && <p className="warning">Testmodus: Das sind vorbereitete Beispiele, keine echten KI-Vorschläge.</p>}
          <p className="hint">
            KI-Vorschläge werden nicht fachlich geprüft. Prüfe sie vor dem Training, besonders bei Kindern. Nicht übernommene
            Vorschläge gehen beim Verlassen der Seite verloren.
          </p>
          <ul className="suggestion-list">
            {drafts.map((draft) => (
              <li key={draft.key} className="position-item">
                <label>
                  Name
                  <input maxLength={120} value={draft.name} onChange={(e) => updateDraft(draft.key, { name: e.target.value })} />
                </label>
                <label>
                  Beschreibung
                  <textarea
                    rows={4}
                    maxLength={4000}
                    value={draft.description}
                    onChange={(e) => updateDraft(draft.key, { description: e.target.value })}
                  />
                </label>
                <label>
                  Minuten
                  <input
                    type="number"
                    inputMode="numeric"
                    min={1}
                    max={180}
                    value={draft.duration}
                    onChange={(e) => updateDraft(draft.key, { duration: e.target.value })}
                  />
                </label>
                {draft.error && <p className="error">{draft.error}</p>}
                <div className="form-actions">
                  <button type="button" onClick={() => void accept(draft)} disabled={draft.saving || !connected}>
                    {draft.saving ? 'Wird übernommen …' : 'Übernehmen'}
                  </button>
                  <button type="button" className="button-link" onClick={() => discard(draft.key)}>
                    Verwerfen
                  </button>
                </div>
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  )
}
