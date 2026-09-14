<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import {
  closeSession,
  configureSession,
  createSession,
  fetchSessions,
  fetchSnapshot,
  formatCountdown,
  joinSession,
  newRound,
  playCard,
  remainingSeconds,
  renamePlayer,
  reveal,
  sessionLink,
  tokenKey,
  type SessionSummary,
  type Snapshot,
} from './api'
import ClosedBanner from './RoomView/components/ClosedBanner.vue'
import EmojiFlightLayer from './RoomView/components/EmojiFlightLayer.vue'
import EmojiRadial from './RoomView/components/EmojiRadial.vue'
import JoinDeadEnd from './RoomView/components/JoinDeadEnd.vue'
import PlayerHand from './RoomView/components/PlayerHand.vue'
import RoomTimer from './RoomView/components/RoomTimer.vue'
import SessionConfigForm from './RoomView/components/SessionConfigForm.vue'
import { useEmojiThrows } from './RoomView/composables/emojiThrows'
import type { ThrowEmoji } from './RoomView/models/emoji'
import { loadTheme, saveTheme, themeAttribute, toggleMode, type ThemeMode } from './RoomView/models/theme'
import { TIMER_OPTIONS } from './RoomView/models/timers'
import {
  applyRoomConfig,
  classifyJoinError,
  closeRoom,
  isNewRound,
  shouldFireAutoReveal,
} from './RoomView/composables/roomLogic'

const TABLE_SIZE = 12
const path = window.location.pathname
const roomId = path.startsWith('/s/') ? path.slice(3) : null

const sessions = ref<SessionSummary[]>([])
const deck = ref('fib')
const customDeck = ref('')
const timer = ref('2m')
const creating = ref(false)
const error = ref('')

const snapshot = ref<Snapshot | null>(null)
const link = window.location.href
const name = ref('')
const token = ref<string | null>(roomId ? localStorage.getItem(tokenKey(roomId)) : null)
const copied = ref(false)
const nowMs = ref(Date.now())
const tableFull = ref(false)
const joinClosed = ref(false)
const configDeck = ref('fib')
const configCustom = ref('')
const configTimer = ref('2m')
const applyingConfig = ref(false)
const throws = useEmojiThrows()
const theme = ref<ThemeMode>('dark')
let poll: number | undefined
let tick: number | undefined
let lastFiredDeadlineUtc: string | null | undefined
let seenRound: number | null = null

async function loadSessions() {
  sessions.value = await fetchSessions().catch(() => [])
}

async function create() {
  creating.value = true
  error.value = ''
  try {
    const s = await createSession(deck.value === 'custom' ? customDeck.value : deck.value, timer.value)
    window.location.href = sessionLink(s.id)
  } catch (e) {
    error.value = String(e)
  } finally {
    creating.value = false
  }
}

async function refresh() {
  if (!roomId) return
  try {
    snapshot.value = await fetchSnapshot(roomId, token.value)
    if (isNewRound(seenRound, snapshot.value)) lastFiredDeadlineUtc = undefined
    seenRound = snapshot.value.round
    error.value = ''
  } catch (e) {
    error.value = String(e)
  }
}

async function join() {
  if (!roomId) return
  error.value = ''
  tableFull.value = false
  joinClosed.value = false
  try {
    const j = await joinSession(roomId, name.value.trim() || undefined, token.value)
    token.value = j.token
    localStorage.setItem(tokenKey(roomId), j.token)
    await refresh()
  } catch (e) {
    markJoinDeadEnd(String(e))
  }
}

function markJoinDeadEnd(message: string) {
  const kind = classifyJoinError(message)
  if (kind === 'table_full') tableFull.value = true
  else if (kind === 'closed') joinClosed.value = true
  else error.value = message
}

async function copyLink() {
  await navigator.clipboard.writeText(window.location.href)
  copied.value = true
  setTimeout(() => (copied.value = false), 1500)
}

function seatAt(spot: number) {
  return snapshot.value?.players.find((p) => p.spot === spot)
}

const hand = computed(() => snapshot.value?.deck.split(',').map((c) => c.trim()).filter(Boolean) ?? [])
const ownName = computed(
  () => snapshot.value?.players.find((p) => p.spot === snapshot.value?.youSpot)?.name ?? '',
)

