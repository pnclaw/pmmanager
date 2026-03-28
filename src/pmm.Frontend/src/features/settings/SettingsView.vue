<template>
  <v-container max-width="600">
    <h1 class="text-h4 mb-6">Settings</h1>

    <v-alert v-if="error" type="error" class="mb-4" closable @click:close="error = null">
      {{ error }}
    </v-alert>

    <v-alert v-if="saved" type="success" class="mb-4" closable @click:close="saved = false">
      Settings saved.
    </v-alert>

    <v-skeleton-loader v-if="loading" type="card" />

    <v-form v-else ref="formRef" @submit.prevent="submit">
      <v-card class="mb-4">
        <v-card-title>prdb.net</v-card-title>
        <v-card-text>
          <v-text-field
            v-model="form.prdbApiKey"
            label="prdb.net ApiKey"
            class="mb-2"
          />
          <v-text-field
            v-model="form.prdbApiUrl"
            label="prdb.net Url"
            :rules="[required]"
          />
        </v-card-text>
      </v-card>

      <v-card class="mb-6">
        <v-card-title>Quality</v-card-title>
        <v-card-text>
          <v-select
            v-model="form.preferredVideoQuality"
            :items="qualityItems"
            label="Preferred Video Quality"
          />
        </v-card-text>
      </v-card>

      <v-card class="mb-6">
        <v-card-title>Display</v-card-title>
        <v-card-text>
          <v-switch
            v-model="form.safeForWork"
            label="Safe for work"
            hint="Blurs all images from the prdb API"
            persistent-hint
            color="primary"
          />
        </v-card-text>
      </v-card>

      <div class="text-right">
        <v-btn type="submit" color="primary" :loading="saving">Save</v-btn>
      </div>
    </v-form>
  </v-container>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { api, VideoQuality, VideoQualityLabels, type UpdateSettingsRequest } from '../../api'
import { useSfwMode } from '../../composables/useSfwMode'

const { sfwMode } = useSfwMode()

const loading = ref(true)
const saving  = ref(false)
const error   = ref<string | null>(null)
const saved   = ref(false)
const formRef = ref()

const form = ref<UpdateSettingsRequest>({
  prdbApiKey: '',
  prdbApiUrl: '',
  preferredVideoQuality: VideoQuality.P2160,
  safeForWork: false,
})

const qualityItems = Object.values(VideoQuality)
  .filter((v): v is VideoQuality => typeof v === 'number')
  .map(v => ({ title: VideoQualityLabels[v], value: v }))

const required = (v: string) => !!v || 'Required'

onMounted(async () => {
  try {
    const settings = await api.settings.get()
    form.value = {
      prdbApiKey: settings.prdbApiKey,
      prdbApiUrl: settings.prdbApiUrl,
      preferredVideoQuality: settings.preferredVideoQuality,
      safeForWork: settings.safeForWork,
    }
  } catch (e: any) {
    error.value = e.message
  } finally {
    loading.value = false
  }
})

async function submit() {
  const { valid } = await formRef.value.validate()
  if (!valid) return

  saving.value = true
  error.value = null
  saved.value = false
  try {
    await api.settings.update(form.value)
    sfwMode.value = form.value.safeForWork
    saved.value = true
  } catch (e: any) {
    error.value = e.message
  } finally {
    saving.value = false
  }
}
</script>
