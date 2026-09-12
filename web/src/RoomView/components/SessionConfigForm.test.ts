import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import SessionConfigForm from './SessionConfigForm.vue'
import { TIMER_OPTIONS } from '../models/timers'

describe('SessionConfigForm', () => {
  const props = { configDeck: 'fib', configCustom: '', configTimer: '2m', applyingConfig: false }

  it('lists every shared timer option', () => {
    const w = mount(SessionConfigForm, { props })
    const values = w.findAll('select')[1].findAll('option').map((o) => o.attributes('value'))
    expect(values).toEqual([...TIMER_OPTIONS])
  })

  it('shows the custom deck input only for custom decks', () => {
    expect(mount(SessionConfigForm, { props }).find('input').exists()).toBe(false)
    expect(mount(SessionConfigForm, { props: { ...props, configDeck: 'custom' } }).find('input').exists()).toBe(true)
  })

  it('emits apply and disables while applying', async () => {
    const w = mount(SessionConfigForm, { props })
    await w.find('button').trigger('click')
    expect(w.emitted('apply')).toHaveLength(1)
    expect(mount(SessionConfigForm, { props: { ...props, applyingConfig: true } }).find('button').attributes('disabled')).toBeDefined()
  })

  it('forwards deck and timer changes', async () => {
    const w = mount(SessionConfigForm, { props })
    await w.findAll('select')[0].setValue('custom')
    await w.findAll('select')[1].setValue('off')
    expect(w.emitted('update:configDeck')).toEqual([['custom']])
    expect(w.emitted('update:configTimer')).toEqual([['off']])
  })
})
