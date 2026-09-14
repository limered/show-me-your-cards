<script setup lang="ts">
import { onMounted, onUnmounted } from 'vue'
import { THROW_EMOJIS, type ThrowEmoji } from '../models/emoji'

defineProps<{
  x: number
  y: number
}>()

const emit = defineEmits<{
  (e: 'pick', emoji: ThrowEmoji): void
  (e: 'close'): void
}>()

function onKey(event: KeyboardEvent) {
  if (event.key === 'Escape') emit('close')
}

onMounted(() => window.addEventListener('keydown', onKey))
onUnmounted(() => window.removeEventListener('keydown', onKey))
</script>

<template>
  <div class="radial-backdrop" @click="emit('close')">
    <div class="radial" :style="{ left: `${x}px`, top: `${y}px` }" @click.stop>
      <button
        v-for="emoji in THROW_EMOJIS"
        :key="emoji"
        class="radial-emoji"
        @click="emit('pick', emoji)"
      >
        {{ emoji }}
      </button>
    </div>
  </div>
</template>

<style scoped>
.radial-backdrop {
  position: fixed;
  inset: 0;
  z-index: 40;
}
.radial {
  position: fixed;
  transform: translate(-50%, -50%);
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 0.25rem;
  padding: 0.5rem;
  border-radius: 1rem;
  background: var(--surface);
  border: 1px solid var(--neon);
  box-shadow: 0 0 18px var(--neon-glow);
}
.radial-emoji {
  font-size: 1.5rem;
  line-height: 1;
  padding: 0.35rem;
  border-radius: 0.75rem;
  border: 1px solid transparent;
  background: transparent;
  cursor: pointer;
  transition: transform 0.12s ease;
}
.radial-emoji:hover {
  transform: scale(1.35) rotate(8deg);
  border-color: var(--neon);
}
</style>
