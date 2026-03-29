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

    <template v-else>
      <v-tabs v-model="tab" class="mb-4">
        <v-tab value="general">General</v-tab>
        <v-tab value="prdb">PRDB.net</v-tab>
        <v-tab value="folders">Folder Mapping</v-tab>
      </v-tabs>

      <v-window v-model="tab">
        <!-- General tab -->
        <v-window-item value="general">
          <v-form ref="formRef" @submit.prevent="submit">
            <v-card class="mb-4">
              <v-card-title>Quality</v-card-title>
              <v-card-text>
                <v-select
                  v-model="form.preferredVideoQuality"
                  :items="qualityItems"
                  label="Preferred Video Quality"
                />
              </v-card-text>
            </v-card>

            <v-card class="mb-4">
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
        </v-window-item>

        <!-- PRDB.net tab -->
        <v-window-item value="prdb">
          <v-form ref="formRef" @submit.prevent="submit">
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

            <div class="text-right">
              <v-btn type="submit" color="primary" :loading="saving">Save</v-btn>
            </div>
          </v-form>
        </v-window-item>

        <!-- Folder Mapping tab -->
        <v-window-item value="folders">
          <v-alert v-if="folderError" type="error" class="mb-4" closable @click:close="folderError = null">
            {{ folderError }}
          </v-alert>

          <div class="d-flex align-center mb-3">
            <v-btn
              :icon="showFolderInfo ? 'mdi-information' : 'mdi-information-outline'"
              variant="text"
              :color="showFolderInfo ? 'info' : undefined"
              @click="showFolderInfo = !showFolderInfo"
            />
            <v-spacer />
            <v-btn color="primary" prepend-icon="mdi-plus" @click="openAddDialog">
              Add Mapping
            </v-btn>
          </div>

          <v-expand-transition>
            <v-alert v-if="showFolderInfo" type="info" variant="tonal" class="mb-4">
              <p class="mb-1">
                Folder mappings translate paths used by your download client (e.g. SABnzbd or NZBGet)
                into paths accessible on the machine running this app.
              </p>
              <p class="mb-0">
                This is typically needed when your download client runs in Docker or on a remote
                machine and mounts network shares under different paths than your local system.
                For example, the download client may write to <code>/downloads/complete</code> while
                the same share is available locally as <code>Z:\downloads\complete</code>.
                If both apps run on the same machine you can usually leave this empty.
              </p>
            </v-alert>
          </v-expand-transition>

          <v-data-table
            :headers="folderHeaders"
            :items="folderMappings"
            :loading="folderLoading"
            item-value="id"
            hover
            no-data-text="No folder mappings configured."
          >
            <template #item.actions="{ item }">
              <v-btn
                icon="mdi-pencil"
                size="small"
                variant="text"
                class="mr-1"
                @click="openEditDialog(item)"
              />
              <v-btn
                icon="mdi-delete"
                size="small"
                variant="text"
                color="error"
                @click="deleteMapping(item.id)"
              />
            </template>
          </v-data-table>
        </v-window-item>
      </v-window>
    </template>

    <!-- Add / Edit dialog -->
    <v-dialog v-model="mappingDialog" max-width="540" persistent>
      <v-card :title="editingId ? 'Edit Mapping' : 'Add Mapping'">
        <v-card-text>
          <v-form ref="mappingFormRef" @submit.prevent="submitMapping">
            <v-text-field
              v-model="mappingForm.originalFolder"
              label="Original Folder (download client path)"
              hint="The path as seen by SABnzbd / NZBGet"
              persistent-hint
              :rules="[required]"
              class="mb-4"
              autofocus
            />
            <v-text-field
              v-model="mappingForm.mappedToFolder"
              label="Mapped To Folder (local path)"
              hint="The equivalent path on the machine running this app"
              persistent-hint
              :rules="[required]"
            />
          </v-form>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="mappingDialog = false">Cancel</v-btn>
          <v-btn color="primary" :loading="mappingSaving" @click="submitMapping">
            {{ editingId ? 'Save' : 'Add' }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-container>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { api, VideoQuality, VideoQualityLabels, type UpdateSettingsRequest, type FolderMapping, type FolderMappingRequest } from '../../api'
import { useSfwMode } from '../../composables/useSfwMode'

const { sfwMode } = useSfwMode()

// Settings form state
const loading  = ref(true)
const saving   = ref(false)
const error    = ref<string | null>(null)
const saved    = ref(false)
const formRef  = ref()
const tab      = ref('general')

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

  await fetchMappings()
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

// Folder mapping state
const folderMappings  = ref<FolderMapping[]>([])
const folderLoading   = ref(false)
const folderError     = ref<string | null>(null)
const showFolderInfo  = ref(false)
const mappingDialog   = ref(false)
const mappingSaving  = ref(false)
const mappingFormRef = ref()
const editingId      = ref<string | null>(null)
const mappingForm    = ref<FolderMappingRequest>({ originalFolder: '', mappedToFolder: '' })

const folderHeaders = [
  { title: 'Original Folder', key: 'originalFolder' },
  { title: 'Mapped To Folder', key: 'mappedToFolder' },
  { title: '', key: 'actions', sortable: false, align: 'end' as const, width: '100px' },
]

async function fetchMappings() {
  folderLoading.value = true
  try {
    folderMappings.value = await api.folderMappings.list()
  } catch (e: any) {
    folderError.value = e.message
  } finally {
    folderLoading.value = false
  }
}

function openAddDialog() {
  editingId.value = null
  mappingForm.value = { originalFolder: '', mappedToFolder: '' }
  mappingDialog.value = true
}

function openEditDialog(item: FolderMapping) {
  editingId.value = item.id
  mappingForm.value = { originalFolder: item.originalFolder, mappedToFolder: item.mappedToFolder }
  mappingDialog.value = true
}

async function submitMapping() {
  const { valid } = await mappingFormRef.value.validate()
  if (!valid) return

  mappingSaving.value = true
  folderError.value = null
  try {
    if (editingId.value) {
      await api.folderMappings.update(editingId.value, mappingForm.value)
    } else {
      await api.folderMappings.create(mappingForm.value)
    }
    mappingDialog.value = false
    await fetchMappings()
  } catch (e: any) {
    folderError.value = e.message
    mappingDialog.value = false
  } finally {
    mappingSaving.value = false
  }
}

async function deleteMapping(id: string) {
  folderError.value = null
  try {
    await api.folderMappings.delete(id)
    await fetchMappings()
  } catch (e: any) {
    folderError.value = e.message
  }
}
</script>
