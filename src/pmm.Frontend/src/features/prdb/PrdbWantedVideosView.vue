<template>
  <v-container>
    <v-row align="center" class="mb-4">
      <v-col>
        <h1 class="text-h4">Wanted</h1>
      </v-col>
    </v-row>

    <v-alert v-if="error" type="error" class="mb-4" closable @click:close="error = null">
      {{ error }}
    </v-alert>

    <v-row class="mb-4">
      <v-col cols="12" sm="6" md="3">
        <v-text-field
          v-model="search"
          prepend-inner-icon="mdi-magnify"
          label="Search"
          clearable
          hide-details
          @update:model-value="onFilterChange"
        />
      </v-col>
      <v-col cols="12" sm="6" md="2">
        <v-select
          v-model="statusFilter"
          :items="statusOptions"
          label="Status"
          hide-details
          @update:model-value="onFilterChange"
        />
      </v-col>
      <v-col cols="12" sm="6" md="3">
        <v-autocomplete
          v-model="selectedSiteId"
          :items="filterOptions.sites"
          item-value="id"
          item-title="title"
          label="Site"
          clearable
          hide-details
          :loading="loadingOptions"
          @update:model-value="onFilterChange"
        />
      </v-col>
      <v-col cols="12" sm="6" md="3">
        <v-autocomplete
          v-model="selectedActorId"
          :items="filterOptions.actors"
          item-value="id"
          item-title="name"
          label="Actor"
          clearable
          hide-details
          :loading="loadingOptions"
          @update:model-value="onFilterChange"
        />
      </v-col>
    </v-row>

    <v-data-table-server
      v-model:items-per-page="pagination.pageSize"
      :headers="headers"
      :items="videos"
      :items-length="total"
      :loading="loading"
      :page="pagination.page"
      item-value="videoId"
      hover
      @update:page="onPageChange"
      @update:items-per-page="onPageSizeChange"
    >
      <template #item.thumbnail="{ item }">
        <div class="py-1">
          <v-img
            v-if="item.thumbnailCdnPath"
            :src="item.thumbnailCdnPath"
            width="80"
            aspect-ratio="16/9"
            cover
            class="rounded"
          />
          <div
            v-else
            class="bg-surface-variant rounded d-flex align-center justify-center"
            style="width: 80px; aspect-ratio: 16/9"
          >
            <v-icon size="small" color="medium-emphasis">mdi-image-off</v-icon>
          </div>
        </div>
      </template>

      <template #item.releaseDate="{ item }">
        {{ item.releaseDate ?? '—' }}
      </template>

      <template #item.addedAtUtc="{ item }">
        {{ formatDate(item.addedAtUtc) }}
      </template>

      <template #item.isFulfilled="{ item }">
        <v-chip
          :color="item.isFulfilled ? 'success' : 'warning'"
          size="small"
          variant="tonal"
        >
          {{ item.isFulfilled ? 'Fulfilled' : 'Unfulfilled' }}
        </v-chip>
      </template>
    </v-data-table-server>
  </v-container>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { api, type PrdbWantedVideo, type PrdbWantedFilterOptions } from '../../api'

const videos  = ref<PrdbWantedVideo[]>([])
const total   = ref(0)
const loading = ref(false)
const error   = ref<string | null>(null)

const search          = ref('')
const statusFilter    = ref<'unfulfilled' | 'fulfilled' | 'all'>('unfulfilled')
const selectedSiteId  = ref<string | null>(null)
const selectedActorId = ref<string | null>(null)

const loadingOptions = ref(false)
const filterOptions  = ref<PrdbWantedFilterOptions>({ sites: [], actors: [] })

const pagination = reactive({ page: 1, pageSize: 50 })

const statusOptions = [
  { title: 'Unfulfilled', value: 'unfulfilled' },
  { title: 'Fulfilled',   value: 'fulfilled' },
  { title: 'All',         value: 'all' },
]

const headers = [
  { title: '',           key: 'thumbnail',  width: 100, sortable: false },
  { title: 'Site',       key: 'siteTitle',  sortable: false, width: 200 },
  { title: 'Title',      key: 'videoTitle', sortable: false },
  { title: 'Released',   key: 'releaseDate', sortable: false, width: 120 },
  { title: 'Added',      key: 'addedAtUtc',  sortable: false, width: 130 },
  { title: 'Status',     key: 'isFulfilled', sortable: false, width: 120 },
]

function isFulfilledParam(): boolean | undefined {
  if (statusFilter.value === 'unfulfilled') return false
  if (statusFilter.value === 'fulfilled')   return true
  return undefined
}

async function load() {
  loading.value = true
  error.value = null
  try {
    const result = await api.prdbWantedVideos.list({
      search:      search.value || undefined,
      isFulfilled: isFulfilledParam(),
      siteId:      selectedSiteId.value  ?? undefined,
      actorId:     selectedActorId.value ?? undefined,
      page:        pagination.page,
      pageSize:    pagination.pageSize,
    })
    videos.value = result.items
    total.value  = result.total
  } catch (e: any) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

async function loadFilterOptions() {
  loadingOptions.value = true
  try {
    filterOptions.value = await api.prdbWantedVideos.filterOptions()
  } catch {
    // non-critical — dropdowns just stay empty
  } finally {
    loadingOptions.value = false
  }
}

function onFilterChange() {
  pagination.page = 1
  load()
}

function onPageChange(page: number) {
  pagination.page = page
  load()
}

function onPageSizeChange(size: number) {
  pagination.pageSize = size
  pagination.page = 1
  load()
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

onMounted(() => {
  loadFilterOptions()
  load()
})
</script>
