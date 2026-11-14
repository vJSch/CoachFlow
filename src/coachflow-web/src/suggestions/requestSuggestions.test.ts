import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { requestSuggestions, slowMessage, timeoutMessage, type SuggestionParams } from './requestSuggestions'

const params: SuggestionParams = { participantCount: 12, level: 'Beginner', spread: 'High', durationMinutes: 90, focusIds: [] }

// Antwortet erst, wenn der Test es will, oder bricht beim Abbruch-Signal ab.
function pendingFetch() {
  let respond: (response: Response) => void = () => undefined
  const fetchMock = vi.fn((_url: string, init: RequestInit) => new Promise<Response>((resolve, reject) => {
    respond = resolve
    init.signal?.addEventListener('abort', () => reject(init.signal?.reason))
  }))
  vi.stubGlobal('fetch', fetchMock)
  return { fetchMock, respond: (status: number, body: unknown) => respond(new Response(JSON.stringify(body), { status })) }
}

beforeEach(() => {
  vi.useFakeTimers()
})

afterEach(() => {
  vi.useRealTimers()
  vi.unstubAllGlobals()
})

describe('Wartezeit bei KI-Vorschlägen', () => {
  it('liefert Vorschläge ohne Hinweis, wenn die Antwort schnell kommt', async () => {
    const { respond } = pendingFetch()
    const onSlow = vi.fn()

    const outcome = requestSuggestions(params, { signal: new AbortController().signal, onSlow })
    await vi.advanceTimersByTimeAsync(2_000)
    respond(200, { suggestions: [{ name: 'A' }], isTestProvider: true })

    expect(await outcome).toEqual({ kind: 'ok', suggestions: [{ name: 'A' }], isTestProvider: true })
    await vi.advanceTimersByTimeAsync(20_000)
    expect(onSlow).not.toHaveBeenCalled()
  })

  it('meldet sich nach 5 Sekunden und gibt nach 15 Sekunden mit der vereinbarten Meldung auf', async () => {
    const { fetchMock } = pendingFetch()
    const onSlow = vi.fn()

    const outcome = requestSuggestions(params, { signal: new AbortController().signal, onSlow })
    await vi.advanceTimersByTimeAsync(4_999)
    expect(onSlow).not.toHaveBeenCalled()
    await vi.advanceTimersByTimeAsync(1)
    expect(onSlow).toHaveBeenCalledOnce()
    await vi.advanceTimersByTimeAsync(10_000)

    expect(await outcome).toEqual({ kind: 'error', message: timeoutMessage })
    // Das Warten endet auch beim Server: Die Anfrage selbst wird abgebrochen.
    expect((fetchMock.mock.calls[0][1] as RequestInit).signal?.aborted).toBe(true)
    expect(slowMessage).toBe('Die Erstellung dauert länger. Du kannst weiter warten oder abbrechen.')
  })

  it('bricht auf Wunsch ab und meldet keinen Fehler', async () => {
    const { fetchMock } = pendingFetch()
    const user = new AbortController()

    const outcome = requestSuggestions(params, { signal: user.signal, onSlow: vi.fn() })
    await vi.advanceTimersByTimeAsync(3_000)
    user.abort()

    expect(await outcome).toEqual({ kind: 'cancelled' })
    expect((fetchMock.mock.calls[0][1] as RequestInit).signal?.aborted).toBe(true)
  })

  it('zeigt die Meldung des Servers bei Ablehnung', async () => {
    const { respond } = pendingFetch()

    const outcome = requestSuggestions(params, { signal: new AbortController().signal, onSlow: vi.fn() })
    respond(429, { detail: 'Es läuft bereits eine Anfrage.' })

    expect(await outcome).toEqual({ kind: 'error', message: 'Es läuft bereits eine Anfrage.' })
  })

  it('sendet nur die Planparameter', async () => {
    const { fetchMock, respond } = pendingFetch()

    const outcome = requestSuggestions(params, { signal: new AbortController().signal, onSlow: vi.fn() })
    respond(200, { suggestions: [], isTestProvider: false })
    await outcome

    expect(JSON.parse((fetchMock.mock.calls[0][1] as RequestInit).body as string)).toEqual(params)
  })
})
