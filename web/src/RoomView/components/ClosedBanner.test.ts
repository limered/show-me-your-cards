import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ClosedBanner from './ClosedBanner.vue'

describe('ClosedBanner', () => {
  it('shows the read-only banner when closed', () => {
    const w = mount(ClosedBanner, { props: { closed: true } })
    expect(w.text()).toContain('read-only')
  })

  it('renders nothing when open', () => {
    expect(mount(ClosedBanner, { props: { closed: false } }).text()).toBe('')
  })
})
