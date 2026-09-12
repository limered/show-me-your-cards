import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import RoomTimer from './RoomTimer.vue'

describe('RoomTimer', () => {
  it('renders the countdown', () => {
    const w = mount(RoomTimer, { props: { countdown: '1:30' } })
    expect(w.text()).toBe('1:30')
    expect(w.classes()).toContain('timer')
  })

  it('renders the off fallback', () => {
    expect(mount(RoomTimer, { props: { countdown: 'Off' } }).text()).toBe('Off')
  })
})
