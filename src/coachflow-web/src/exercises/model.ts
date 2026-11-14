export type Level = 'MiniVolleyball' | 'MidiVolleyball' | 'Beginner' | 'Advanced'

export type ExerciseOrigin = 'Own' | 'Ai'

export type Focus = { id: string; name: string }

export type Exercise = {
  id: string
  name: string
  description: string
  defaultDurationMinutes: number
  minParticipants: number | null
  maxParticipants: number | null
  level: Level | null
  origin: ExerciseOrigin
  focuses: Focus[]
  version: number
  updatedBy: string | null
  updatedAt: string
}

export const levels: { value: Level; label: string }[] = [
  { value: 'MiniVolleyball', label: 'Mini-Volleyball' },
  { value: 'MidiVolleyball', label: 'Midi-Volleyball' },
  { value: 'Beginner', label: 'Anfänger' },
  { value: 'Advanced', label: 'Fortgeschrittene' },
]

export function levelLabel(level: Level | null) {
  return levels.find((l) => l.value === level)?.label ?? 'Alle Niveaus'
}

export function participantsLabel(min: number | null, max: number | null) {
  if (min !== null && max !== null) return min === max ? `${min} Personen` : `${min}–${max} Personen`
  if (min !== null) return `ab ${min} Personen`
  if (max !== null) return `bis ${max} Personen`
  return 'Beliebige Teilnehmerzahl'
}

export function formatDateTime(iso: string) {
  return new Date(iso).toLocaleString('de-DE', { dateStyle: 'medium', timeStyle: 'short' })
}
