<template>
  <v-container>
    <v-btn
      variant="text"
      prepend-icon="mdi-arrow-left"
      to="/prdb/sites"
      class="mb-4 px-0"
    >
      Sites
    </v-btn>

    <v-row align="center" class="mb-4">
      <v-col>
        <h1 class="text-h4">Videos</h1>
      </v-col>
    </v-row>

    <v-alert v-if="error" type="error" class="mb-4" closable @click:close="error = null">
      {{ error }}
    </v-alert>

    <v-row class="mb-4">
      <v-col cols="12" sm="6" md="4">
        <v-text-field
          v-model="search"
          prepend-inner-icon="mdi-magnify"
          label="Search"
          clearable
          hide-details
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
      item-value="id"
      show-expand
      hover
      @update:page="onPageChange"
      @update:items-per-page="onPageSizeChange"
      @click:row="onRowClick"
    >
      <template #item.releaseDate="{ item }">
        {{ item.releaseDate ?? '—' }}
      </template>

      <template #item.preNames="{ item }">
        {{ item.preNames.length }}
      </template>

      <template #expanded-row="{ columns, item }">
        <tr>
          <td :colspan="columns.length" class="pa-3 bg-surface-variant">
            <div v-if="item.preNames.length === 0" class="text-medium-emphasis text-body-2">
              No pre-names
            </div>
            <div v-for="preName in item.preNames" :key="preName.id" class="text-body-2 mb-1">
              {{ preName.title }}
            </div>
          </td>
        </tr>
      </template>
    </v-data-table-server>
  </v-container>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api, type PrdbVideo } from '../../api'

const route  = useRoute()
const router = useRouter()
const siteId = route.params.id as string

const videos  = ref<PrdbVideo[]>([])
const total   = ref(0)
const loading = ref(false)
const error   = ref<string | null>(null)
const search  = ref('')

const pagination = reactive({ page: 1, pageSize: 50 })

const headers = [
  { title: 'Title',        key: 'title' },
  { title: 'Release Date', key: 'releaseDate', width: 140 },
  { title: 'Actors',       key: 'actorCount',  width: 90 },
  { title: 'Pre-names',    key: 'preNames',    width: 110 },
  { title: '',             key: 'data-table-expand', width: 48 },
]

async function load() {
  loading.value = true
  error.value = null
  try {
    const result = await api.prdbSites.videos(siteId, {
      search: search.value || undefined,
      page: pagination.page,
      pageSize: pagination.pageSize,
    })
    videos.value = result.items
    total.value = result.total
  } catch (e: any) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

function onRowClick(_: MouseEvent, { item }: { item: PrdbVideo }) {
  router.push(`/prdb/videos/${item.id}`)
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

onMounted(load)
</script>
