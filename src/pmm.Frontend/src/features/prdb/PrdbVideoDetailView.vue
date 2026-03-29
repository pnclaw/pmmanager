<template>
  <v-container>
    <v-btn
      variant="text"
      prepend-icon="mdi-arrow-left"
      class="mb-4 px-0"
      @click="$router.back()"
    >
      Back
    </v-btn>

    <div v-if="loading" class="d-flex justify-center py-12">
      <v-progress-circular indeterminate />
    </div>

    <v-alert v-if="error" type="error" class="mb-4" closable @click:close="error = null">
      {{ error }}
    </v-alert>

    <template v-if="video">
      <v-row class="mb-6">
        <v-col cols="12" md="5" lg="4">
          <v-img
            v-if="video.imageCdnPaths.length > 0"
            :src="video.imageCdnPaths[0]"
            aspect-ratio="16/9"
            cover
            class="rounded"
            :style="sfwMode ? 'filter: blur(12px)' : ''"
          />
          <div
            v-else
            class="bg-surface-variant rounded d-flex align-center justify-center"
            style="aspect-ratio: 16/9"
          >
            <v-icon size="x-large" color="medium-emphasis">mdi-image-off</v-icon>
          </div>
        </v-col>

        <v-col cols="12" md="7" lg="8">
          <div class="text-caption text-medium-emphasis mb-1">{{ video.siteTitle }}</div>
          <h1 class="text-h5 mb-4">{{ video.title }}</h1>

          <v-chip
            v-if="video.isFulfilled !== null"
            :color="video.isFulfilled ? 'success' : 'warning'"
            variant="tonal"
            class="mb-4"
          >
            {{ video.isFulfilled ? 'Fulfilled' : 'Wanted' }}
          </v-chip>

          <div v-if="video.releaseDate" class="text-body-2 mb-1">
            <span class="text-medium-emphasis">Released:</span> {{ formatDate(video.releaseDate) }}
          </div>
        </v-col>
      </v-row>

      <div v-if="video.actors.length > 0" class="mb-6">
        <div class="text-subtitle-1 font-weight-medium mb-2">Cast</div>
        <div class="d-flex flex-wrap ga-2">
          <v-chip
            v-for="actor in video.actors"
            :key="actor.id"
            size="small"
          >
            {{ actor.name }}
          </v-chip>
        </div>
      </div>

      <div v-if="video.preNames.length > 0">
        <div class="text-subtitle-1 font-weight-medium mb-2">Alternative titles</div>
        <div v-for="name in video.preNames" :key="name" class="text-body-2 mb-1">
          {{ name }}
        </div>
      </div>
    </template>
  </v-container>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { api, type PrdbVideoDetail } from '../../api'
import { useSfwMode } from '../../composables/useSfwMode'

const { sfwMode } = useSfwMode()
const route = useRoute()
const id = route.params.id as string

const video   = ref<PrdbVideoDetail | null>(null)
const loading = ref(false)
const error   = ref<string | null>(null)

async function load() {
  loading.value = true
  error.value = null
  try {
    video.value = await api.prdbVideos.get(id)
  } catch (e: any) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

onMounted(load)
</script>
