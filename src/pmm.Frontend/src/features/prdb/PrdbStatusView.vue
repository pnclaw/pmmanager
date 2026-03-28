<template>
  <v-container>
    <v-row align="center" class="mb-4">
      <v-col class="d-flex align-center ga-2">
        <h1 class="text-h4">prdb Status</h1>
        <v-menu :close-on-content-click="false" max-width="360">
          <template #activator="{ props }">
            <v-btn icon="mdi-information-outline" variant="text" size="small" v-bind="props" />
          </template>
          <v-card>
            <v-card-title class="text-body-1 font-weight-medium pt-4 pb-1">Background Sync Service</v-card-title>
            <v-card-text class="text-body-2">
              <p class="mb-2">A background service runs automatically every <strong>15 minutes</strong> and performs the following in order:</p>
              <ol class="pl-4">
                <li class="mb-1"><strong>Actor summary backfill</strong> — pages through all actors on prdb.net and inserts any not yet in the local DB (5 000 per run until complete, then checks for new actors each tick).</li>
                <li class="mb-1"><strong>Video detail sync</strong> — fetches full detail for videos that haven't been processed yet, populating cast, images, and pre-names.</li>
                <li><strong>Actor detail backfill</strong> — batch-fetches full actor details (50 per API call, 1 000 per run) for all actors lacking detail.</li>
              </ol>
              <p class="mt-2 text-medium-emphasis">Individual steps can also be triggered manually using the Run Now buttons on each card.</p>
            </v-card-text>
          </v-card>
        </v-menu>
      </v-col>
      <v-col class="text-right d-flex align-center justify-end ga-3">
        <span v-if="status?.syncWorker" class="text-body-2 text-medium-emphasis">
          <template v-if="status.syncWorker.nextRunAt">
            Next run {{ nextRunLabel }}
          </template>
          <template v-else>
            Next run scheduled
          </template>
        </span>
        <v-btn
          prepend-icon="mdi-refresh"
          :loading="loading"
          @click="load"
        >
          Refresh
        </v-btn>
      </v-col>
    </v-row>

    <v-alert v-if="error" type="error" class="mb-4" closable @click:close="error = null">
      {{ error }}
    </v-alert>

    <v-row v-if="status">
      <!-- Actor Summary Backfill -->
      <v-col cols="12" md="6">
        <v-card>
          <v-card-title class="d-flex align-center ga-2">
            <v-icon>mdi-account-sync</v-icon>
            Actor Summary Backfill
            <v-spacer />
            <v-btn
              size="small"
              variant="tonal"
              prepend-icon="mdi-play"
              :loading="runningBackfill"
              @click="runBackfill"
            >
              Run Now
            </v-btn>
          </v-card-title>
          <v-card-text>
            <div v-if="status.actorBackfill.isComplete" class="d-flex align-center ga-2 mb-3">
              <v-icon color="success">mdi-check-circle</v-icon>
              <span class="text-success">Backfill complete</span>
            </div>
            <div v-else class="mb-3">
              <div class="d-flex justify-space-between mb-1">
                <span class="text-body-2">Progress</span>
                <span class="text-body-2">{{ backfillProgressLabel }}</span>
              </div>
              <v-progress-linear
                :model-value="backfillPercent"
                color="primary"
                height="8"
                rounded
              />
            </div>

            <v-table density="compact">
              <tbody>
                <tr>
                  <td class="text-medium-emphasis">Actors in DB</td>
                  <td>{{ status.actorBackfill.actorsInDb.toLocaleString() }}</td>
                </tr>
                <tr v-if="status.actorBackfill.totalActors">
                  <td class="text-medium-emphasis">Total on prdb</td>
                  <td>{{ status.actorBackfill.totalActors.toLocaleString() }}</td>
                </tr>
                <tr v-if="!status.actorBackfill.isComplete && status.actorBackfill.currentPage">
                  <td class="text-medium-emphasis">Next page</td>
                  <td>{{ status.actorBackfill.currentPage }}</td>
                </tr>
                <tr v-if="status.actorBackfill.lastSyncedAt">
                  <td class="text-medium-emphasis">Last synced</td>
                  <td>{{ formatDate(status.actorBackfill.lastSyncedAt) }}</td>
                </tr>
              </tbody>
            </v-table>
          </v-card-text>
        </v-card>
      </v-col>

      <!-- Actor Detail Sync -->
      <v-col cols="12" md="6">
        <v-card>
          <v-card-title class="d-flex align-center ga-2">
            <v-icon>mdi-account-details</v-icon>
            Actor Detail Sync
            <v-spacer />
            <v-btn
              size="small"
              variant="tonal"
              prepend-icon="mdi-play"
              :loading="runningVideoDetailSync"
              @click="runVideoDetailSync"
            >
              Run Now
            </v-btn>
          </v-card-title>
          <v-card-text>
            <div v-if="status.actorDetailSync.actorsPending === 0" class="d-flex align-center ga-2 mb-3">
              <v-icon color="success">mdi-check-circle</v-icon>
              <span class="text-success">All actors have full detail</span>
            </div>
            <div v-else class="mb-3">
              <div class="d-flex justify-space-between mb-1">
                <span class="text-body-2">Progress</span>
                <span class="text-body-2">{{ actorDetailProgressLabel }}</span>
              </div>
              <v-progress-linear
                :model-value="actorDetailPercent"
                color="primary"
                height="8"
                rounded
              />
            </div>

            <v-table density="compact">
              <tbody>
                <tr>
                  <td class="text-medium-emphasis">With detail</td>
                  <td>{{ status.actorDetailSync.actorsWithDetail.toLocaleString() }}</td>
                </tr>
                <tr>
                  <td class="text-medium-emphasis">Pending</td>
                  <td>{{ status.actorDetailSync.actorsPending.toLocaleString() }}</td>
                </tr>
                <tr>
                  <td class="text-medium-emphasis">Favourites</td>
                  <td>{{ status.actorDetailSync.favoriteActors.toLocaleString() }}</td>
                </tr>
              </tbody>
            </v-table>
          </v-card-text>
        </v-card>
      </v-col>

      <!-- Video Detail Sync -->
      <v-col cols="12" md="6">
        <v-card>
          <v-card-title class="d-flex align-center ga-2">
            <v-icon>mdi-video-check</v-icon>
            Video Detail Sync
            <v-spacer />
            <v-btn
              size="small"
              variant="tonal"
              prepend-icon="mdi-play"
              :loading="runningVideoDetailSync"
              @click="runVideoDetailSync"
            >
              Run Now
            </v-btn>
          </v-card-title>
          <v-card-text>
            <div v-if="status.videoDetailSync.videosPending === 0" class="d-flex align-center ga-2 mb-3">
              <v-icon color="success">mdi-check-circle</v-icon>
              <span class="text-success">All videos have full detail</span>
            </div>
            <div v-else class="mb-3">
              <div class="d-flex justify-space-between mb-1">
                <span class="text-body-2">Progress</span>
                <span class="text-body-2">{{ videoDetailProgressLabel }}</span>
              </div>
              <v-progress-linear
                :model-value="videoDetailPercent"
                color="primary"
                height="8"
                rounded
              />
            </div>

            <v-table density="compact">
              <tbody>
                <tr>
                  <td class="text-medium-emphasis">With detail</td>
                  <td>{{ status.videoDetailSync.videosWithDetail.toLocaleString() }}</td>
                </tr>
                <tr>
                  <td class="text-medium-emphasis">Pending</td>
                  <td>{{ status.videoDetailSync.videosPending.toLocaleString() }}</td>
                </tr>
                <tr>
                  <td class="text-medium-emphasis">With cast linked</td>
                  <td>{{ status.videoDetailSync.videosWithCast.toLocaleString() }}</td>
                </tr>
              </tbody>
            </v-table>
          </v-card-text>
        </v-card>
      </v-col>

      <!-- Library Counts -->
      <v-col cols="12" md="6">
        <v-card>
          <v-card-title class="d-flex align-center ga-2">
            <v-icon>mdi-database</v-icon>
            Library
          </v-card-title>
          <v-card-text>
            <v-table density="compact">
              <tbody>
                <tr>
                  <td class="text-medium-emphasis">Networks</td>
                  <td>{{ status.library.networks.toLocaleString() }}</td>
                </tr>
                <tr>
                  <td class="text-medium-emphasis">Sites</td>
                  <td>
                    {{ status.library.sites.toLocaleString() }}
                    <span class="text-medium-emphasis text-body-2">
                      ({{ status.library.favoriteSites.toLocaleString() }} favourite)
                    </span>
                  </td>
                </tr>
                <tr>
                  <td class="text-medium-emphasis">Videos</td>
                  <td>{{ status.library.videos.toLocaleString() }}</td>
                </tr>
                <tr>
                  <td class="text-medium-emphasis">Video images</td>
                  <td>{{ status.library.videoImages.toLocaleString() }}</td>
                </tr>
                <tr>
                  <td class="text-medium-emphasis">Actors</td>
                  <td>
                    {{ status.library.actors.toLocaleString() }}
                    <span class="text-medium-emphasis text-body-2">
                      ({{ status.library.favoriteActors.toLocaleString() }} favourite)
                    </span>
                  </td>
                </tr>
                <tr>
                  <td class="text-medium-emphasis">Actor images</td>
                  <td>{{ status.library.actorImages.toLocaleString() }}</td>
                </tr>
              </tbody>
            </v-table>
          </v-card-text>
        </v-card>
      </v-col>

      <!-- Rate Limits -->
      <v-col cols="12" md="6">
        <v-card>
          <v-card-title class="d-flex align-center ga-2">
            <v-icon>mdi-gauge</v-icon>
            Rate Limits
          </v-card-title>
          <v-card-text>
            <div v-if="!status.rateLimit" class="text-medium-emphasis">
              API key not configured or rate limit unavailable.
            </div>
            <template v-else>
              <div v-if="!status.rateLimit.isEnforced" class="d-flex align-center ga-2 mb-4">
                <v-icon color="info">mdi-information</v-icon>
                <span class="text-medium-emphasis">Rate limiting is not enforced for this key.</span>
              </div>

              <div class="mb-4">
                <div class="d-flex justify-space-between mb-1">
                  <span class="text-body-2 font-weight-medium">Hourly</span>
                  <span class="text-body-2">
                    {{ status.rateLimit.hourly.used }} / {{ status.rateLimit.hourly.limit }}
                    &nbsp;·&nbsp;
                    resets {{ formatResets(status.rateLimit.hourly.resetsInSeconds) }}
                  </span>
                </div>
                <v-progress-linear
                  :model-value="ratePct(status.rateLimit.hourly)"
                  :color="rateColor(status.rateLimit.hourly)"
                  height="8"
                  rounded
                />
              </div>

              <div>
                <div class="d-flex justify-space-between mb-1">
                  <span class="text-body-2 font-weight-medium">Monthly</span>
                  <span class="text-body-2">
                    {{ status.rateLimit.monthly.used }} / {{ status.rateLimit.monthly.limit }}
                    &nbsp;·&nbsp;
                    resets {{ formatResets(status.rateLimit.monthly.resetsInSeconds) }}
                  </span>
                </div>
                <v-progress-linear
                  :model-value="ratePct(status.rateLimit.monthly)"
                  :color="rateColor(status.rateLimit.monthly)"
                  height="8"
                  rounded
                />
              </div>
            </template>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>
  </v-container>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { api, type PrdbStatus } from '../../api'

