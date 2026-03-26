<template>
  <v-container>
    <v-row align="center" class="mb-4">
      <v-col>
        <h1 class="text-h4">prdb Actors</h1>
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
          @update:model-value="load"
        />
      </v-col>
    </v-row>

    <v-data-table
      :headers="headers"
      :items="actors"
      :loading="loading"
      item-value="id"
      hover
    >
      <template #item.birthday="{ item }">
        {{ item.birthday ?? '—' }}
      </template>

      <template #item.aliases="{ item }">
        <span v-if="item.aliases.length === 0" class="text-medium-emphasis">—</span>
        <span v-else>{{ item.aliases.join(', ') }}</span>
      </template>
    </v-data-table>
  </v-container>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { api, type PrdbActor } from '../../api'

const actors  = ref<PrdbActor[]>([])
const loading = ref(false)
const error   = ref<string | null>(null)
const search  = ref('')

const headers = [
  { title: 'Name',        key: 'name' },
  { title: 'Gender',      key: 'gender',      width: 100 },
  { title: 'Nationality', key: 'nationality', width: 120 },
  { title: 'Birthday',    key: 'birthday',    width: 130 },
  { title: 'Aliases',     key: 'aliases' },
]

async function load() {
  loading.value = true
  error.value = null
  try {
    actors.value = await api.prdbActors.list({
      search: search.value || undefined,
    })
  } catch (e: any) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>
