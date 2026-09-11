import { describe, expect, it } from 'vitest'
import { sessionLink, tokenKey } from './api'

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
