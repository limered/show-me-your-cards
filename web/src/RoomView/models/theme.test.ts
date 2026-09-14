import { describe, expect, it } from 'vitest'
import { THEME_KEY, loadTheme, saveTheme, themeAttribute, toggleMode } from './theme'

function memoryStorage(initial: Record<string, string> = {}) {
  const data = { ...initial }
  return {
    getItem: (key: string) => (key in data ? data[key] : null),
    setItem: (key: string, value: string) => {
      data[key] = value
    },
    data,
  }
}

describe('room theme', () => {
  it('toggles between per-skin light and dark', () => {
    expect(toggleMode('dark')).toBe('light')
    expect(toggleMode('light')).toBe('dark')
  })

  it('maps each mode to the dreamy-neon skin attribute', () => {
    expect(themeAttribute('dark')).toBe('dreamy-neon-dark')
    expect(themeAttribute('light')).toBe('dreamy-neon-light')
  })

  it('defaults to the dark base style', () => {
    expect(loadTheme(memoryStorage())).toBe('dark')
    expect(loadTheme(memoryStorage({ [THEME_KEY]: 'nope' }))).toBe('dark')
  })

  it('round-trips the saved mode', () => {
    const storage = memoryStorage()
    saveTheme(storage, 'light')
    expect(storage.data[THEME_KEY]).toBe('light')
    expect(loadTheme(storage)).toBe('light')
  })
})
