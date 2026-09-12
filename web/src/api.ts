export interface Seat {
  spot: number
  name: string
  played: boolean
  card: string | null
}

export interface SessionSummary {
  id: string
  deck: string
  timer: string
  players: number
}

export interface RevealResult {
  card: string
  mean: number
  spots: number[]
}

export interface Snapshot {
  id: string
  deck: string
  timer: string
  closed: boolean
  revealed: boolean
  deadlineUtc: string | null
  round: number
  roundStartedAtUtc: string | null
  players: Seat[]
  youSpot: number | null
  youCard: string | null
  result: RevealResult | null
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

export function joinSession(id: string, name?: string, token?: string | null): Promise<JoinResult> {
  return fetch(`/api/sessions/${id}/join`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ name: name || null, token: token || null }),
  }).then(json<JoinResult>)
}

async function post(id: string, action: string, body: object): Promise<void> {
  const res = await fetch(`/api/sessions/${id}/${action}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!res.ok) throw new Error(await res.text())
}

export function playCard(id: string, token: string, card: string): Promise<void> {
  return post(id, 'play', { token, card })
}

export function renamePlayer(id: string, token: string, name: string): Promise<void> {
  return post(id, 'rename', { token, name: name || null })
}

export function reveal(id: string): Promise<void> {
  return post(id, 'reveal', {})
}

export function newRound(id: string): Promise<void> {
  return post(id, 'round', {})
}

export function configureSession(id: string, deck?: string, timer?: string): Promise<void> {
  return post(id, 'config', { deck: deck || null, timer: timer || null })
}

export function closeSession(id: string): Promise<void> {
  return post(id, 'close', {})
}

export function remainingSeconds(deadlineUtc: string | null, nowMs = Date.now()): number | null {
  if (!deadlineUtc) return null
  const left = Math.ceil((Date.parse(deadlineUtc) - nowMs) / 1000)
  return Math.max(0, left)
}

export function formatCountdown(totalSeconds: number | null): string {
  if (totalSeconds === null) return 'Off'
  const m = Math.floor(totalSeconds / 60)
  const s = String(totalSeconds % 60).padStart(2, '0')
  return `${m}:${s}`
}
