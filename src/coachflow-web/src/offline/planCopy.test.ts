import 'fake-indexeddb/auto'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../api'
import type { Plan } from '../plans/model'
import { clearCopy, loadCopy, loadPlan, loadPlans, refreshCopy, saveCopy } from './planCopy'

const toni = { email: 'toni@example.org', name: 'Toni' }
const kim = { email: 'kim@example.org', name: 'Kim' }

function plan(id: string, title: string): Plan {
  return {
    id,
    title,
    participantCount: 12,
    level: 'Beginner',
    spread: 'Medium',
    durationMinutes: 90,
    note: '',
    focuses: [],
    positions: [],
    totalPositionMinutes: 0,
    warnings: [],
    version: 1,
    updatedBy: 'Toni',
    updatedAt: '2026-11-01T12:00:00Z',
  }
}

function respondWith(status: number, body: unknown) {
  vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify(body), { status })))
}

function failNetwork() {
  vi.stubGlobal('fetch', vi.fn(async () => {
    throw new TypeError('Failed to fetch')
  }))
}

beforeEach(async () => {
  await clearCopy()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('Offline-Kopie', () => {
  it('speichert, liest und löscht die Kopie', async () => {
    await saveCopy(toni, [plan('1', 'Dienstag')], new Date('2026-11-10T18:30:00Z'))

    const copy = await loadCopy()
    await clearCopy()

    expect(copy).toEqual({ email: toni.email, name: 'Toni', savedAt: '2026-11-10T18:30:00.000Z', plans: [plan('1', 'Dienstag')] })
    expect(await loadCopy()).toBeNull()
  })

  it('ersetzt beim erfolgreichen Laden die Kopie durch den aktuellen Stand', async () => {
    await saveCopy(toni, [plan('alt', 'Alt')])
    respondWith(200, [plan('1', 'Neu')])

    const result = await loadPlans(toni)

    expect(result).toEqual({ plans: [plan('1', 'Neu')], offlineSince: null })
    expect((await loadCopy())?.plans.map((p) => p.title)).toEqual(['Neu'])
  })

  it('nutzt ohne Verbindung die Kopie und nennt ihren Stand', async () => {
    await saveCopy(toni, [plan('1', 'Dienstag')], new Date('2026-11-10T18:30:00Z'))
    failNetwork()

    const result = await loadPlans(toni)

    expect(result.plans.map((p) => p.title)).toEqual(['Dienstag'])
    expect(result.offlineSince).toBe('2026-11-10T18:30:00.000Z')
  })

  it('nutzt die Kopie auch, wenn der Server gestört ist', async () => {
    await saveCopy(toni, [plan('1', 'Dienstag')])
    respondWith(502, {})

    const result = await loadPlans(toni)

    expect(result.plans.map((p) => p.title)).toEqual(['Dienstag'])
    expect(result.offlineSince).not.toBeNull()
  })

  it('nutzt die Kopie, wenn der Server nicht rechtzeitig antwortet', async () => {
    await saveCopy(toni, [plan('1', 'Dienstag')])
    // Antwortet nie, bricht nur über das Signal ab, wie hängendes WLAN.
    vi.stubGlobal('fetch', vi.fn((_url: string, init: RequestInit) => new Promise((_resolve, reject) => {
      init.signal?.addEventListener('abort', () => reject(init.signal?.reason))
    })))

    const result = await loadPlans(toni, 50)

    expect(result.plans.map((p) => p.title)).toEqual(['Dienstag'])
    expect(result.offlineSince).not.toBeNull()
  })

  it('zeigt ohne Verbindung keine Kopie eines anderen Kontos', async () => {
    await saveCopy(kim, [plan('1', 'Von Kim')])
    failNetwork()

    await expect(loadPlans(toni)).rejects.toThrow('noch keine Pläne für offline gespeichert')
  })

  it('fällt ohne gültige Anmeldung nicht auf die Kopie zurück', async () => {
    await saveCopy(toni, [plan('1', 'Dienstag')])
    respondWith(401, { detail: 'Bitte melde dich an.' })

    const error = await loadPlans(toni).catch((caught: unknown) => caught)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(401)
  })

  it('findet einzelne Pläne ohne Verbindung in der Kopie', async () => {
    await saveCopy(toni, [plan('1', 'Dienstag'), plan('2', 'Donnerstag')], new Date('2026-11-10T18:30:00Z'))
    failNetwork()

    const found = await loadPlan(toni, '2')

    expect(found.plan.title).toBe('Donnerstag')
    expect(found.offlineSince).toBe('2026-11-10T18:30:00.000Z')
    await expect(loadPlan(toni, 'fehlt')).rejects.toThrow('nicht offline gespeichert')
  })

  it('behält die bisherige Kopie, wenn das Auffrischen scheitert', async () => {
    await saveCopy(toni, [plan('1', 'Dienstag')])
    failNetwork()

    await refreshCopy(toni)

    expect((await loadCopy())?.plans.map((p) => p.title)).toEqual(['Dienstag'])
  })
})
