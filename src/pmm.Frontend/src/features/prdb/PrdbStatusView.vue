<template>
  <v-container>
    <v-row align="center" class="mb-4">
      <v-col>
        <h1 class="text-h4">prdb Status</h1>
      </v-col>
      <v-col class="text-right">
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
      <!-- Actor Backfill -->
      <v-col cols="12" md="6">
        <v-card>
          <v-card-title class="d-flex align-center ga-2">
            <v-icon>mdi-account-sync</v-icon>
            Actor Backfill
          </v-card-title>
          <v-card-text>
            <div v-if="status.actorBackfill.isComplete" class="d-flex align-center ga-2 mb-3">
              <v-icon color="success">mdi-check-circle</v-icon>
              <span class="text-success">Backfill complete</span>
            </div>
            <div v-else class="mb-3">
              <div class="d-flex justify-space-between mb-1">
                <span class="text-body-2">Progress</span>
                <span class="text-body-2">
                  {{ backfillProgressLabel }}
                </span>
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

const status  = ref<PrdbStatus | null>(null)
const loading = ref(false)
const error   = ref<string | null>(null)

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
