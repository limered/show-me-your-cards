import { describe, expect, it } from 'vitest'
import { formatCountdown, remainingSeconds, sessionLink, tokenKey } from './api'

describe('session link', () => {
  it('points at the short-id room path', () => {
    expect(sessionLink('Ab3x9Q2z')).toBe('/s/Ab3x9Q2z')
  })
})

describe('token storage', () => {
  it('scopes the player token to its session', () => {
    expect(tokenKey('Ab3x9Q2z')).toBe('smyc:Ab3x9Q2z')
  })
})

describe('round countdown', () => {
  it('returns null when the timer is off', () => {
    expect(remainingSeconds(null, 1000)).toBeNull()
  })

  it('counts down to the server deadline', () => {
    expect(remainingSeconds('1970-01-01T00:01:00.000Z', 30_000)).toBe(30)
  })

  it('floors at zero once the deadline has passed', () => {
    expect(remainingSeconds('1970-01-01T00:01:00.000Z', 61_000)).toBe(0)
  })

  it('formats minutes and seconds with an off fallback', () => {
    expect(formatCountdown(90)).toBe('1:30')
    expect(formatCountdown(5)).toBe('0:05')
    expect(formatCountdown(null)).toBe('Off')
  })
})
