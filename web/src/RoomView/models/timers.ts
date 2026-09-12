export const TIMER_OPTIONS = ['1s', '5s', '30s', '1m', '2m', '5m', 'off'] as const

export type TimerSetting = (typeof TIMER_OPTIONS)[number]

export function isTimerSetting(value: string | null | undefined): value is TimerSetting {
  return value !== null && value !== undefined && (TIMER_OPTIONS as readonly string[]).includes(value)
}
