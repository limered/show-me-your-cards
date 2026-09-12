import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import PlayerHand from './PlayerHand.vue'

describe('PlayerHand', () => {
  it('hides the hand once the session is closed', () => {
    expect(mount(PlayerHand, { props: { closed: true, hand: ['1', '2'], youCard: null } }).text()).toBe('')
  })

  it('marks the voted card as selected', () => {
    const w = mount(PlayerHand, { props: { closed: false, hand: ['1', '2', '3'], youCard: '2' } })
    expect(w.findAll('button')).toHaveLength(3)
    expect(w.findAll('button')[1].classes()).toContain('selected')
  })

  it('emits the played card', async () => {
    const w = mount(PlayerHand, { props: { closed: false, hand: ['5'], youCard: null } })
    await w.find('button').trigger('click')
    expect(w.emitted('play')).toEqual([['5']])
  })
})
