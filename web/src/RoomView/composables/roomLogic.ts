import type { Snapshot } from '../../api'

export type JoinDeadEnd = 'table_full' | 'closed' | 'generic'

export function classifyJoinError(message: string): JoinDeadEnd {
  if (message.includes('table_full')) return 'table_full'
  if (message.includes('closed')) return 'closed'
  return 'generic'
}

export function shouldFireAutoReveal(
  snapshot: Snapshot | null,
  remaining: number | null,
  lastFiredDeadlineUtc: string | null | undefined,
): boolean {
  if (!snapshot || snapshot.closed || snapshot.revealed || remaining !== 0) return false
  if (lastFiredDeadlineUtc === snapshot.deadlineUtc) return false
  return true
}

export function resolveConfigDeck(configDeck: string, configCustom: string): string {
  return configDeck === 'custom' ? configCustom : configDeck
}

export function isNewRound(previousRound: number | null | undefined, snapshot: Snapshot | null): boolean {
  if (!snapshot || previousRound === null || previousRound === undefined) return false
  return snapshot.round !== previousRound
}

export async function applyRoomConfig(
  api: {
    configureSession: (roomId: string, deck?: string, timer?: string) => Promise<void>
    fetchSnapshot: (roomId: string, token?: string | null) => Promise<Snapshot>
  },
  roomId: string | null,
  token: string | null,
  configDeck: string,
  configCustom: string,
  configTimer: string,
): Promise<Snapshot | undefined> {
  if (!roomId) return undefined
  const deck = resolveConfigDeck(configDeck, configCustom)
  await api.configureSession(roomId, deck || undefined, configTimer || undefined)
  return api.fetchSnapshot(roomId, token)
}

export async function closeRoom(
  api: {
    closeSession: (roomId: string) => Promise<void>
    fetchSnapshot: (roomId: string, token?: string | null) => Promise<Snapshot>
  },
  roomId: string | null,
  token: string | null,
): Promise<Snapshot | undefined> {
  if (!roomId) return undefined
  await api.closeSession(roomId)
  return api.fetchSnapshot(roomId, token)
}