const status               = ref<PrdbStatus | null>(null)
const loading              = ref(false)
const runningBackfill      = ref(false)
const runningVideoDetailSync = ref(false)
const error                = ref<string | null>(null)

// ── SyncWorker next run ────────────────────────────────────────────────────

const nextRunLabel = computed(() => {
  const next = status.value?.syncWorker?.nextRunAt
  if (!next) return ''
  const diffMs = new Date(next).getTime() - Date.now()
  if (diffMs <= 0) return 'imminently'
  const m = Math.ceil(diffMs / 60_000)
  return m === 1 ? 'in ~1 minute' : `in ~${m} minutes`
})

// ── Actor summary backfill ─────────────────────────────────────────────────

const backfillPercent = computed(() => {
  const bf = status.value?.actorBackfill
  if (!bf || bf.isComplete) return 100
  if (!bf.totalActors || !bf.currentPage) return 0
  return Math.min(((bf.currentPage - 1) * 500) / bf.totalActors * 100, 100)
})

const backfillProgressLabel = computed(() => {
  const bf = status.value?.actorBackfill
  if (!bf) return ''
  if (bf.totalActors) {
    const fetched = ((bf.currentPage ?? 1) - 1) * 500
    return `~${fetched.toLocaleString()} / ${bf.totalActors.toLocaleString()} (${backfillPercent.value.toFixed(1)}%)`
  }
  return `Page ${bf.currentPage}`
})

