<script setup lang="ts">
defineProps<{ closed: boolean }>()
</script>

<template>
  <p v-if="closed" class="banner">This session is closed — read-only. History stays visible via this link.</p>
</template>
