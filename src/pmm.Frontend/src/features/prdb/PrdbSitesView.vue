<template>
  <v-container>
    <v-row align="center" class="mb-4">
      <v-col>
        <h1 class="text-h4">prdb Sites</h1>
      </v-col>
      <v-col class="text-right">
        <v-btn
          color="primary"
          prepend-icon="mdi-sync"
          :loading="syncing"
          @click="sync"
        >
          Sync
        </v-btn>
      </v-col>
    </v-row>

    <v-alert v-if="syncResult" type="success" class="mb-4" closable @click:close="syncResult = null">
      Sync complete — {{ syncResult.sitesUpserted }} sites, {{ syncResult.networksUpserted }} networks, {{ syncResult.videosUpserted }} videos upserted.
    </v-alert>

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
          @update:model-value="load"
        />
      </v-col>
      <v-col cols="12" sm="4" md="3" class="d-flex align-center">
        <v-switch
          v-model="favoritesOnly"
          label="Favorites only"
          hide-details
          color="primary"
          @update:model-value="load"
        />
      </v-col>
    </v-row>

    <v-data-table
      :headers="headers"
      :items="sites"
      :loading="loading"
      item-value="id"
      hover
    >
      <template #item.isFavorite="{ item }">
        <v-icon :color="item.isFavorite ? 'amber' : 'default'">
          {{ item.isFavorite ? 'mdi-star' : 'mdi-star-outline' }}
        </v-icon>
      </template>

      <template #item.networkTitle="{ item }">
        {{ item.networkTitle ?? '—' }}
      </template>

      <template #item.actions="{ item }">
        <v-btn
          size="small"
          variant="text"
          prepend-icon="mdi-movie-open"
          :to="`/prdb/sites/${item.id}/videos`"
        >
          Videos
        </v-btn>
      </template>
    </v-data-table>
  </v-container>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { api, type PrdbSite } from '../../api'

const sites      = ref<PrdbSite[]>([])
const loading    = ref(false)
const syncing    = ref(false)
const error      = ref<string | null>(null)
const syncResult = ref<{ networksUpserted: number; sitesUpserted: number; videosUpserted: number } | null>(null)
const search     = ref('')
const favoritesOnly = ref(false)

const headers = [
  { title: '',         key: 'isFavorite',   width: 48,  sortable: false },
  { title: 'Title',   key: 'title' },
  { title: 'Network', key: 'networkTitle' },
  { title: 'Videos',  key: 'videoCount',   width: 100 },
  { title: '',        key: 'actions',       sortable: false, align: 'end' as const },
]

async function load() {
  loading.value = true
  error.value = null
  try {
    sites.value = await api.prdbSites.list({
      search: search.value || undefined,
      favoritesOnly: favoritesOnly.value || undefined,
    })
  } catch (e: any) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

async function sync() {
  syncing.value = true
  syncResult.value = null
  error.value = null
  try {
    syncResult.value = await api.prdbSync.syncAll()
    await load()
  } catch (e: any) {
    error.value = e.message
  } finally {
    syncing.value = false
  }
}

onMounted(load)
</script>
