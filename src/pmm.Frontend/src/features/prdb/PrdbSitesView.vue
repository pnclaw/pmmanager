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
      Sync complete — {{ syncResult.sitesUpserted }} sites, {{ syncResult.networksUpserted }} networks, {{ syncResult.favoriteSitesSynced }} favorite sites, {{ syncResult.favoriteActorsSynced }} favorite actors, {{ syncResult.videosUpserted }} videos upserted.
    </v-alert>

    <v-alert v-if="error" type="error" class="mb-4" closable @click:close="error = null">
      {{ error }}
    </v-alert>

    <v-alert
      v-if="!loading && favoritesOnly && !search && sites.length === 0"
      type="info"
      class="mb-4"
    >
      No favorite sites found. Try syncing, or turn off <strong>Favorites only</strong> to see all sites.
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
        <v-btn
          icon
          size="small"
          variant="text"
          :loading="togglingIds.includes(item.id)"
          @click="toggleFavorite(item)"
        >
          <v-icon :color="item.isFavorite ? 'amber' : 'default'">
            {{ item.isFavorite ? 'mdi-star' : 'mdi-star-outline' }}
          </v-icon>
        </v-btn>
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

const sites       = ref<PrdbSite[]>([])
const loading     = ref(false)
const syncing     = ref(false)
const error       = ref<string | null>(null)
const togglingIds = ref<string[]>([])
const syncResult = ref<{ networksUpserted: number; sitesUpserted: number; favoriteSitesSynced: number; favoriteActorsSynced: number; videosUpserted: number } | null>(null)
const search     = ref('')
const favoritesOnly = ref(true)

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

async function toggleFavorite(item: PrdbSite) {
  if (togglingIds.value.includes(item.id)) return
  togglingIds.value = [...togglingIds.value, item.id]
  const newFavorite = !item.isFavorite
  try {
    await api.prdbSites.setFavorite(item.id, newFavorite)
    item.isFavorite = newFavorite
    if (!newFavorite && favoritesOnly.value)
      sites.value = sites.value.filter(s => s.id !== item.id)
  } catch (e: any) {
    error.value = e.message
  } finally {
    togglingIds.value = togglingIds.value.filter(id => id !== item.id)
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
