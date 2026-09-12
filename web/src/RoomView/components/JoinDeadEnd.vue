<script setup lang="ts">
defineProps<{ tableFull: boolean; joinClosed: boolean }>()
</script>

<template>
  <p v-if="tableFull" class="dead-end">
    Table is full (12 seats) — no queue. Ask the host for a new session or
    <a href="/">browse open sessions</a>.
  </p>
  <p v-else-if="joinClosed" class="dead-end">
    This session is closed — read-only. <a href="/">Browse open sessions</a>.
  </p>
</template>
