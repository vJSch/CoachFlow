export class ApiError extends Error {
  readonly status: number
  // Vollständige Fehlerantwort, z. B. mit dem aktuellen Stand bei einem Konflikt.
  readonly body: Record<string, unknown>

  constructor(status: number, message: string, body: Record<string, unknown> = {}) {
    super(message)
    this.status = status
    this.body = body
  }
}

// Für Antworten ohne eigene Meldung, z. B. von der Ratenbegrenzung.
const fallbackMessages: Record<number, string> = {
  401: 'Bitte melde dich an.',
  429: 'Zu viele Anfragen. Bitte warte einen Moment und versuche es dann erneut.',
  503: 'Der Dienst ist gerade nicht verfügbar. Bitte versuche es später erneut.',
}

function messageFrom(body: Record<string, unknown>): string | undefined {
  // Prüffehler je Feld haben Vorrang, weil sie genauer sind als die Zusammenfassung.
  const errors = body.errors
  if (errors && typeof errors === 'object') {
    const messages = Object.values(errors as Record<string, string[]>).flat()
    if (messages.length > 0) return messages.join(' ')
  }
  return typeof body.detail === 'string' ? body.detail : undefined
}

export async function api<T = void>(
  path: string,
  options: { method?: 'GET' | 'POST' | 'PUT' | 'DELETE'; body?: unknown; signal?: AbortSignal } = {},
): Promise<T> {
  const hasBody = options.body !== undefined
  let response: Response
  try {
    response = await fetch(path, {
      method: options.method ?? (hasBody ? 'POST' : 'GET'),
      headers: hasBody ? { 'Content-Type': 'application/json' } : undefined,
      body: hasBody ? JSON.stringify(options.body) : undefined,
      credentials: 'same-origin',
      signal: options.signal,
    })
  } catch (error) {
    // Ein bewusster Abbruch bleibt ein Abbruch. Eine Zeitüberschreitung zählt wie fehlende Verbindung.
    const timedOut = options.signal?.reason instanceof DOMException && options.signal.reason.name === 'TimeoutError'
    if (options.signal?.aborted && !timedOut) throw error
    throw new ApiError(0, 'Keine Verbindung zum Server. Bitte prüfe die Internetverbindung.')
  }

  const text = await response.text()
  if (!response.ok) {
    let body: Record<string, unknown> = {}
    try {
      body = JSON.parse(text)
    } catch {
      // Antwort ohne JSON-Inhalt
    }
    throw new ApiError(
      response.status,
      messageFrom(body) ?? fallbackMessages[response.status] ?? 'Etwas ist schiefgelaufen. Bitte versuche es erneut.',
      body,
    )
  }

  return (text ? JSON.parse(text) : undefined) as T
}
