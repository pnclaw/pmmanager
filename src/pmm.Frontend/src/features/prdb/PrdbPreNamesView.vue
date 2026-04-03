<template>
  <v-container style="max-width: 900px">
    <v-alert v-if="error" type="error" class="mb-4" closable @click:close="error = null">
      {{ error }}
    </v-alert>

    <v-expand-transition>
      <v-row v-if="!mobile || filterPanelOpen" class="mb-4" dense>
        <v-col cols="12" sm="6" md="4">
          <v-text-field
            v-model="search"
            prepend-inner-icon="mdi-magnify"
            label="Search prenames"
            clearable
            hide-details
            @update:model-value="onSearchChange"
          />
        </v-col>
        <v-col cols="6" sm="3" md="2">
          <v-text-field
            v-model="releaseDateFrom"
            label="From"
            type="date"
            hide-details
            @update:model-value="onFilterChange"
          />
        </v-col>
        <v-col cols="6" sm="3" md="2">
          <v-text-field
            v-model="releaseDateTo"
            label="To"
            type="date"
            hide-details
            @update:model-value="onFilterChange"
          />
        </v-col>
      </v-row>
    </v-expand-transition>

    <div v-if="loading" class="text-center py-8">
      <v-progress-circular indeterminate color="primary" />
    </div>

    <div v-else-if="groups.length === 0" class="text-center py-8 text-medium-emphasis">
      No prenames found.
    </div>

    <template v-else>
      <div class="text-caption text-medium-emphasis mb-2">
        {{ totalGroups }} video group{{ totalGroups === 1 ? '' : 's' }} found
      </div>

      <v-list class="pa-0">
        <template v-for="(group, index) in groups" :key="group.videoId">
          <v-list-item
            class="py-3"
            :ripple="true"
            @click="router.push(`/prdb/videos/${group.videoId}`)"
          >
            <v-list-item-title class="font-weight-medium mb-1">
              {{ group.siteTitle }} - {{ group.videoTitle }}
            </v-list-item-title>
            <div class="d-flex flex-wrap ga-1 mt-1">
              <v-chip
                v-for="preName in group.preNames"
                :key="preName.id"
                size="x-small"
                variant="tonal"
              >
                {{ preName.title }}
              </v-chip>
            </div>

            <template #append>
              <v-icon size="small" color="medium-emphasis">mdi-chevron-right</v-icon>
            </template>
          </v-list-item>

          <v-divider v-if="index < groups.length - 1" />
        </template>
      </v-list>
    </template>
  </v-container>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { useDisplay } from 'vuetify'
import { useRouter } from 'vue-router'
import { api, type PrdbPreNameGroup } from '../../api'
import { usePageAction } from '../../composables/usePageAction'
import { useFilterPanel } from '../../composables/useFilterPanel'

const { mobile } = useDisplay()
const { setActions, clearAction } = usePageAction()
const { filterPanelOpen, toggle, closePanel } = useFilterPanel()
const router = useRouter()

function toDateString(d: Date): string {
  return d.toISOString().slice(0, 10)
}

const today = new Date()
const sevenDaysAgo = new Date(today)
sevenDaysAgo.setDate(today.getDate() - 7)

const groups = ref<PrdbPreNameGroup[]>([])
const totalGroups = ref(0)
const loading = ref(false)
const error = ref<string | null>(null)
const search = ref('')
const releaseDateFrom = ref(toDateString(sevenDaysAgo))
const releaseDateTo = ref(toDateString(today))

let debounceTimer: ReturnType<typeof setTimeout> | null = null

async function load() {
  const q = search.value?.trim()
  const hasQuery = q && q.length >= 3
  const hasDateRange = releaseDateFrom.value || releaseDateTo.value

  if (!hasQuery && !hasDateRange) {
    groups.value = []
    totalGroups.value = 0
    return
  }

  loading.value = true
  error.value = null

  try {
    const result = await api.prdbPreNames.search({
      q: hasQuery ? q : undefined,
      releaseDateFrom: releaseDateFrom.value || undefined,
      releaseDateTo: releaseDateTo.value || undefined,
    })
    groups.value = result.items
    totalGroups.value = result.totalGroups
  } catch (e: any) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

function onSearchChange() {
  if (debounceTimer) clearTimeout(debounceTimer)
  debounceTimer = setTimeout(() => load(), 400)
}

function onFilterChange() {
  if (debounceTimer) clearTimeout(debounceTimer)
  load()
}

onMounted(() => {
  load()
  setActions({
    icon: 'mdi-tune',
    title: 'Toggle filters',
    onClick: toggle,
    badgeActive: () => !!(search.value || releaseDateFrom.value || releaseDateTo.value),
    mobileOnly: true,
  })
})

onUnmounted(() => {
  if (debounceTimer) clearTimeout(debounceTimer)
  clearAction()
  closePanel()
})
</script>
