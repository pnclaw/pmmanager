<template>
  <v-container>
    <v-row align="center" class="mb-4">
      <v-col>
        <h1 class="text-h4">Downloads</h1>
      </v-col>
    </v-row>

    <v-alert v-if="error" type="error" class="mb-4" closable @click:close="error = null">
      {{ error }}
    </v-alert>

    <v-row class="mb-2" align="center">
      <v-col cols="12" sm="5" md="4">
        <v-text-field
          v-model="search"
          prepend-inner-icon="mdi-magnify"
          label="Search"
          clearable
          hide-details
        />
      </v-col>
      <v-col cols="12" sm="4" md="3">
        <v-select
          v-model="statusFilter"
          :items="statusOptions"
          label="Status"
          hide-details
        />
      </v-col>
      <v-col cols="auto">
        <v-switch
          v-model="activeOnly"
          label="Active only"
          color="primary"
          hide-details
        />
      </v-col>
    </v-row>

    <v-data-table
      :headers="headers"
      :items="filteredLogs"
      :loading="loading"
      item-value="id"
      hover
      @click:row="openDetail"
    >
      <template #item.status="{ item }">
        <v-chip :color="statusColor(item.status)" size="small" variant="tonal">
          {{ statusLabel(item.status) }}
        </v-chip>
      </template>

      <template #item.progress="{ item }">
        <template v-if="item.status === DownloadStatus.Downloading || item.status === DownloadStatus.PostProcessing">
          <v-progress-linear
            :model-value="progressPct(item)"
            color="primary"
            height="6"
            rounded
            class="mb-1"
            style="min-width: 120px"
          />
          <div class="text-caption text-medium-emphasis">
            {{ formatBytes(item.downloadedBytes) }} / {{ formatBytes(item.totalSizeBytes) }}
          </div>
        </template>
        <span v-else-if="item.totalSizeBytes" class="text-caption text-medium-emphasis">
          {{ formatBytes(item.totalSizeBytes) }}
        </span>
        <span v-else class="text-medium-emphasis">—</span>
      </template>

      <template #item.createdAt="{ item }">
        <span class="text-caption">{{ formatDate(item.createdAt) }}</span>
      </template>

      <template #item.completedAt="{ item }">
        <span v-if="item.completedAt" class="text-caption">{{ formatDate(item.completedAt) }}</span>
        <span v-else class="text-medium-emphasis">—</span>
      </template>
    </v-data-table>

    <!-- Detail dialog -->
    <v-dialog v-model="detailOpen" max-width="560">
      <v-card v-if="detailItem">
        <v-card-title class="pt-4 d-flex align-center justify-space-between">
          <span class="text-truncate mr-2">{{ detailItem.nzbName }}</span>
          <v-chip :color="statusColor(detailItem.status)" size="small" variant="tonal" class="flex-shrink-0">
            {{ statusLabel(detailItem.status) }}
          </v-chip>
        </v-card-title>
        <v-card-subtitle>{{ detailItem.downloadClientTitle }}</v-card-subtitle>

        <v-card-text>
          <v-list density="compact" lines="one">
            <v-list-item v-if="detailItem.clientItemId" title="Client ID" :subtitle="detailItem.clientItemId" />

            <template v-if="detailItem.status === DownloadStatus.Downloading || detailItem.status === DownloadStatus.PostProcessing">
              <v-list-item title="Progress">
                <template #subtitle>
                  <v-progress-linear
                    :model-value="progressPct(detailItem)"
                    color="primary"
                    height="6"
                    rounded
                    class="mt-1 mb-1"
                  />
                  <span class="text-caption">
                    {{ formatBytes(detailItem.downloadedBytes) }} / {{ formatBytes(detailItem.totalSizeBytes) }}
                  </span>
                </template>
              </v-list-item>
            </template>
            <v-list-item v-else-if="detailItem.totalSizeBytes" title="Size" :subtitle="formatBytes(detailItem.totalSizeBytes)" />

            <v-list-item v-if="detailItem.storagePath" title="Storage Path" :subtitle="detailItem.storagePath" />

            <v-list-item v-if="detailItem.errorMessage" title="Error">
              <template #subtitle>
                <span class="text-error">{{ detailItem.errorMessage }}</span>
              </template>
            </v-list-item>

            <v-list-item title="Started" :subtitle="formatDateTime(detailItem.createdAt)" />
            <v-list-item v-if="detailItem.completedAt" title="Completed" :subtitle="formatDateTime(detailItem.completedAt)" />
            <v-list-item v-if="detailItem.lastPolledAt" title="Last polled" :subtitle="formatDateTime(detailItem.lastPolledAt)" />
          </v-list>

          <template v-if="detailItem.fileNames?.length">
            <div class="text-subtitle-2 mt-3 mb-1">Extracted Files</div>
            <v-list density="compact" class="bg-surface-variant rounded">
              <v-list-item
                v-for="file in detailItem.fileNames"
                :key="file"
                :title="file"
                prepend-icon="mdi-file-outline"
                density="compact"
              />
            </v-list>
          </template>
        </v-card-text>

        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="detailOpen = false">Close</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-container>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { api, DownloadStatus, DownloadStatusLabels, type DownloadLog } from '../../api'

