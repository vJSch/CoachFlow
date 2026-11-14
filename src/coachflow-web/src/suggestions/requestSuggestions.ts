import { api, ApiError } from '../api'
import type { Level } from '../exercises/model'
import type { Spread } from '../plans/model'

export type Suggestion = {
  name: string
  description: string
  defaultDurationMinutes: number
  minParticipants: number | null
  maxParticipants: number | null
}

export type SuggestionParams = {
  participantCount: number
  level: Level
  spread: Spread
  durationMinutes: number
  focusIds: string[]
}

export type SuggestionOutcome =
  | { kind: 'ok'; suggestions: Suggestion[]; isTestProvider: boolean }
  | { kind: 'cancelled' }
  | { kind: 'error'; message: string }

// Zeiten und Meldungen wie vereinbart (R6).
export const slowHintMs = 5_000
export const giveUpMs = 15_000
export const slowMessage = 'Die Erstellung dauert länger. Du kannst weiter warten oder abbrechen.'
export const timeoutMessage = 'Es konnte rechtzeitig kein Übungsvorschlag erstellt werden. Deine Eingaben bleiben erhalten.'

// Fordert Vorschläge an. Nach 5 Sekunden kommt onSlow, nach 15 Sekunden endet das Warten.
// Ein Abbruch über signal beendet auch die Anfrage beim Server.
export async function requestSuggestions(
  params: SuggestionParams,
  options: { signal: AbortSignal; onSlow: () => void },
): Promise<SuggestionOutcome> {
  const controller = new AbortController()
  let timedOut = false
  const cancel = () => controller.abort()
  options.signal.addEventListener('abort', cancel)
  const slowTimer = setTimeout(options.onSlow, slowHintMs)
  const giveUpTimer = setTimeout(() => {
    timedOut = true
    controller.abort()
  }, giveUpMs)

  try {
    const result = await api<{ suggestions: Suggestion[]; isTestProvider: boolean }>('/api/exercise-suggestions', {
      body: params,
      signal: controller.signal,
    })
    return { kind: 'ok', suggestions: result.suggestions, isTestProvider: result.isTestProvider }
  } catch (error) {
    if (timedOut) return { kind: 'error', message: timeoutMessage }
    if (options.signal.aborted) return { kind: 'cancelled' }
    return { kind: 'error', message: error instanceof ApiError ? error.message : 'Es konnte kein Vorschlag erstellt werden.' }
  } finally {
    clearTimeout(slowTimer)
    clearTimeout(giveUpTimer)
    options.signal.removeEventListener('abort', cancel)
  }
}
