import { ref } from 'vue'
import { throwAllowed, type ThrowEmoji } from '../models/emoji'

export interface RadialState {
  x: number
  y: number
  targetSpot: number
}

export interface Flight {
  id: number
  emoji: ThrowEmoji
  fromSpot: number
  toSpot: number
  startedAt: number
}

export function useEmojiThrows() {
  const radial = ref<RadialState | null>(null)
  const flights = ref<Flight[]>([])
  let lastThrowMs: number | null = null
  let nextId = 1

  function openRadial(x: number, y: number, targetSpot: number, ownSpot: number | null): void {
    if (targetSpot === ownSpot) return
    radial.value = { x, y, targetSpot }
  }

  function closeRadial(): void {
    radial.value = null
  }

  function pickEmoji(emoji: ThrowEmoji, fromSpot: number, nowMs = Date.now()): boolean {
    const target = radial.value
    if (!target || !throwAllowed(nowMs, lastThrowMs)) return false
    lastThrowMs = nowMs
    flights.value = [
      ...flights.value,
      { id: nextId++, emoji, fromSpot, toSpot: target.targetSpot, startedAt: nowMs },
    ]
    radial.value = null
    return true
  }

  function finishFlight(id: number): void {
    flights.value = flights.value.filter((flight) => flight.id !== id)
  }

  return { radial, flights, openRadial, closeRadial, pickEmoji, finishFlight }
}
