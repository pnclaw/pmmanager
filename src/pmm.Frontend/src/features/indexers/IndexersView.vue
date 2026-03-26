<template>
  <v-container>
    <v-row align="center" class="mb-4">
      <v-col>
        <h1 class="text-h4">Indexers</h1>
      </v-col>
      <v-col class="text-right">
        <v-btn color="primary" prepend-icon="mdi-plus" @click="openCreateDialog">
          New Indexer
        </v-btn>
      </v-col>
    </v-row>

    <v-alert v-if="error" type="error" class="mb-4" closable @click:close="error = null">
      {{ error }}
    </v-alert>

    <v-data-table
      :headers="headers"
      :items="indexers"
      :loading="loading"
      item-value="id"
      hover
    >
      <template #item.parsingType="{ item }">
        {{ parsingTypeLabel(item.parsingType) }}
      </template>

      <template #item.isEnabled="{ item }">
        <v-chip :color="item.isEnabled ? 'success' : 'default'" size="small">
          {{ item.isEnabled ? 'Enabled' : 'Disabled' }}
        </v-chip>
      </template>

      <template #item.actions="{ item }">
        <v-btn
          icon="mdi-pencil"
          size="small"
          variant="text"
          @click="openEditDialog(item)"
        />
        <v-btn
          icon="mdi-delete"
          size="small"
          variant="text"
          color="error"
          @click="deleteIndexer(item.id)"
        />
      </template>
    </v-data-table>

    <v-dialog v-model="dialog" max-width="500" persistent>
      <v-card :title="editingId ? 'Edit Indexer' : 'New Indexer'">
        <v-card-text>
          <v-form ref="formRef" @submit.prevent="submitForm">
            <v-text-field
              v-model="form.title"
              label="Title"
              :rules="[required]"
              required
              autofocus
              class="mb-2"
            />
            <v-text-field
              v-model="form.url"
              label="URL"
              :rules="[required]"
              required
              class="mb-2"
            />
            <v-select
              v-model="form.parsingType"
              label="Parsing Type"
              :items="parsingTypeOptions"
              item-title="title"
              item-value="value"
              :rules="[requiredSelect]"
              required
              class="mb-2"
            />
            <v-text-field
              v-model="form.apiKey"
              label="API Key"
              class="mb-2"
            />
            <v-switch
              v-model="form.isEnabled"
              label="Enabled"
              color="primary"
              hide-details
            />
          </v-form>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="dialog = false">Cancel</v-btn>
          <v-btn color="primary" :loading="saving" @click="submitForm">
            {{ editingId ? 'Save' : 'Create' }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-container>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { api, type Indexer, ParsingType } from '../../api'

const parsingTypeOptions = [
  { title: 'Newznab', value: ParsingType.Newznab },
]

const indexers = ref<Indexer[]>([])
const loading = ref(false)
const error = ref<string | null>(null)
const dialog = ref(false)
const saving = ref(false)
const editingId = ref<string | null>(null)
const formRef = ref()

const emptyForm = () => ({
  title: '',
  url: '',
  parsingType: ParsingType.Newznab,
  isEnabled: true,
  apiKey: '',
})

const form = ref(emptyForm())

const headers = [
  { title: 'Title', key: 'title' },
  { title: 'URL', key: 'url' },
  { title: 'Type', key: 'parsingType', width: '120px' },
  { title: 'Status', key: 'isEnabled', width: '110px' },
  { title: '', key: 'actions', sortable: false, align: 'end' as const, width: '90px' },
]

const required = (v: string) => !!v || 'Required'
const requiredSelect = (v: number | null) => v !== null && v !== undefined ? true : 'Required'

function parsingTypeLabel(value: number): string {
  return parsingTypeOptions.find((o) => o.value === value)?.title ?? String(value)
}

async function fetchIndexers() {
  loading.value = true
  error.value = null
  try {
    indexers.value = await api.indexers.list()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to load indexers'
  } finally {
    loading.value = false
  }
}

function openCreateDialog() {
  editingId.value = null
  form.value = emptyForm()
  dialog.value = true
}

function openEditDialog(indexer: Indexer) {
  editingId.value = indexer.id
  form.value = {
    title: indexer.title,
    url: indexer.url,
    parsingType: indexer.parsingType,
    isEnabled: indexer.isEnabled,
    apiKey: indexer.apiKey,
  }
  dialog.value = true
}

async function submitForm() {
  const { valid } = await formRef.value.validate()
  if (!valid) return

  saving.value = true
  try {
    if (editingId.value) {
      await api.indexers.update(editingId.value, form.value)
    } else {
      await api.indexers.create(form.value)
    }
    dialog.value = false
    await fetchIndexers()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to save indexer'
  } finally {
    saving.value = false
  }
}

async function deleteIndexer(id: string) {
  error.value = null
  try {
    await api.indexers.delete(id)
    await fetchIndexers()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to delete indexer'
  }
}

onMounted(fetchIndexers)
</script>
