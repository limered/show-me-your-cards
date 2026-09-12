import { describe, expect, it, vi } from 'vitest'
import { remainingSeconds } from './api'
import {
  applyRoomConfig,
  classifyJoinError,
  closeRoom,
  isNewRound,
  resolveConfigDeck,
  shouldFireAutoReveal,
} from './RoomView/composables/roomLogic'
import type { Snapshot } from './api'

function snapshot(overrides: Partial<Snapshot> = {}): Snapshot {
  return {
    id: 'room1',
    deck: '1,2,3,5,8,13,21',
    timer: '2m',
    closed: false,
    revealed: false,
    deadlineUtc: '1970-01-01T00:01:00.000Z',
    round: 1,
    roundStartedAtUtc: '1970-01-01T00:00:00.000Z',
    players: [],
    youSpot: null,
    youCard: null,
    result: null,
    ...overrides,
  }
}

describe('markJoinDeadEnd', () => {
  it('flags a full table', () => {
    expect(classifyJoinError('Conflict: {"error":"table_full"}')).toBe('table_full')
  })

  it('flags a closed session', () => {
    expect(classifyJoinError('Conflict: {"error":"closed"}')).toBe('closed')
  })

  it('falls back to a generic error', () => {
    expect(classifyJoinError('TypeError: failed to fetch')).toBe('generic')
  })
})

describe('fireAutoReveal', () => {
  const atDeadline = remainingSeconds('1970-01-01T00:01:00.000Z', 60_000)

  it('fires when the countdown hits zero', () => {
    expect(shouldFireAutoReveal(snapshot(), atDeadline, undefined)).toBe(true)
  })

  it('stays quiet on closed or revealed snapshots', () => {
    expect(shouldFireAutoReveal(snapshot({ closed: true }), 0, undefined)).toBe(false)
    expect(shouldFireAutoReveal(snapshot({ revealed: true }), 0, undefined)).toBe(false)
  })

  it('stays quiet while time remains or the timer is off', () => {
    expect(shouldFireAutoReveal(snapshot(), 30, undefined)).toBe(false)
    expect(shouldFireAutoReveal(snapshot({ deadlineUtc: null }), null, undefined)).toBe(false)
  })

  it('fires only once per deadline', () => {
    const snap = snapshot()
    expect(shouldFireAutoReveal(snap, 0, snap.deadlineUtc)).toBe(false)
    expect(shouldFireAutoReveal(snap, 0, 'other-deadline')).toBe(true)
  })

  it('ignores a missing snapshot', () => {
    expect(shouldFireAutoReveal(null, 0, undefined)).toBe(false)
  })
})

describe('applyConfig', () => {
  it('resolves the custom deck before configuring', async () => {
    const configureSession = vi.fn().mockResolvedValue(undefined)
    const fetchSnapshot = vi.fn().mockResolvedValue(snapshot())
    await applyRoomConfig({ configureSession, fetchSnapshot }, 'room1', 'tok', 'custom', '1,2,4', '5m')
    expect(configureSession).toHaveBeenCalledWith('room1', '1,2,4', '5m')
    expect(fetchSnapshot).toHaveBeenCalledWith('room1', 'tok')
  })

  it('passes named decks through and drops blanks', async () => {
    const configureSession = vi.fn().mockResolvedValue(undefined)
    const fetchSnapshot = vi.fn().mockResolvedValue(snapshot())
    await applyRoomConfig({ configureSession, fetchSnapshot }, 'room1', null, 'fib', '', '')
    expect(configureSession).toHaveBeenCalledWith('room1', 'fib', undefined)
  })

  it('does nothing without a room and surfaces configure errors', async () => {
    const api = { configureSession: vi.fn(), fetchSnapshot: vi.fn() }
    expect(await applyRoomConfig(api, null, null, 'fib', '', '2m')).toBeUndefined()
    expect(api.configureSession).not.toHaveBeenCalled()
    api.configureSession.mockRejectedValue(new Error('boom'))
    await expect(applyRoomConfig(api, 'room1', null, 'fib', '', '2m')).rejects.toThrow('boom')
  })

  it('resolves named versus custom decks', () => {
    expect(resolveConfigDeck('fib', '1,2')).toBe('fib')
    expect(resolveConfigDeck('custom', '1,2')).toBe('1,2')
  })
})

describe('doClose', () => {
  it('closes then refreshes the snapshot', async () => {
    const closeSession = vi.fn().mockResolvedValue(undefined)
    const fetchSnapshot = vi.fn().mockResolvedValue(snapshot({ closed: true }))
    const next = await closeRoom({ closeSession, fetchSnapshot }, 'room1', 'tok')
    expect(closeSession).toHaveBeenCalledWith('room1')
    expect(next?.closed).toBe(true)
  })

  it('does nothing without a room', async () => {
    const api = { closeSession: vi.fn(), fetchSnapshot: vi.fn() }
    expect(await closeRoom(api, null, null)).toBeUndefined()
    expect(api.closeSession).not.toHaveBeenCalled()
  })
})

describe('onTick', () => {
  it('drives the countdown from the broadcast deadline', () => {
    const snap = snapshot({ deadlineUtc: '1970-01-01T00:01:00.000Z' })
    const remaining = remainingSeconds(snap.deadlineUtc, 30_000)
    expect(remaining).toBe(30)
    expect(shouldFireAutoReveal(snap, remaining, undefined)).toBe(false)
  })

  it('detects a new round-started broadcast and resets the fired deadline', () => {
    const previousRound = 1
    const next = snapshot({ round: 2, deadlineUtc: '1970-01-01T00:05:00.000Z' })
    expect(isNewRound(previousRound, next)).toBe(true)
    expect(isNewRound(previousRound, snapshot())).toBe(false)
    expect(isNewRound(null, next)).toBe(false)
    expect(isNewRound(previousRound, null)).toBe(false)
    const lastFiredDeadlineUtc = '1970-01-01T00:01:00.000Z'
    const reset = isNewRound(previousRound, next) ? undefined : lastFiredDeadlineUtc
    expect(shouldFireAutoReveal(next, 0, reset)).toBe(true)
  })
})