function applyTheme() {
  document.documentElement.dataset.theme = themeAttribute(theme.value)
}

function toggleTheme() {
  theme.value = toggleMode(theme.value)
  saveTheme(localStorage, theme.value)
  applyTheme()
}

function onSeatClick(event: MouseEvent, spot: number) {
  if (!snapshot.value || snapshot.value.closed) return
  if (!seatAt(spot)) return
  throws.openRadial(event.clientX, event.clientY, spot, snapshot.value.youSpot)
}

function onPickEmoji(emoji: ThrowEmoji) {
  const from = snapshot.value?.youSpot
  if (from === null || from === undefined) {
    throws.closeRadial()
    return
  }
  throws.pickEmoji(emoji, from)
}

async function play(card: string) {
  if (!roomId || !token.value) return
  await playCard(roomId, token.value, card)
  await refresh()
}

async function rename() {
  if (!roomId || !token.value) return
  await renamePlayer(roomId, token.value, name.value.trim())
  await refresh()
}

async function doReveal() {
  if (roomId) {
    await reveal(roomId)
    await refresh()
  }
}

async function doNewRound() {
  if (roomId) {
    await newRound(roomId)
    await refresh()
  }
}

async function applyConfig() {
  if (!roomId) return
  applyingConfig.value = true
  error.value = ''
  try {
    const next = await applyRoomConfig(
      { configureSession, fetchSnapshot },
      roomId,
      token.value,
      configDeck.value,
      configCustom.value,
      configTimer.value,
    )
    if (next) {
      if (isNewRound(seenRound, next)) lastFiredDeadlineUtc = undefined
      seenRound = next.round
      snapshot.value = next
    }
  } catch (e) {
    error.value = String(e)
  } finally {
    applyingConfig.value = false
  }
}

async function doClose() {
  if (!roomId) return
  const next = await closeRoom({ closeSession, fetchSnapshot }, roomId, token.value)
  if (next) {
    if (isNewRound(seenRound, next)) lastFiredDeadlineUtc = undefined
    seenRound = next.round
    snapshot.value = next
  }
}

const remaining = computed(() => remainingSeconds(snapshot.value?.deadlineUtc ?? null, nowMs.value))
const countdown = computed(() => formatCountdown(remaining.value))

function fireAutoReveal() {
  const snap = snapshot.value
  if (!shouldFireAutoReveal(snap, remaining.value, lastFiredDeadlineUtc)) return
  lastFiredDeadlineUtc = snap!.deadlineUtc
  void refresh()
}

onMounted(async () => {
  theme.value = loadTheme(localStorage)
  applyTheme()
  if (roomId) {
    await refresh()
    poll = window.setInterval(refresh, 2000)
    tick = window.setInterval(onTick, 1000)
  } else {
    await loadSessions()
  }
})

function onTick() {
  nowMs.value = Date.now()
  fireAutoReveal()
}

onUnmounted(() => {
  if (poll !== undefined) window.clearInterval(poll)
  if (tick !== undefined) window.clearInterval(tick)
})
</script>

