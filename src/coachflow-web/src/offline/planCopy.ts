import { api, ApiError } from '../api'
import type { Account } from '../auth'
import type { Plan } from '../plans/model'

// Lesekopie aller Pläne auf diesem Gerät. Sie gehört zu genau einem Konto und wird beim Abmelden gelöscht.
export type PlanCopy = { email: string; name: string; savedAt: string; plans: Plan[] }

const databaseName = 'coachflow'
const storeName = 'offline'
const copyKey = 'plans'

function openDatabase(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(databaseName, 1)
    request.onupgradeneeded = () => request.result.createObjectStore(storeName)
    request.onsuccess = () => resolve(request.result)
    request.onerror = () => reject(request.error)
  })
}

async function withStore<T>(mode: IDBTransactionMode, action: (store: IDBObjectStore) => IDBRequest<T>): Promise<T> {
  const database = await openDatabase()
  try {
    return await new Promise<T>((resolve, reject) => {
      const transaction = database.transaction(storeName, mode)
      const request = action(transaction.objectStore(storeName))
      transaction.oncomplete = () => resolve(request.result)
      transaction.onerror = () => reject(transaction.error)
      transaction.onabort = () => reject(transaction.error)
    })
  } finally {
    database.close()
  }
}

let persistenceRequested = false

// Bittet den Browser, die Daten nicht bei Speicherknappheit zu löschen. Eine Garantie ist das nicht.
function requestPersistence() {
  if (persistenceRequested) return
  persistenceRequested = true
  navigator.storage?.persist?.().catch(() => undefined)
}

// Speicherfehler, z. B. im privaten Modus, dürfen die App nicht stören. Es gibt dann nur keine Offline-Kopie.
export async function saveCopy(account: Account, plans: Plan[], now = new Date()): Promise<boolean> {
  const copy: PlanCopy = { email: account.email, name: account.name, savedAt: now.toISOString(), plans }
  try {
    await withStore('readwrite', (store) => store.put(copy, copyKey))
    requestPersistence()
    return true
  } catch (error) {
    console.warn('Offline-Kopie konnte nicht gespeichert werden.', error)
    return false
  }
}

export async function loadCopy(): Promise<PlanCopy | null> {
  try {
    return (await withStore<PlanCopy | undefined>('readonly', (store) => store.get(copyKey))) ?? null
  } catch {
    return null
  }
}

export async function clearCopy(): Promise<void> {
  try {
    await withStore('readwrite', (store) => store.delete(copyKey))
  } catch (error) {
    console.warn('Offline-Kopie konnte nicht gelöscht werden.', error)
  }
}

// Lädt alle Pläne neu und ersetzt die Kopie. Ohne Verbindung bleibt die bisherige Kopie stehen.
export async function refreshCopy(account: Account): Promise<void> {
  try {
    await saveCopy(account, await api<Plan[]>('/api/plans'))
  } catch {
    // Nächster Versuch beim nächsten Start oder nach der nächsten Änderung.
  }
}

async function copyFor(account: Account): Promise<PlanCopy | null> {
  const copy = await loadCopy()
  return copy && copy.email === account.email ? copy : null
}

// Keine Verbindung oder Server gestört (z. B. 502 vom Proxy). Dann hilft die Kopie.
// Eine 401 dagegen heißt „nicht angemeldet“ und darf nicht zur Kopie führen.
export function isUnavailable(error: unknown) {
  return error instanceof ApiError && (error.status === 0 || error.status >= 500)
}

// Schlechtes Hallen-WLAN lässt Anfragen sonst minutenlang hängen. Danach gilt: keine Verbindung.
export const loadTimeoutMs = 8000

// offlineSince ist gesetzt, wenn die Daten aus der Kopie stammen, und nennt deren Stand.
export async function loadPlans(
  account: Account,
  timeoutMs = loadTimeoutMs,
): Promise<{ plans: Plan[]; offlineSince: string | null }> {
  try {
    const plans = await api<Plan[]>('/api/plans', { signal: AbortSignal.timeout(timeoutMs) })
    await saveCopy(account, plans)
    return { plans, offlineSince: null }
  } catch (error) {
    if (!isUnavailable(error)) throw error
    const copy = await copyFor(account)
    if (!copy) throw new ApiError(0, 'Keine Verbindung. Auf diesem Gerät sind noch keine Pläne für offline gespeichert.')
    return { plans: copy.plans, offlineSince: copy.savedAt }
  }
}

export async function loadPlan(
  account: Account,
  id: string,
  timeoutMs = loadTimeoutMs,
): Promise<{ plan: Plan; offlineSince: string | null }> {
  try {
    return { plan: await api<Plan>(`/api/plans/${id}`, { signal: AbortSignal.timeout(timeoutMs) }), offlineSince: null }
  } catch (error) {
    if (!isUnavailable(error)) throw error
    const copy = await copyFor(account)
    const plan = copy?.plans.find((p) => p.id === id)
    if (!copy || !plan) throw new ApiError(0, 'Keine Verbindung. Dieser Plan ist auf diesem Gerät nicht offline gespeichert.')
    return { plan, offlineSince: copy.savedAt }
  }
}