const logs    = ref<DownloadLog[]>([])
const loading = ref(false)
const error   = ref<string | null>(null)

const search       = ref('')
const statusFilter = ref<number | 'all'>('all')
const activeOnly   = ref(false)

const detailOpen = ref(false)
const detailItem = ref<DownloadLog | null>(null)

let pollTimer: ReturnType<typeof setInterval> | null = null

const statusOptions = [
  { title: 'All',              value: 'all' as const },
  { title: 'Queued',           value: DownloadStatus.Queued },
  { title: 'Downloading',      value: DownloadStatus.Downloading },
  { title: 'Post-processing',  value: DownloadStatus.PostProcessing },
  { title: 'Completed',        value: DownloadStatus.Completed },
  { title: 'Failed',           value: DownloadStatus.Failed },
]

const headers = [
  { title: 'Name',      key: 'nzbName',          sortable: true },
  { title: 'Client',    key: 'downloadClientTitle', sortable: true, width: '150px' },
  { title: 'Status',    key: 'status',           sortable: true,  width: '150px' },
  { title: 'Progress',  key: 'progress',         sortable: false, width: '180px' },
  { title: 'Started',   key: 'createdAt',        sortable: true,  width: '130px' },
  { title: 'Completed', key: 'completedAt',      sortable: true,  width: '130px' },
]

const terminalStatuses = new Set([DownloadStatus.Completed, DownloadStatus.Failed])

const filteredLogs = computed(() => {
  return logs.value.filter(log => {
    if (activeOnly.value && terminalStatuses.has(log.status)) return false
    if (statusFilter.value !== 'all' && log.status !== statusFilter.value) return false
    if (search.value) {
      const q = search.value.toLowerCase()
      if (!log.nzbName.toLowerCase().includes(q) &&
          !log.downloadClientTitle.toLowerCase().includes(q)) return false
    }
    return true
  })
})

async function load() {
  loading.value = true
  error.value = null
  try {
    logs.value = await api.downloadLogs.list()
  } catch (e: any) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

function openDetail(_: MouseEvent, { item }: { item: DownloadLog }) {
  detailItem.value = item
  detailOpen.value = true
}

function statusLabel(status: DownloadStatus): string {
  return DownloadStatusLabels[status] ?? String(status)
}

function statusColor(status: DownloadStatus): string {
  switch (status) {
    case DownloadStatus.Queued:         return 'default'
    case DownloadStatus.Downloading:    return 'info'
    case DownloadStatus.PostProcessing: return 'warning'
    case DownloadStatus.Completed:      return 'success'
    case DownloadStatus.Failed:         return 'error'
    default:                            return 'default'
  }
}

function progressPct(log: DownloadLog): number {
  if (!log.totalSizeBytes || !log.downloadedBytes) return 0
  return Math.min(100, Math.round((log.downloadedBytes / log.totalSizeBytes) * 100))
}

function formatBytes(bytes: number | null): string {
  if (bytes == null) return '—'
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`
  return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} GB`
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString()
}

onMounted(() => {
  load()
  pollTimer = setInterval(load, 20_000)
})

onUnmounted(() => {
  if (pollTimer !== null) clearInterval(pollTimer)
})
</script>