<template>
  <main>
    <div v-if="!roomId">
      <header class="room-bar">
        <h1>Show me your cards</h1>
        <button class="theme-toggle" @click="toggleTheme" :title="`Switch to ${theme === 'dark' ? 'light' : 'dark'} mode`">
          {{ theme === 'dark' ? '☀️ Light' : '🌙 Dark' }}
        </button>
      </header>
      <section>
        <h2>New session</h2>
        <label>Deck
          <select v-model="deck">
            <option value="fib">Fibonacci</option>
            <option value="inc">Incremental</option>
            <option value="custom">Custom</option>
          </select>
        </label>
        <label v-if="deck === 'custom'">Cards
          <input v-model="customDeck" placeholder="1,2,4,8,16" />
        </label>
        <label>Timer
          <select v-model="timer">
            <option v-for="t in TIMER_OPTIONS" :key="t" :value="t">{{ t }}</option>
          </select>
        </label>
        <button :disabled="creating" @click="create">Create session</button>
      </section>
      <section>
        <h2>Open sessions</h2>
        <ul>
          <li v-for="s in sessions" :key="s.id">
            <a :href="sessionLink(s.id)">{{ s.id }}</a> ({{ s.players }} seated)
          </li>
        </ul>
      </section>
      <p v-if="error">{{ error }}</p>
    </div>

    <div v-else>
      <header class="room-bar">
        <div class="me">
          <span class="me-label">You</span>
          <strong class="me-name">{{ ownName || '…' }}</strong>
          <input v-model="name" placeholder="rename" @keyup.enter="rename" aria-label="Rename yourself" />
          <button @click="rename">Rename</button>
        </div>
        <div class="link">
          <span>{{ link }}</span>
          <button @click="copyLink">{{ copied ? 'Copied!' : 'Copy link' }}</button>
        </div>
        <div class="bar-right">
          <RoomTimer v-if="snapshot && remaining !== null" :countdown="countdown" />
          <button class="theme-toggle" @click="toggleTheme" :title="`Switch to ${theme === 'dark' ? 'light' : 'dark'} mode`">
            {{ theme === 'dark' ? '☀️ Light' : '🌙 Dark' }}
          </button>
        </div>
      </header>
      <ClosedBanner :closed="snapshot?.closed === true" />
      <div v-if="!token">
        <JoinDeadEnd :table-full="tableFull" :join-closed="joinClosed" />
        <template v-if="!tableFull && !joinClosed">
          <label>Display name (optional)
            <input v-model="name" placeholder="auto-assigned if blank" />
          </label>
          <button @click="join">Join table</button>
        </template>
      </div>
      <template v-else>
        <div v-if="!snapshot?.closed" class="controls">
          <button v-if="!snapshot?.revealed" @click="doReveal">Reveal</button>
          <button v-else @click="doNewRound">New round</button>
          <button @click="doClose" title="Close session for everyone">Close</button>
        </div>
        <SessionConfigForm
          v-if="!snapshot?.closed"
          :config-deck="configDeck"
          :config-custom="configCustom"
          :config-timer="configTimer"
          :applying-config="applyingConfig"
          @update:config-deck="configDeck = $event"
          @update:config-custom="configCustom = $event"
          @update:config-timer="configTimer = $event"
          @apply="applyConfig"
        />

        <ol class="seats">
          <li
            v-for="spot in TABLE_SIZE"
            :key="spot"
            :data-seat="spot - 1"
            :class="{ glow: snapshot?.result?.spots.includes(spot - 1) }"
            @click="onSeatClick($event, spot - 1)"
          >
            <template v-if="seatAt(spot - 1)">
              <span class="card">
                <template v-if="snapshot?.revealed">{{ seatAt(spot - 1)!.card ?? '—' }}</template>
                <template v-else>{{ seatAt(spot - 1)!.played ? '🂠' : '·' }}</template>
              </span>
              {{ seatAt(spot - 1)!.name }}
            </template>
            <template v-else>· empty seat</template>
          </li>
        </ol>

        <div v-if="snapshot?.revealed && snapshot?.result" class="dial">
          <div class="result">{{ snapshot.result.card }}</div>
          <div class="mean">{{ snapshot.result.mean.toFixed(1) }}</div>
        </div>

        <PlayerHand
          :closed="snapshot?.closed === true"
          :hand="hand"
          :you-card="snapshot?.youCard"
          @play="play"
        />
        <EmojiRadial
          v-if="throws.radial.value"
          :x="throws.radial.value.x"
          :y="throws.radial.value.y"
          @pick="onPickEmoji"
          @close="throws.closeRadial()"
        />
        <EmojiFlightLayer :flights="throws.flights.value" @done="throws.finishFlight" />
      </template>
      <p v-if="error">{{ error }}</p>
    </div>
  </main>
</template>

