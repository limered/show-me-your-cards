import { describe, expect, it } from 'vitest'
import { useEmojiThrows } from './composables/emojiThrows'
import { THROW_COOLDOWN_MS } from './models/emoji'
import { loadTheme, saveTheme, themeAttribute, toggleMode } from './models/theme'

describe('play-room throw flow', () => {
  it('opens a radial on another seat and throws the picked emoji seat-to-seat', () => {
    const room = useEmojiThrows()
    room.openRadial(120, 80, 4, 1)
    expect(room.radial.value).toEqual({ x: 120, y: 80, targetSpot: 4 })

    expect(room.pickEmoji('🎉', 1, 10_000)).toBe(true)
    expect(room.radial.value).toBeNull()
    expect(room.flights.value).toHaveLength(1)
    expect(room.flights.value[0]).toMatchObject({ emoji: '🎉', fromSpot: 1, toSpot: 4 })
  })

  it('ignores clicks on your own seat', () => {
    const room = useEmojiThrows()
    room.openRadial(10, 10, 1, 1)
    expect(room.radial.value).toBeNull()
  })

  it('cools down rapid picks so the room cannot be spammed', () => {
    const room = useEmojiThrows()
    room.openRadial(120, 80, 4, 1)
    expect(room.pickEmoji('🎉', 1, 10_000)).toBe(true)

    room.openRadial(130, 90, 5, 1)
    expect(room.pickEmoji('🔥', 1, 10_000 + THROW_COOLDOWN_MS - 1)).toBe(false)
    expect(room.flights.value).toHaveLength(1)

    expect(room.pickEmoji('🔥', 1, 10_000 + THROW_COOLDOWN_MS)).toBe(true)
    expect(room.flights.value).toHaveLength(2)
  })

  it('clears finished flights', () => {
    const room = useEmojiThrows()
    room.openRadial(120, 80, 4, 1)
    room.pickEmoji('🎉', 1, 10_000)
    room.finishFlight(room.flights.value[0].id)
    expect(room.flights.value).toHaveLength(0)
  })
})

describe('play-room theme flow', () => {
  it('flips the skin mode without leaving the table', () => {
    const store: Record<string, string> = {}
    const storage = {
      getItem: (key: string) => store[key] ?? null,
      setItem: (key: string, value: string) => {
        store[key] = value
      },
    }
    const mode = loadTheme(storage)
    const next = toggleMode(mode)
    saveTheme(storage, next)
    expect(themeAttribute(loadTheme(storage))).toBe('dreamy-neon-light')
  })
})
