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
      @click:row="onRowClick"
    >
      <template #item.thumbnail="{ item }">
        <div class="py-2">
          <div class="position-relative rounded overflow-hidden" style="width: 240px; height: 135px">
            <v-img
              v-if="item.thumbnailCdnPath"
              :src="item.thumbnailCdnPath"
              width="240"
              height="135"
              cover
              :style="sfwMode ? 'filter: blur(12px)' : ''"
            />
            <div
              v-else
              class="bg-surface-variant d-flex align-center justify-center"
              style="width: 240px; height: 135px"
            >
              <v-icon size="small" color="medium-emphasis">mdi-image-off</v-icon>
            </div>
            <div
              class="position-absolute text-caption font-weight-bold px-2 py-1"
              style="top: 0; left: 0; border-bottom-right-radius: 6px"
              :style="item.isFulfilled
                ? 'background: rgba(var(--v-theme-success), 0.85); color: rgb(var(--v-theme-on-success))'
                : 'background: rgba(var(--v-theme-warning), 0.85); color: rgb(var(--v-theme-on-warning))'"
            >
              {{ item.isFulfilled ? 'Fulfilled' : 'Unfulfilled' }}
            </div>
          </div>
        </div>
      </template>

      <template #item.videoInfo="{ item }">
        <div>
          <div class="text-caption text-medium-emphasis">{{ item.siteTitle }}</div>
          <div>{{ item.videoTitle }}</div>
        </div>
      </template>

      <template #item.releaseDate="{ item }">
        {{ item.releaseDate ? formatDate(item.releaseDate) : '—' }}
      </template>

      <template #item.addedAtUtc="{ item }">
        {{ formatDate(item.addedAtUtc) }}
      </template>

      <template #item.actions="{ item }">
        <v-btn
          icon="mdi-delete"
          size="small"
          variant="text"
          color="error"
          :loading="removing === item.videoId"
          @click="remove(item.videoId)"
        />
      </template>
    </v-data-table-server>
  </v-container>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useDisplay } from 'vuetify'
import { useRouter } from 'vue-router'
import { api, type PrdbWantedVideo, type PrdbWantedFilterOptions } from '../../api'
import { useSfwMode } from '../../composables/useSfwMode'

const { sfwMode } = useSfwMode()
const { mdAndUp } = useDisplay()
const router = useRouter()

const videos   = ref<PrdbWantedVideo[]>([])
const total    = ref(0)
const loading  = ref(false)
const error    = ref<string | null>(null)
const removing = ref<string | null>(null)

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

const headers = computed(() => [
  { title: '',         key: 'thumbnail',   width: 260, sortable: false },
  { title: 'Video',    key: 'videoInfo',   sortable: false },
  { title: 'Released', key: 'releaseDate', sortable: false, width: 120 },
  ...(mdAndUp.value ? [{ title: 'Added', key: 'addedAtUtc', sortable: false, width: 130 }] : []),
  { title: '',         key: 'actions',     sortable: false, width: 60 },
])

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

function onRowClick(_: MouseEvent, { item }: { item: PrdbWantedVideo }) {
  router.push(`/prdb/videos/${item.videoId}`)
}

async function remove(videoId: string) {
  removing.value = videoId
  try {
    await api.prdbWantedVideos.remove(videoId)
    videos.value = videos.value.filter(v => v.videoId !== videoId)
    total.value--
  } catch (e: any) {
    error.value = e.message
  } finally {
    removing.value = null
  }
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

onMounted(() => {
  loadFilterOptions()
  load()
})
</script>
