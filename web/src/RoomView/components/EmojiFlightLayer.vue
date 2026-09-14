<script setup lang="ts">
import { onUnmounted, ref, watch } from 'vue'
import type { Flight } from '../composables/emojiThrows'
import { FLIGHT_DURATION_MS, bezierPoint, flightControl, type Point } from '../models/emoji'

const props = defineProps<{
  flights: Flight[]
}>()

const emit = defineEmits<{
  (e: 'done', id: number): void
}>()

interface Placed extends Flight {
  x: number
  y: number
  rotation: number
  scale: number
}

interface Anchors {
  from: Point
  control: Point
  to: Point
}

const placed = ref<Placed[]>([])
const anchors = new Map<number, Anchors>()
let raf = 0

function seatCenter(spot: number): Point | null {
  const el = document.querySelector(`[data-seat="${spot}"]`)
  if (!el) return null
  const rect = el.getBoundingClientRect()
  return { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 }
}

function anchorPoints(flight: Flight): Anchors {
  const cached = anchors.get(flight.id)
  if (cached) return cached
  const fallback: Point = { x: window.innerWidth / 2, y: window.innerHeight / 2 }
  const from = seatCenter(flight.fromSpot) ?? fallback
  const to = seatCenter(flight.toSpot) ?? fallback
  const entry: Anchors = { from, control: flightControl(from, to), to }
  anchors.set(flight.id, entry)
  return entry
}

function settle(flight: Flight): void {
  anchors.delete(flight.id)
  emit('done', flight.id)
}

function step(now: number): void {
  const next: Placed[] = []
  for (const flight of props.flights) {
    const t = Math.min(1, (now - flight.startedAt) / FLIGHT_DURATION_MS)
    if (t >= 1) {
      settle(flight)
      continue
    }
    const { from, control, to } = anchorPoints(flight)
    const at = bezierPoint(from, control, to, t)
    next.push({ ...flight, x: at.x, y: at.y, rotation: t * 360, scale: 0.6 + 0.9 * Math.sin(Math.PI * t) })
  }
  placed.value = next
  raf = props.flights.length > 0 ? requestAnimationFrame(step) : 0
}

watch(
  () => props.flights,
  (flights) => {
    if (flights.length === 0) {
      placed.value = []
      return
    }
    if (typeof requestAnimationFrame !== 'function') {
      for (const flight of flights) settle(flight)
      return
    }
    if (!raf) raf = requestAnimationFrame(step)
  },
  { immediate: true },
)

onUnmounted(() => {
  if (raf) cancelAnimationFrame(raf)
})
</script>

<template>
  <div class="flight-layer" aria-hidden="true">
    <span
      v-for="flight in placed"
      :key="flight.id"
      class="flight"
      :style="{
        transform: `translate(${flight.x}px, ${flight.y}px) rotate(${flight.rotation}deg) scale(${flight.scale})`,
      }"
    >
      {{ flight.emoji }}
    </span>
  </div>
</template>

<style scoped>
.flight-layer {
  position: fixed;
  inset: 0;
  pointer-events: none;
  z-index: 50;
  overflow: hidden;
}
.flight {
  position: absolute;
  left: 0;
  top: 0;
  font-size: 2rem;
  line-height: 1;
  text-shadow: 0 0 12px var(--neon-glow);
  will-change: transform;
}
</style>
