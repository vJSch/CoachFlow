import type { Focus, Level } from '../exercises/model'

export type Spread = 'Low' | 'Medium' | 'High'

export type PlanPosition = {
  id: string
  exerciseId: string | null
  name: string
  description: string
  plannedDurationMinutes: number
  note: string
}

export type Plan = {
  id: string
  title: string
  participantCount: number
  level: Level
  spread: Spread
  durationMinutes: number
  note: string
  focuses: Focus[]
  positions: PlanPosition[]
  totalPositionMinutes: number
  warnings: string[]
  version: number
  updatedBy: string | null
  updatedAt: string
}

export const maxPlanFocuses = 2

export const spreads: { value: Spread; label: string }[] = [
  { value: 'Low', label: 'gering' },
  { value: 'Medium', label: 'mittel' },
  { value: 'High', label: 'hoch' },
]

export function spreadLabel(spread: Spread) {
  return spreads.find((s) => s.value === spread)?.label ?? spread
}

// Gleiche Regel wie im Backend. Die Abweichung ist nur ein Hinweis und verhindert das Speichern nicht.
export function durationWarning(planMinutes: number, positionMinutes: number[]) {
  if (positionMinutes.length === 0 || !Number.isFinite(planMinutes)) return null
  const total = positionMinutes.reduce((sum, minutes) => sum + (Number.isFinite(minutes) ? minutes : 0), 0)
  return total === planMinutes ? null : `Die Übungen dauern zusammen ${total} Minuten, geplant sind ${planMinutes} Minuten.`
}
