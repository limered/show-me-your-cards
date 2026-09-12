<script setup lang="ts">
defineProps<{ countdown: string }>()
</script>

<template>
  <span class="timer" title="Round timer">{{ countdown }}</span>
</template>