// ── Actor detail sync ──────────────────────────────────────────────────────

const actorDetailPercent = computed(() => {
  const s = status.value?.actorDetailSync
  if (!s || s.totalActors === 0) return 0
  return (s.actorsWithDetail / s.totalActors) * 100
})

const actorDetailProgressLabel = computed(() => {
  const s = status.value?.actorDetailSync
  if (!s) return ''
  return `${s.actorsWithDetail.toLocaleString()} / ${s.totalActors.toLocaleString()} (${actorDetailPercent.value.toFixed(1)}%)`
})

// ── Video detail sync ──────────────────────────────────────────────────────

const videoDetailPercent = computed(() => {
  const s = status.value?.videoDetailSync
  if (!s || s.totalVideos === 0) return 0
  return (s.videosWithDetail / s.totalVideos) * 100
})

const videoDetailProgressLabel = computed(() => {
  const s = status.value?.videoDetailSync
  if (!s) return ''
  return `${s.videosWithDetail.toLocaleString()} / ${s.totalVideos.toLocaleString()} (${videoDetailPercent.value.toFixed(1)}%)`
})

// ── Rate limits ────────────────────────────────────────────────────────────

function ratePct(w: { used: number; limit: number }) {
  return w.limit > 0 ? (w.used / w.limit) * 100 : 0
}

function rateColor(w: { used: number; limit: number }) {
  const pct = ratePct(w)
  if (pct >= 90) return 'error'
  if (pct >= 70) return 'warning'
  return 'success'
}

function formatResets(seconds: number) {
  if (seconds < 60) return `${seconds}s`
  const h = Math.floor(seconds / 3600)
  const m = Math.floor((seconds % 3600) / 60)
  if (h > 0) return `in ${h}h ${m}m`
  return `in ${m}m`
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleString()
}

// ── Actions ────────────────────────────────────────────────────────────────

async function runBackfill() {
  runningBackfill.value = true
  error.value = null
  try {
    await api.prdbStatus.runBackfill()
    await load()
  } catch (e: any) {
    error.value = e.message
  } finally {
    runningBackfill.value = false
  }
}

async function runVideoDetailSync() {
  runningVideoDetailSync.value = true
  error.value = null
  try {
    await api.prdbStatus.runVideoDetailSync()
    await load()
  } catch (e: any) {
    error.value = e.message
  } finally {
    runningVideoDetailSync.value = false
  }
}

async function load() {
  loading.value = true
  error.value = null
  try {
    status.value = await api.prdbStatus.get()
  } catch (e: any) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>
