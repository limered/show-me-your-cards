import { describe, expect, it } from 'vitest'
import { isTimerSetting, TIMER_OPTIONS } from './timers'

describe('timers', () => {
  it('covers every supported setting including off', () => {
    expect([...TIMER_OPTIONS]).toEqual(['1s', '5s', '30s', '1m', '2m', '5m', 'off'])
  })

  it('accepts known settings and rejects the rest', () => {
    expect(isTimerSetting('2m')).toBe(true)
    expect(isTimerSetting('off')).toBe(true)
    expect(isTimerSetting('9m')).toBe(false)
    expect(isTimerSetting(null)).toBe(false)
    expect(isTimerSetting(undefined)).toBe(false)
  })
})
