import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import JoinDeadEnd from './JoinDeadEnd.vue'

describe('JoinDeadEnd', () => {
  it('explains a full table', () => {
    const w = mount(JoinDeadEnd, { props: { tableFull: true, joinClosed: false } })
    expect(w.text()).toContain('Table is full')
  })

  it('explains a closed session', () => {
    const w = mount(JoinDeadEnd, { props: { tableFull: false, joinClosed: true } })
    expect(w.text()).toContain('closed')
  })

  it('renders nothing when joining is possible', () => {
    expect(mount(JoinDeadEnd, { props: { tableFull: false, joinClosed: false } }).text()).toBe('')
  })
})
