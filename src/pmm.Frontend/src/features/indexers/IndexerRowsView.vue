<template>
  <v-container fluid>
    <!-- Header -->
    <v-row align="center" class="mb-4">
      <v-col cols="auto">
        <v-btn icon="mdi-arrow-left" variant="text" @click="router.push('/indexers')" />
      </v-col>
      <v-col>
        <h1 class="text-h4">Indexer Rows</h1>
      </v-col>
      <v-col class="text-right d-flex justify-end ga-2">
        <v-btn
          color="primary"
          prepend-icon="mdi-download"
          :loading="scraping"
          @click="scrapeLatest"
        >
          Get Latest
        </v-btn>
        <v-btn
          color="secondary"
          prepend-icon="mdi-history"
          @click="backfillDialog = true"
        >
          Backfill
        </v-btn>
        <v-btn
          color="error"
          prepend-icon="mdi-delete-sweep"
          :disabled="!filters.indexerId"
          @click="clearDialog = true"
        >
          Clear
        </v-btn>
      </v-col>
    </v-row>

    <v-alert v-if="error" type="error" class="mb-4" closable @click:close="error = null">
      {{ error }}
    </v-alert>
    <v-alert v-if="successMsg" type="success" class="mb-4" closable @click:close="successMsg = null">
      {{ successMsg }}
    </v-alert>

    <!-- Filters -->
    <v-card class="mb-4" variant="outlined">
      <v-card-text>
        <v-row dense>
          <v-col cols="12" sm="6" md="3">
            <v-select
              v-model="filters.indexerId"
              label="Indexer"
              :items="indexers"
              item-title="title"
              item-value="id"
              clearable
              hide-details
              @update:model-value="onIndexerChange"
            />
          </v-col>
          <v-col cols="12" sm="6" md="3">
            <v-select
              v-model="filters.categories"
              label="Category"
              :items="availableCategories"
              multiple
              clearable
              hide-details
              :disabled="!filters.indexerId"
            />
          </v-col>
          <v-col cols="12" sm="6" md="3">
            <v-text-field
              v-model="filters.from"
              label="Published from"
              type="date"
              clearable
              hide-details
            />
          </v-col>
          <v-col cols="12" sm="6" md="3">
            <v-text-field
              v-model="filters.to"
              label="Published to"
              type="date"
              clearable
              hide-details
            />
          </v-col>
          <v-col cols="12" sm="6" md="3">
            <v-text-field
              v-model="filters.search"
              label="Title search"
              prepend-inner-icon="mdi-magnify"
              clearable
              hide-details
            />
          </v-col>
          <v-col cols="12" sm="6" md="3">
            <v-text-field
              v-model.number="filters.minSizeGb"
              label="Min size (GB)"
              type="number"
              min="0"
              step="0.1"
              clearable
              hide-details
            />
          </v-col>
          <v-col cols="12" sm="6" md="3">
            <v-text-field
              v-model.number="filters.maxSizeGb"
              label="Max size (GB)"
              type="number"
              min="0"
              step="0.1"
              clearable
              hide-details
            />
          </v-col>
          <v-col cols="12" sm="6" md="3" class="d-flex align-center ga-2">
            <v-btn color="primary" @click="applyFilters">Apply</v-btn>
            <v-btn variant="text" @click="resetFilters">Reset</v-btn>
          </v-col>
        </v-row>
      </v-card-text>
    </v-card>

    <!-- Table -->
    <v-data-table-server
      v-model:items-per-page="pagination.pageSize"
      :headers="headers"
      :items="rows"
      :items-length="totalRows"
      :loading="loading"
      :page="pagination.page"
      item-value="id"
      hover
      @update:page="onPageChange"
      @update:items-per-page="onPageSizeChange"
    >
      <template #item.title="{ item }">
        <span class="text-truncate d-inline-block" style="max-width: 400px;" :title="item.title">
          {{ item.title }}
        </span>
      </template>
      <template #item.nzbSize="{ item }">
        {{ formatSize(item.nzbSize) }}
      </template>
      <template #item.fileSize="{ item }">
        {{ item.fileSize != null ? formatSize(item.fileSize) : '—' }}
      </template>
      <template #item.nzbPublishedAt="{ item }">
        {{ item.nzbPublishedAt ? formatDate(item.nzbPublishedAt) : '—' }}
      </template>
      <template #item.nzbUrl="{ item }">
        <a :href="item.nzbUrl" target="_blank" rel="noopener">
          <v-icon size="small">mdi-download</v-icon>
        </a>
      </template>
    </v-data-table-server>

    <!-- Backfill dialog -->
    <v-dialog v-model="backfillDialog" max-width="400" persistent>
      <v-card title="Backfill">
        <v-card-text>
          <v-text-field
            v-model.number="backfillPages"
            label="Number of pages"
            type="number"
            min="1"
            :rules="[v => v >= 1 || 'Must be at least 1']"
            autofocus
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="backfillDialog = false">Cancel</v-btn>
          <v-btn color="primary" :loading="backfilling" @click="runBackfill">Run</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Clear confirmation dialog -->
    <v-dialog v-model="clearDialog" max-width="420" persistent>
      <v-card title="Clear Rows">
        <v-card-text>
          This will permanently delete <strong>all rows</strong> for the selected indexer. This cannot be undone.
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="clearDialog = false">Cancel</v-btn>
          <v-btn color="error" :loading="clearing" @click="clearRows">Delete All</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-container>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { api, type Indexer, type IndexerRow } from '../../api'

