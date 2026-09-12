<script setup lang="ts">
import { TIMER_OPTIONS } from '../models/timers'

defineProps<{
  configDeck: string
  configCustom: string
  configTimer: string
  applyingConfig: boolean
}>()

defineEmits<{
  (e: 'update:configDeck', value: string): void
  (e: 'update:configCustom', value: string): void
  (e: 'update:configTimer', value: string): void
  (e: 'apply'): void
}>()
</script>

<template>
  <div class="controls">
    <label>Deck
      <select :value="configDeck" @change="$emit('update:configDeck', ($event.target as HTMLSelectElement).value)">
        <option value="fib">Fibonacci</option>
        <option value="inc">Incremental</option>
        <option value="custom">Custom</option>
      </select>
    </label>
    <label v-if="configDeck === 'custom'">Cards
      <input :value="configCustom" placeholder="1,2,4,8,16" @input="$emit('update:configCustom', ($event.target as HTMLInputElement).value)" />
    </label>
    <label>Timer
      <select :value="configTimer" @change="$emit('update:configTimer', ($event.target as HTMLSelectElement).value)">
        <option v-for="t in TIMER_OPTIONS" :key="t" :value="t">{{ t }}</option>
      </select>
    </label>
    <button :disabled="applyingConfig" title="Applies from next round" @click="$emit('apply')">Apply for next round</button>
  </div>
</template>
