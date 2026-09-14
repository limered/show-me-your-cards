import { describe, expect, it } from 'vitest'
import {
  FLIGHT_DURATION_MS,
  THROW_COOLDOWN_MS,
  THROW_EMOJIS,
  bezierPoint,
  flightControl,
  throwAllowed,
} from './emoji'

describe('emoji throw cooldown', () => {
  it('allows the first throw', () => {
    expect(throwAllowed(10_000, null)).toBe(true)
    expect(throwAllowed(10_000, undefined)).toBe(true)
  })

  it('blocks throws inside the cooldown window', () => {
    expect(throwAllowed(10_000, 9_000)).toBe(false)
    expect(throwAllowed(10_000, 8_501)).toBe(false)
  })

  it('allows throws once the cooldown has elapsed', () => {
    expect(throwAllowed(10_000, 8_500)).toBe(true)
    expect(throwAllowed(10_000, 1_000)).toBe(true)
  })

  it('holds about one throw per 1.5s', () => {
    expect(THROW_COOLDOWN_MS).toBe(1_500)
  })
})

describe('throw picker', () => {
  it('offers a small radial set', () => {
    expect(THROW_EMOJIS.length).toBeGreaterThan(0)
    expect(THROW_EMOJIS.length).toBeLessThanOrEqual(8)
  })
})

describe('flight arc', () => {
  it('runs sub-second', () => {
    expect(FLIGHT_DURATION_MS).toBeGreaterThan(0)
    expect(FLIGHT_DURATION_MS).toBeLessThan(1_000)
  })

  it('interpolates along a quadratic bezier', () => {
    expect(bezierPoint({ x: 0, y: 0 }, { x: 50, y: -100 }, { x: 100, y: 0 }, 0)).toEqual({ x: 0, y: 0 })
    expect(bezierPoint({ x: 0, y: 0 }, { x: 50, y: -100 }, { x: 100, y: 0 }, 1)).toEqual({ x: 100, y: 0 })
    expect(bezierPoint({ x: 0, y: 0 }, { x: 50, y: -100 }, { x: 100, y: 0 }, 0.5)).toEqual({ x: 50, y: -50 })
  })

  it('lifts the control point above the seat-to-seat line', () => {
    const control = flightControl({ x: 0, y: 200 }, { x: 100, y: 200 })
    expect(control).toEqual({ x: 50, y: 80 })
  })
})