<style>
:root,
[data-theme='dreamy-neon-dark'] {
  --bg: #14101f;
  --bg-soft: #1e1633;
  --surface: #221a3d;
  --ink: #f3ecff;
  --muted: #b9a8e6;
  --neon: #c084fc;
  --neon-hot: #f472b6;
  --neon-glow: rgba(192, 132, 252, 0.55);
  --gold: #ffd75e;
  --line: #4c3d7a;
}
[data-theme='dreamy-neon-light'] {
  --bg: #fdf4ff;
  --bg-soft: #fae8ff;
  --surface: #ffffff;
  --ink: #3b0764;
  --muted: #7e22ce;
  --neon: #a21caf;
  --neon-hot: #db2777;
  --neon-glow: rgba(219, 39, 119, 0.25);
  --gold: #b45309;
  --line: #e9d5ff;
}
html {
  background: var(--bg);
}
body {
  margin: 0;
  background:
    radial-gradient(60rem 30rem at 15% -5%, rgba(192, 132, 252, 0.25), transparent),
    radial-gradient(50rem 26rem at 90% 0%, rgba(244, 114, 182, 0.2), transparent),
    var(--bg);
  color: var(--ink);
  font-family: ui-rounded, 'SF Pro Rounded', system-ui, sans-serif;
}
a {
  color: var(--neon-hot);
}
button {
  background: var(--surface);
  color: var(--ink);
  border: 1px solid var(--line);
  border-radius: 0.5rem;
  padding: 0.3rem 0.7rem;
  cursor: pointer;
}
button:hover {
  border-color: var(--neon);
  box-shadow: 0 0 10px var(--neon-glow);
}
input,
select {
  background: var(--bg-soft);
  color: var(--ink);
  border: 1px solid var(--line);
  border-radius: 0.5rem;
  padding: 0.3rem 0.5rem;
}
</style>

<style scoped>
main {
  max-width: 60rem;
  margin: 0 auto;
  padding: 1rem;
  min-height: 100vh;
}
.room-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
  flex-wrap: wrap;
  padding: 0.6rem 0.9rem;
  margin-bottom: 1rem;
  border: 1px solid var(--line);
  border-radius: 1rem;
  background: color-mix(in srgb, var(--surface) 82%, transparent);
  box-shadow: 0 0 22px var(--neon-glow);
}
.me {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}
.me-label {
  color: var(--muted);
  font-size: 0.8rem;
  text-transform: uppercase;
  letter-spacing: 0.08em;
}
.me-name {
  text-shadow: 0 0 12px var(--neon-glow);
}
.link {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  color: var(--muted);
  font-size: 0.85rem;
  overflow-wrap: anywhere;
}
.bar-right {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}
.theme-toggle {
  white-space: nowrap;
}
.controls {
  display: flex;
  gap: 0.5rem;
  align-items: center;
  margin: 0.75rem 0;
  flex-wrap: wrap;
}
.seats {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 0.5rem;
  list-style: none;
  padding: 0;
}
.seats li {
  border: 1px solid var(--line);
  background: color-mix(in srgb, var(--surface) 70%, transparent);
  border-radius: 0.75rem;
  padding: 0.5rem;
  cursor: pointer;
  transition: box-shadow 0.15s ease, border-color 0.15s ease;
}
.seats li:hover {
  border-color: var(--neon);
  box-shadow: 0 0 14px var(--neon-glow);
}
.glow {
  box-shadow: 0 0 12px 2px var(--gold);
  border-color: var(--gold);
}
.card {
  font-weight: bold;
  margin-right: 0.25rem;
}
.timer {
  font-variant-numeric: tabular-nums;
  border: 1px solid var(--neon);
  border-radius: 0.5rem;
  padding: 0.25rem 0.5rem;
  margin-left: 0.5rem;
  text-shadow: 0 0 10px var(--neon-glow);
}
.banner {
  border: 1px solid var(--line);
  border-radius: 0.75rem;
  padding: 0.5rem;
  background: var(--surface);
}
.dead-end {
  border: 1px solid var(--line);
  border-radius: 0.75rem;
  padding: 0.5rem;
  background: var(--surface);
}
.hand {
  display: flex;
  gap: 0.25rem;
  flex-wrap: wrap;
  margin-top: 1rem;
}
.hand button.selected {
  background: var(--gold);
  color: #221a00;
  transform: translateY(-0.5rem);
  box-shadow: 0 0 14px var(--gold);
}
.dial {
  text-align: center;
  margin: 1rem 0;
}
.dial .result {
  font-size: 3rem;
  font-weight: bold;
  text-shadow: 0 0 18px var(--neon-hot), 0 0 34px var(--neon-glow);
}
.dial .mean {
  opacity: 0.6;
}
</style>
