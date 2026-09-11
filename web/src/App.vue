<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import {
  createSession,
  fetchSessions,
  fetchSnapshot,
  joinSession,
  newRound,
  playCard,
  renamePlayer,
  reveal,
  sessionLink,
  tokenKey,
  type SessionSummary,
  type Snapshot,
} from './api'

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
let poll: number | undefined

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
    error.value = ''
  } catch (e) {
    error.value = String(e)
  }
}

async function join() {
  if (!roomId) return
  error.value = ''
  try {
    const j = await joinSession(roomId, name.value.trim() || undefined)
    token.value = j.token
    localStorage.setItem(tokenKey(roomId), j.token)
    await refresh()
  } catch (e) {
    error.value = String(e)
  }
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

onMounted(async () => {
  if (roomId) {
    await refresh()
    poll = window.setInterval(refresh, 2000)
  } else {
    await loadSessions()
  }
})

onUnmounted(() => {
  if (poll !== undefined) window.clearInterval(poll)
})
</script>

<template>
  <main>
    <div v-if="!roomId">
      <h1>Show me your cards</h1>
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
            <option value="30s">30s</option>
            <option value="1m">1m</option>
            <option value="2m">2m</option>
            <option value="5m">5m</option>
            <option value="off">Off</option>
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
      <header>
        <span>{{ link }}</span>
        <button @click="copyLink">{{ copied ? 'Copied!' : 'Copy link' }}</button>
      </header>
      <div v-if="!token">
        <label>Display name (optional)
          <input v-model="name" placeholder="auto-assigned if blank" />
        </label>
        <button @click="join">Join table</button>
      </div>
      <template v-else>
        <div class="controls">
          <label>Name
            <input v-model="name" placeholder="rename" @keyup.enter="rename" />
          </label>
          <button @click="rename">Rename</button>
          <button v-if="!snapshot?.revealed" @click="doReveal">Reveal</button>
          <button v-else @click="doNewRound">New round</button>
        </div>

        <ol class="seats">
          <li
            v-for="spot in TABLE_SIZE"
            :key="spot"
            :class="{ glow: snapshot?.result?.spots.includes(spot - 1) }"
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

        <div class="hand">
          <button
            v-for="c in hand"
            :key="c"
            :class="{ selected: snapshot?.youCard === c }"
            @click="play(c)"
          >
            {{ c }}
          </button>
        </div>
      </template>
      <p v-if="error">{{ error }}</p>
    </div>
  </main>
</template>

<style scoped>
.seats {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 0.5rem;
  list-style: none;
  padding: 0;
}
.seats li {
  border: 1px solid #ccc;
  border-radius: 0.5rem;
  padding: 0.5rem;
}
.glow {
  box-shadow: 0 0 12px 2px gold;
  border-color: gold;
}
.card {
  font-weight: bold;
  margin-right: 0.25rem;
}
.hand {
  display: flex;
  gap: 0.25rem;
  flex-wrap: wrap;
  margin-top: 1rem;
}
.hand button.selected {
  background: gold;
  transform: translateY(-0.5rem);
}
.dial {
  text-align: center;
  margin: 1rem 0;
}
.dial .result {
  font-size: 3rem;
  font-weight: bold;
  text-shadow: 0 0 12px gold;
}
.dial .mean {
  opacity: 0.6;
}
</style>
