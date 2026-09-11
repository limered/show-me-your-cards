export interface Seat {
  spot: number
  name: string
}

export interface SessionSummary {
  id: string
  deck: string
  timer: string
  players: number
}

export interface Snapshot {
  id: string
  deck: string
  timer: string
  closed: boolean
  players: Seat[]
  youSpot: number | null
}

export interface JoinResult {
  token: string
  name: string
  spot: number
}

export function sessionLink(id: string): string {
  return `/s/${id}`
}

export function tokenKey(id: string): string {
  return `smyc:${id}`
}

async function json<T>(res: Response): Promise<T> {
  if (!res.ok) throw new Error(await res.text())
  return (await res.json()) as T
}

export function createSession(deck?: string, timer?: string): Promise<SessionSummary> {
  return fetch('/api/sessions', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ deck, timer }),
  }).then(json<SessionSummary>)
}

export function fetchSessions(): Promise<SessionSummary[]> {
  return fetch('/api/sessions').then(json<SessionSummary[]>)
}

export function fetchSnapshot(id: string, token?: string | null): Promise<Snapshot> {
  const q = token ? `?token=${encodeURIComponent(token)}` : ''
  return fetch(`/api/sessions/${id}${q}`).then(json<Snapshot>)
}

export function joinSession(id: string, name?: string): Promise<JoinResult> {
  return fetch(`/api/sessions/${id}/join`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ name: name || null }),
  }).then(json<JoinResult>)
}