const router = useRouter()
const route = useRoute()

const indexers = ref<Indexer[]>([])
const rows = ref<IndexerRow[]>([])
const totalRows = ref(0)
const loading = ref(false)
const error = ref<string | null>(null)
const successMsg = ref<string | null>(null)
const availableCategories = ref<number[]>([])

const scraping = ref(false)
const backfilling = ref(false)
const clearing = ref(false)
const backfillDialog = ref(false)
const clearDialog = ref(false)
const backfillPages = ref(10)

const filters = reactive({
  indexerId: (route.params.id as string) || null as string | null,
  categories: [] as number[],
  from: null as string | null,
  to: null as string | null,
  search: null as string | null,
  minSizeGb: null as number | null,
  maxSizeGb: null as number | null,
})

const pagination = reactive({ page: 1, pageSize: 50 })

const headers = [
  { title: 'Title', key: 'title', sortable: false },
  { title: 'Category', key: 'category', width: '110px', sortable: false },
  { title: 'NZB Size', key: 'nzbSize', width: '110px', sortable: false },
  { title: 'File Size', key: 'fileSize', width: '110px', sortable: false },
  { title: 'Published', key: 'nzbPublishedAt', width: '160px', sortable: false },
  { title: 'NZB', key: 'nzbUrl', width: '60px', sortable: false, align: 'center' as const },
]

function formatSize(bytes: number): string {
  if (bytes >= 1_073_741_824) return (bytes / 1_073_741_824).toFixed(2) + ' GB'
  if (bytes >= 1_048_576) return (bytes / 1_048_576).toFixed(1) + ' MB'
  return (bytes / 1024).toFixed(0) + ' KB'
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleString()
}

async function fetchRows() {
  if (!filters.indexerId) {
    rows.value = []
    totalRows.value = 0
    return
  }
  loading.value = true
  try {
    const result = await api.indexers.rows(filters.indexerId, {
      page: pagination.page,
      pageSize: pagination.pageSize,
      search: filters.search || undefined,
      categories: filters.categories.length ? filters.categories : undefined,
      from: filters.from || undefined,
      to: filters.to || undefined,
      minSize: filters.minSizeGb ? Math.round(filters.minSizeGb * 1_073_741_824) : undefined,
      maxSize: filters.maxSizeGb ? Math.round(filters.maxSizeGb * 1_073_741_824) : undefined,
    })
    rows.value = result.items
    totalRows.value = result.total
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to load rows'
  } finally {
    loading.value = false
  }
}

async function loadCategories() {
  if (!filters.indexerId) return
  try {
    availableCategories.value = await api.indexers.rowCategories(filters.indexerId)
  } catch {
    // non-critical
  }
}

async function onIndexerChange() {
  pagination.page = 1
  filters.categories = []
  availableCategories.value = []
  await Promise.all([fetchRows(), loadCategories()])
}

function applyFilters() {
  pagination.page = 1
  fetchRows()
}

function resetFilters() {
  filters.categories = []
  filters.from = null
  filters.to = null
  filters.search = null
  filters.minSizeGb = null
  filters.maxSizeGb = null
  pagination.page = 1
  fetchRows()
}

function onPageChange(page: number) {
  pagination.page = page
  fetchRows()
}

function onPageSizeChange(size: number) {
  pagination.pageSize = size
  pagination.page = 1
  fetchRows()
}

async function scrapeLatest() {
  if (!filters.indexerId) return
  scraping.value = true
  error.value = null
  try {
    const result = await api.indexers.scrape(filters.indexerId)
    successMsg.value = `Scrape complete — ${result.newRows} new row${result.newRows === 1 ? '' : 's'} saved.`
    await Promise.all([fetchRows(), loadCategories()])
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Scrape failed'
  } finally {
    scraping.value = false
  }
}

async function runBackfill() {
  if (!filters.indexerId || backfillPages.value < 1) return
  backfilling.value = true
  error.value = null
  try {
    const result = await api.indexers.backfill(filters.indexerId, backfillPages.value)
    successMsg.value = `Backfill complete — ${result.newRows} new row${result.newRows === 1 ? '' : 's'} saved.`
    backfillDialog.value = false
    await Promise.all([fetchRows(), loadCategories()])
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Backfill failed'
  } finally {
    backfilling.value = false
  }
}

async function clearRows() {
  if (!filters.indexerId) return
  clearing.value = true
  error.value = null
  try {
    await api.indexers.clearRows(filters.indexerId)
    successMsg.value = 'All rows cleared.'
    clearDialog.value = false
    rows.value = []
    totalRows.value = 0
    availableCategories.value = []
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Clear failed'
  } finally {
    clearing.value = false
  }
}

onMounted(async () => {
  indexers.value = await api.indexers.list()
  if (filters.indexerId) {
    await Promise.all([fetchRows(), loadCategories()])
  }
})
</script>
