<template>
  <v-container>
    <v-alert v-if="error" type="error" class="mb-4" closable @click:close="error = null">
      {{ error }}
    </v-alert>

    <v-expand-transition>
      <v-row v-if="!mobile || filterPanelOpen" class="mb-4">
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
    </v-expand-transition>

    <!-- Card grid -->
    <div v-if="loading" class="d-flex justify-center py-10">
      <v-progress-circular indeterminate />
    </div>

    <template v-else>
      <v-row v-if="videos.length">
        <v-col v-for="item in videos" :key="item.videoId" cols="12" sm="6" md="4" lg="3">
          <v-card @click="router.push(`/prdb/videos/${item.videoId}`)">
            <div class="position-relative">
              <v-img
                v-if="item.thumbnailCdnPath"
                :src="item.thumbnailCdnPath"
                :aspect-ratio="16 / 9"
                cover
                :style="sfwMode ? 'filter: blur(12px)' : ''"
              />
              <div
                v-else
                class="bg-surface-variant d-flex align-center justify-center"
                style="aspect-ratio: 16/9"
              >
                <v-icon size="large" color="medium-emphasis">mdi-image-off</v-icon>
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

              <v-btn
                icon="mdi-pencil"
                size="small"
                class="position-absolute"
                style="top: 6px; right: 6px; background-color: rgba(0,0,0,0.5)"
                @click.stop="openDialog(item)"
              />
            </div>

            <v-card-text class="pb-3">
              <div class="text-caption text-medium-emphasis">{{ item.siteTitle }}</div>
              <div class="text-body-2 font-weight-medium">{{ item.videoTitle }}</div>
              <div class="text-caption text-medium-emphasis mt-1">
                {{ item.releaseDate ? 'Released ' + formatDate(item.releaseDate) : 'Release date unknown' }}
              </div>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>

      <div v-else class="text-center text-medium-emphasis py-10">
        No videos found.
      </div>

      <div v-if="totalPages > 1" class="d-flex justify-center mt-4">
        <v-pagination
          v-model="pagination.page"
          :length="totalPages"
          :total-visible="5"
          @update:model-value="onPageChange"
        />
      </div>
    </template>

    <!-- Edit dialog -->
    <v-dialog v-model="dialogOpen" max-width="400">
      <v-card v-if="dialogItem">
        <v-card-title class="pt-4">Wanted Video</v-card-title>
        <v-card-subtitle>{{ dialogItem.siteTitle }}</v-card-subtitle>
        <v-card-subtitle class="pb-2">{{ dialogItem.videoTitle }}</v-card-subtitle>

        <v-card-text class="text-body-2 text-medium-emphasis pb-2">
          Added {{ formatDate(dialogItem.addedAtUtc) }}
        </v-card-text>

        <v-card-actions class="px-4 pb-4 flex-column align-stretch ga-2">
          <v-btn
            :color="dialogItem.isFulfilled ? 'warning' : 'success'"
            variant="tonal"
            :loading="saving"
            :disabled="removing !== null"
            block
            @click="toggleFulfilled"
          >
            {{ dialogItem.isFulfilled ? 'Mark as unfulfilled' : 'Mark as fulfilled' }}
          </v-btn>
          <v-btn
            color="error"
            variant="tonal"
            :loading="removing !== null"
            :disabled="saving"
            block
            @click="removeFromDialog"
          >
            Remove from wanted list
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-container>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, onUnmounted } from 'vue'
import { useDisplay } from 'vuetify'
import { useRouter } from 'vue-router'
import { api, type PrdbWantedVideo, type PrdbWantedFilterOptions } from '../../api'
import { useSfwMode } from '../../composables/useSfwMode'
import { usePageAction } from '../../composables/usePageAction'
import { useFilterPanel } from '../../composables/useFilterPanel'

const { sfwMode } = useSfwMode()
const { mobile } = useDisplay()
const { setActions, clearAction } = usePageAction()
const { filterPanelOpen, toggle, closePanel } = useFilterPanel()
const router = useRouter()

const videos   = ref<PrdbWantedVideo[]>([])
const total    = ref(0)
const loading  = ref(false)
const error    = ref<string | null>(null)
const removing = ref<string | null>(null)
const saving   = ref(false)

const dialogOpen = ref(false)
const dialogItem = ref<PrdbWantedVideo | null>(null)

const search          = ref('')
const statusFilter    = ref<'unfulfilled' | 'fulfilled' | 'all'>('unfulfilled')
const selectedSiteId  = ref<string | null>(null)
const selectedActorId = ref<string | null>(null)

const loadingOptions = ref(false)
const filterOptions  = ref<PrdbWantedFilterOptions>({ sites: [], actors: [] })

const pagination = reactive({ page: 1, pageSize: 24 })

const totalPages = computed(() => Math.ceil(total.value / pagination.pageSize))

const statusOptions = [
  { title: 'Unfulfilled', value: 'unfulfilled' },
  { title: 'Fulfilled',   value: 'fulfilled' },
  { title: 'All',         value: 'all' },
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

function openDialog(item: PrdbWantedVideo) {
  dialogItem.value = item
  dialogOpen.value = true
}

async function toggleFulfilled() {
  if (!dialogItem.value) return
  const newValue = !dialogItem.value.isFulfilled
  saving.value = true
  try {
    await api.prdbWantedVideos.update(dialogItem.value.videoId, { isFulfilled: newValue })
    dialogItem.value.isFulfilled = newValue
  } catch (e: any) {
    error.value = e.message
  } finally {
    saving.value = false
  }
}

async function removeFromDialog() {
  if (!dialogItem.value) return
  const videoId = dialogItem.value.videoId
  removing.value = videoId
  try {
    await api.prdbWantedVideos.remove(videoId)
    videos.value = videos.value.filter(v => v.videoId !== videoId)
    total.value--
    dialogOpen.value = false
  } catch (e: any) {
    error.value = e.message
  } finally {
    removing.value = null
  }
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

const filtersActive = computed(() =>
  !!search.value || statusFilter.value !== 'unfulfilled' || !!selectedSiteId.value || !!selectedActorId.value
)

onMounted(() => {
  loadFilterOptions()
  load()
  setActions({ icon: 'mdi-tune', title: 'Toggle filters', onClick: toggle, badgeActive: () => filtersActive.value, mobileOnly: true })
})

onUnmounted(() => {
  clearAction()
  closePanel()
})
</script>
