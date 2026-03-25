<template>
  <v-container>
    <!-- Header -->
    <v-row align="center" class="mb-4">
      <v-col>
        <h1 class="text-h4">Items</h1>
      </v-col>
      <v-col class="text-right">
        <v-btn color="primary" prepend-icon="mdi-plus" @click="openCreateDialog">
          New Item
        </v-btn>
      </v-col>
    </v-row>

    <v-alert v-if="error" type="error" class="mb-4" closable @click:close="error = null">
      {{ error }}
    </v-alert>

    <!-- Items table -->
    <v-data-table
      :headers="headers"
      :items="items"
      :loading="loading"
      item-value="id"
      hover
    >
      <template #item.createdAt="{ item }">
        {{ formatDate(item.createdAt) }}
      </template>

      <template #item.description="{ item }">
        <span class="text-medium-emphasis">{{ item.description ?? '—' }}</span>
      </template>

      <template #item.actions="{ item }">
        <v-btn
          icon="mdi-delete"
          size="small"
          variant="text"
          color="error"
          @click="deleteItem(item.id)"
        />
      </template>
    </v-data-table>

    <!-- Create dialog -->
    <v-dialog v-model="createDialog" max-width="500" persistent>
      <v-card title="Create Item">
        <v-card-text>
          <v-form ref="formRef" @submit.prevent="submitCreate">
            <v-text-field
              v-model="form.name"
              label="Name"
              :rules="[(v) => !!v || 'Name is required']"
              required
              autofocus
              class="mb-2"
            />
            <v-textarea
              v-model="form.description"
              label="Description"
              rows="3"
              hide-details
            />
          </v-form>
        </v-card-text>

        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="createDialog = false">Cancel</v-btn>
          <v-btn color="primary" :loading="saving" @click="submitCreate">Create</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-container>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { api, type Item } from '../../api'

const items = ref<Item[]>([])
const loading = ref(false)
const error = ref<string | null>(null)
const createDialog = ref(false)
const saving = ref(false)
const formRef = ref()
const form = ref({ name: '', description: '' })

const headers = [
  { title: 'ID', key: 'id', width: '80px' },
  { title: 'Name', key: 'name' },
  { title: 'Description', key: 'description' },
  { title: 'Created At', key: 'createdAt', width: '200px' },
  { title: '', key: 'actions', sortable: false, align: 'end' as const, width: '60px' },
]

async function fetchItems() {
  loading.value = true
  error.value = null
  try {
    items.value = await api.items.list()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to load items'
  } finally {
    loading.value = false
  }
}

function openCreateDialog() {
  form.value = { name: '', description: '' }
  createDialog.value = true
}

async function submitCreate() {
  const { valid } = await formRef.value.validate()
  if (!valid) return

  saving.value = true
  try {
    await api.items.create({
      name: form.value.name,
      description: form.value.description || undefined,
    })
    createDialog.value = false
    await fetchItems()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to create item'
  } finally {
    saving.value = false
  }
}

async function deleteItem(id: number) {
  error.value = null
  try {
    await api.items.delete(id)
    await fetchItems()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to delete item'
  }
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleString()
}

onMounted(fetchItems)
</script>
