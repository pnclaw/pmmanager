<template>
  <v-container>
    <v-row align="center" class="mb-4">
      <v-col>
        <h1 class="text-h4">Download Clients</h1>
      </v-col>
      <v-col class="text-right">
        <v-btn color="primary" prepend-icon="mdi-plus" @click="openCreateDialog">
          New Client
        </v-btn>
      </v-col>
    </v-row>

    <v-alert v-if="error" type="error" class="mb-4" closable @click:close="error = null">
      {{ error }}
    </v-alert>

    <v-data-table
      :headers="headers"
      :items="clients"
      :loading="loading"
      item-value="id"
      hover
    >
      <template #item.clientType="{ item }">
        {{ clientTypeLabel(item.clientType) }}
      </template>

      <template #item.useSsl="{ item }">
        <v-icon :color="item.useSsl ? 'success' : 'default'" size="small">
          {{ item.useSsl ? 'mdi-lock' : 'mdi-lock-open-outline' }}
        </v-icon>
      </template>

      <template #item.isEnabled="{ item }">
        <v-chip :color="item.isEnabled ? 'success' : 'default'" size="small">
          {{ item.isEnabled ? 'Enabled' : 'Disabled' }}
        </v-chip>
      </template>

      <template #item.actions="{ item }">
        <div class="d-flex justify-end flex-nowrap">
          <v-btn icon="mdi-pencil" size="small" variant="text" @click="openEditDialog(item)" />
          <v-btn icon="mdi-delete" size="small" variant="text" color="error" @click="deleteClient(item.id)" />
        </div>
      </template>
    </v-data-table>

    <!-- Create / Edit dialog -->
    <v-dialog v-model="dialog" max-width="520" persistent>
      <v-card :title="editingId ? 'Edit Download Client' : 'New Download Client'">
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
            <v-select
              v-model="form.clientType"
              label="Client Type"
              :items="clientTypeOptions"
              item-title="title"
              item-value="value"
              :rules="[requiredSelect]"
              required
              class="mb-2"
            />
            <v-row dense class="mb-2">
              <v-col cols="8">
                <v-text-field
                  v-model="form.host"
                  label="Host"
                  :rules="[required]"
                  required
                  hide-details
                />
              </v-col>
              <v-col cols="4">
                <v-text-field
                  v-model.number="form.port"
                  label="Port"
                  type="number"
                  :rules="[required]"
                  required
                  hide-details
                />
              </v-col>
            </v-row>

            <!-- SABnzbd: API key -->
            <v-text-field
              v-if="form.clientType === ClientType.Sabnzbd"
              v-model="form.apiKey"
              label="API Key"
              class="mb-2"
            />

            <!-- NZBGet: username + password -->
            <template v-if="form.clientType === ClientType.Nzbget">
              <v-text-field
                v-model="form.username"
                label="Username"
                class="mb-2"
              />
              <v-text-field
                v-model="form.password"
                label="Password"
                type="password"
                class="mb-2"
              />
            </template>

            <v-text-field
              v-model="form.category"
              label="Category"
              class="mb-2"
            />
            <v-row dense>
              <v-col cols="6">
                <v-switch
                  v-model="form.useSsl"
                  label="Use SSL"
                  color="primary"
                  hide-details
                />
              </v-col>
              <v-col cols="6">
                <v-switch
                  v-model="form.isEnabled"
                  label="Enabled"
                  color="primary"
                  hide-details
                />
              </v-col>
            </v-row>
          </v-form>

          <v-alert
            v-if="testResult"
            :type="testResult.success ? 'success' : 'error'"
            class="mt-4"
            density="compact"
          >
            {{ testResult.message }}
          </v-alert>
        </v-card-text>

        <v-card-actions>
          <v-btn
            variant="outlined"
            prepend-icon="mdi-connection"
            :loading="testing"
            @click="runTest"
          >
            Test
          </v-btn>
          <v-spacer />
          <v-btn variant="text" @click="dialog = false">Cancel</v-btn>
          <v-btn color="primary" :loading="saving" @click="submitForm">
            {{ editingId ? 'Save' : 'Create' }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Save-anyway dialog -->
    <v-dialog v-model="saveAnywayDialog" max-width="440" persistent>
      <v-card title="Test Failed">
        <v-card-text>
          <p class="mb-2">{{ testResult?.message }}</p>
          <p>The client has been <strong>disabled</strong>. Save anyway?</p>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="saveAnywayDialog = false">Cancel</v-btn>
          <v-btn color="warning" :loading="saving" @click="persistForm">Save Disabled</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-container>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { api, type DownloadClient, ClientType } from '../../api'

const clientTypeOptions = [
  { title: 'SABnzbd', value: ClientType.Sabnzbd },
  { title: 'NZBGet', value: ClientType.Nzbget },
]

function clientTypeLabel(value: number): string {
  return clientTypeOptions.find(o => o.value === value)?.title ?? String(value)
}

const clients = ref<DownloadClient[]>([])
const loading = ref(false)
const error = ref<string | null>(null)
const dialog = ref(false)
const saving = ref(false)
const testing = ref(false)
const saveAnywayDialog = ref(false)
const testResult = ref<{ success: boolean; message: string } | null>(null)
const editingId = ref<string | null>(null)
const formRef = ref()

const emptyForm = () => ({
  title: '',
  clientType: ClientType.Sabnzbd,
  host: '',
  port: 8080,
  useSsl: false,
  apiKey: '',
  username: '',
  password: '',
  category: '',
  isEnabled: true,
})

const form = ref(emptyForm())

const headers = [
  { title: 'Title', key: 'title' },
  { title: 'Type', key: 'clientType', width: '110px' },
  { title: 'Host', key: 'host' },
  { title: 'Port', key: 'port', width: '80px' },
  { title: 'SSL', key: 'useSsl', width: '60px', align: 'center' as const },
  { title: 'Status', key: 'isEnabled', width: '110px' },
  { title: '', key: 'actions', sortable: false, align: 'end' as const, width: '90px' },
]

const required = (v: string | number) => !!v || 'Required'
const requiredSelect = (v: number | null) => v !== null && v !== undefined ? true : 'Required'

async function fetchClients() {
  loading.value = true
  error.value = null
  try {
    clients.value = await api.downloadClients.list()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to load clients'
  } finally {
    loading.value = false
  }
}

function openCreateDialog() {
  editingId.value = null
  form.value = emptyForm()
  testResult.value = null
  dialog.value = true
}

function openEditDialog(client: DownloadClient) {
  editingId.value = client.id
  form.value = {
    title: client.title,
    clientType: client.clientType,
    host: client.host,
    port: client.port,
    useSsl: client.useSsl,
    apiKey: client.apiKey,
    username: client.username,
    password: client.password,
    category: client.category,
    isEnabled: client.isEnabled,
  }
  testResult.value = null
  dialog.value = true
}

async function runTest() {
  testing.value = true
  testResult.value = null
  try {
    testResult.value = await api.downloadClients.test({
      clientType: form.value.clientType,
      host: form.value.host,
      port: form.value.port,
      useSsl: form.value.useSsl,
      apiKey: form.value.apiKey,
      username: form.value.username,
      password: form.value.password,
    })
  } catch (e) {
    testResult.value = { success: false, message: e instanceof Error ? e.message : 'Test failed' }
  } finally {
    testing.value = false
  }
}

async function submitForm() {
  const { valid } = await formRef.value.validate()
  if (!valid) return

  testing.value = true
  try {
    testResult.value = await api.downloadClients.test({
      clientType: form.value.clientType,
      host: form.value.host,
      port: form.value.port,
      useSsl: form.value.useSsl,
      apiKey: form.value.apiKey,
      username: form.value.username,
      password: form.value.password,
    })
  } catch (e) {
    testResult.value = { success: false, message: e instanceof Error ? e.message : 'Test failed' }
  } finally {
    testing.value = false
  }

  if (!testResult.value?.success) {
    form.value.isEnabled = false
    saveAnywayDialog.value = true
    return
  }

  await persistForm()
}

async function persistForm() {
  saveAnywayDialog.value = false
  saving.value = true
  try {
    if (editingId.value) {
      await api.downloadClients.update(editingId.value, form.value)
    } else {
      await api.downloadClients.create(form.value)
    }
    dialog.value = false
    await fetchClients()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to save client'
  } finally {
    saving.value = false
  }
}

async function deleteClient(id: string) {
  error.value = null
  try {
    await api.downloadClients.delete(id)
    await fetchClients()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to delete client'
  }
}

onMounted(fetchClients)
</script>
