export const THROW_EMOJIS = ['🎉', '👏', '😂', '🔥', '🚀', '☕', '💩', '❓'] as const

export type ThrowEmoji = (typeof THROW_EMOJIS)[number]

export const THROW_COOLDOWN_MS = 1_500

export const FLIGHT_DURATION_MS = 700

export interface Point {
  x: number
  y: number
}

export function throwAllowed(nowMs: number, lastThrowMs: number | null | undefined): boolean {
  if (lastThrowMs === null || lastThrowMs === undefined) return true
  return nowMs - lastThrowMs >= THROW_COOLDOWN_MS
}

export function bezierPoint(from: Point, control: Point, to: Point, t: number): Point {
  const u = 1 - t
  return {
    x: u * u * from.x + 2 * u * t * control.x + t * t * to.x,
    y: u * u * from.y + 2 * u * t * control.y + t * t * to.y,
  }
}

export function flightControl(from: Point, to: Point, lift = 120): Point {
  return { x: (from.x + to.x) / 2, y: (from.y + to.y) / 2 - lift }
}
