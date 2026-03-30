<template>
  <v-container>
    <v-row>
      <v-col cols="12" md="6">
        <v-card :loading="loading">
          <v-card-title>API Status</v-card-title>
          <v-card-text>
            <div v-if="health" class="d-flex align-center ga-4">
              <v-chip
                :color="health.status === 'ok' ? 'success' : 'error'"
                size="large"
                :prepend-icon="health.status === 'ok' ? 'mdi-check-circle' : 'mdi-alert-circle'"
              >
                {{ health.status.toUpperCase() }}
              </v-chip>
              <span class="text-body-2 text-medium-emphasis">
                Last checked: {{ formatDate(health.timestamp) }}
              </span>
            </div>

            <v-skeleton-loader v-else-if="loading" type="chip" />

            <v-alert v-if="error" type="error" class="mt-4">
              {{ error }}
            </v-alert>
          </v-card-text>

        </v-card>
      </v-col>
    </v-row>
  </v-container>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { api, type HealthResponse } from '../../api'
import { usePageAction } from '../../composables/usePageAction'

const health = ref<HealthResponse | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)

async function fetchHealth() {
  loading.value = true
  error.value = null
  try {
    health.value = await api.health.get()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to fetch health status'
  } finally {
    loading.value = false
  }
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleString()
}

const { setAction, clearAction } = usePageAction()

onMounted(() => {
  fetchHealth()
  setAction('mdi-refresh', 'Refresh', fetchHealth)
})

onUnmounted(clearAction)
</script>
