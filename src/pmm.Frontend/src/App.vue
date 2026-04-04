<template>
  <v-app>
    <v-app-bar>
      <v-app-bar-nav-icon @click="drawer = !drawer" />
      <v-app-bar-title>{{ mobile ? pageTitle : `PMManager — ${pageTitle}` }}</v-app-bar-title>
      <template #append>
        <v-btn
          v-for="(action, i) in pageActions"
          v-show="!action.mobileOnly || mobile"
          :key="i"
          :title="action.title"
          :loading="i === 0 && pageActionLoading"
          icon
          @click="action.onClick()"
        >
          <v-badge
            :model-value="action.badgeActive?.() ?? false"
            dot
            color="primary"
            floating
          >
            <v-icon>{{ action.icon }}</v-icon>
          </v-badge>
        </v-btn>
      </template>
    </v-app-bar>

    <v-navigation-drawer v-model="drawer">
      <v-list>
        <v-list-subheader>Usenet</v-list-subheader>
        <v-list-item
          prepend-icon="mdi-database-search"
          title="Indexers"
          to="/indexers"
          rounded="lg"
        />
        <v-list-item
          prepend-icon="mdi-download-network"
          title="Download Clients"
          to="/download-clients"
          rounded="lg"
        />
        <v-list-item
          prepend-icon="mdi-download"
          title="Downloads"
          to="/downloads"
          rounded="lg"
        />
        <v-divider class="my-2" />
        <v-list-subheader>PRDB</v-list-subheader>
        <v-list-item
          prepend-icon="mdi-bookmark-multiple"
          title="Wanted"
          to="/prdb/wanted"
          rounded="lg"
        />
        <v-list-item
          prepend-icon="mdi-format-list-bulleted-square"
          title="PreDB"
          to="/prdb/predb"
          rounded="lg"
        />
        <v-list-item
          prepend-icon="mdi-video"
          title="Videos"
          to="/prdb/videos"
          rounded="lg"
        />
        <v-list-item
          prepend-icon="mdi-web"
          title="Sites"
          to="/prdb/sites"
          rounded="lg"
        />
        <v-list-item
          prepend-icon="mdi-account-group"
          title="Actors"
          to="/prdb/actors"
          rounded="lg"
        />
        <v-list-item
          prepend-icon="mdi-chart-box"
          title="Status"
          to="/prdb/status"
          rounded="lg"
        />
        <v-divider class="my-2" />
        <v-list-item
          prepend-icon="mdi-cog"
          title="Settings"
          to="/settings"
          rounded="lg"
        />
        <v-list-item
          prepend-icon="mdi-heart-pulse"
          title="Health"
          to="/health"
          rounded="lg"
        />
      </v-list>
    </v-navigation-drawer>

    <v-main>
      <router-view />
    </v-main>
  </v-app>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useDisplay } from 'vuetify'
import { api } from './api'
import { useSfwMode } from './composables/useSfwMode'
import { usePageAction } from './composables/usePageAction'

const { sfwMode } = useSfwMode()
const { pageActions, pageActionLoading } = usePageAction()
const route = useRoute()
const { mobile } = useDisplay()

const drawer = ref(true)

const pageTitle = computed(() => (route.meta.title as string) ?? 'PMManager')

// On mobile, close the drawer after navigating
import { watch } from 'vue'
watch(
  () => route.path,
  () => { if (mobile.value) drawer.value = false }
)

onMounted(async () => {
  if (mobile.value) drawer.value = false
  try {
    const settings = await api.settings.get()
    sfwMode.value = settings.safeForWork
  } catch {
    // non-critical — SFW defaults to off
  }
})
</script>
