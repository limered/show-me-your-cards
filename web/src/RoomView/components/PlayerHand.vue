<script setup lang="ts">
defineProps<{
  closed: boolean
  hand: string[]
  youCard: string | null | undefined
}>()

defineEmits<{
  (e: 'play', card: string): void
}>()
</script>

<template>
  <div v-if="!closed" class="hand">
    <button
      v-for="c in hand"
      :key="c"
      :class="{ selected: youCard === c }"
      @click="$emit('play', c)"
    >
      {{ c }}
    </button>
  </div>
</template>
