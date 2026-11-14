import { useState } from 'react'
import { api, ApiError } from '../api'
import type { Focus } from './model'

type Props = {
  focuses: Focus[]
  selected: string[]
  onToggle: (id: string) => void
  // Neu angelegte oder bereits vorhandene Einträge werden übernommen und ausgewählt.
  onAdded: (focus: Focus) => void
  // Pläne erlauben höchstens zwei Schwerpunkte, Übungen beliebig viele.
  max?: number
}

export function FocusPicker({ focuses, selected, onToggle, onAdded, max }: Props) {
  const full = max !== undefined && selected.length >= max

  const [name, setName] = useState('')
  const [message, setMessage] = useState<{ text: string; isError: boolean } | null>(null)
  const [busy, setBusy] = useState(false)

  async function add() {
    if (!name.trim()) return
    setBusy(true)
    setMessage(null)
    try {
      const focus = await api<Focus>('/api/focuses', { body: { name } })
      onAdded(focus)
      setName('')
    } catch (caught) {
      const existing = caught instanceof ApiError && caught.status === 409 ? (caught.body.focus as Focus | undefined) : undefined
      if (existing) {
        onAdded(existing)
        setName('')
        const text = full ? `„${existing.name}“ gibt es bereits.` : `„${existing.name}“ gibt es bereits und ist jetzt ausgewählt.`
        setMessage({ text, isError: false })
      } else {
        setMessage({ text: caught instanceof ApiError ? caught.message : 'Der Schwerpunkt konnte nicht angelegt werden.', isError: true })
      }
    } finally {
      setBusy(false)
    }
  }

  return (
    <fieldset className="focus-picker">
      <legend>Schwerpunkte</legend>
      <div className="chips">
        {focuses.map((focus) => (
          <label key={focus.id} className={selected.includes(focus.id) ? 'chip chip-selected' : 'chip'}>
            <input
              type="checkbox"
              checked={selected.includes(focus.id)}
              disabled={full && !selected.includes(focus.id)}
              onChange={() => onToggle(focus.id)}
            />
            {focus.name}
          </label>
        ))}
      </div>
      <div className="inline-add">
        <input
          aria-label="Neuer Schwerpunkt"
          placeholder="Neuer Schwerpunkt"
          maxLength={60}
          value={name}
          onChange={(e) => setName(e.target.value)}
          onKeyDown={(e) => {
            // Enter soll den Schwerpunkt anlegen, nicht die ganze Übung speichern.
            if (e.key === 'Enter') {
              e.preventDefault()
              void add()
            }
          }}
        />
        <button type="button" onClick={() => void add()} disabled={busy || !name.trim()}>
          Hinzufügen
        </button>
      </div>
      {max !== undefined && (
        <p className="hint">
          Höchstens {max} Schwerpunkte je Plan.{full ? ' Entferne einen, um einen anderen zu wählen.' : ''}
        </p>
      )}
      <p className="hint">Neue Schwerpunkte stehen danach allen zur Verfügung und lassen sich nicht mehr löschen.</p>
      {message && <p className={message.isError ? 'error' : 'notice'}>{message.text}</p>}
    </fieldset>
  )
}
