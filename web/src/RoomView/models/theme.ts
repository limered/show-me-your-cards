export const SKIN = 'dreamy-neon'

export type ThemeMode = 'dark' | 'light'

export const THEME_KEY = 'smyc:theme'

export function toggleMode(mode: ThemeMode): ThemeMode {
  return mode === 'dark' ? 'light' : 'dark'
}

export function themeAttribute(mode: ThemeMode): string {
  return `${SKIN}-${mode}`
}

interface ThemeStorage {
  getItem: (key: string) => string | null
  setItem: (key: string, value: string) => void
}

export function loadTheme(storage: ThemeStorage): ThemeMode {
  return storage.getItem(THEME_KEY) === 'light' ? 'light' : 'dark'
}

export function saveTheme(storage: ThemeStorage, mode: ThemeMode): void {
  storage.setItem(THEME_KEY, mode)
}
