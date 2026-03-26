<template>
  <v-app>
    <v-navigation-drawer permanent>
      <v-list>
        <v-list-item
          prepend-icon="mdi-movie-open"
          title="PMManager"
          subtitle="Media Manager"
          class="py-4"
        />
        <v-divider />
        <v-list-item
          prepend-icon="mdi-heart-pulse"
          title="Health"
          to="/health"
          rounded="lg"
        />
        <v-list-item
          prepend-icon="mdi-format-list-bulleted"
          title="Items"
          to="/items"
          rounded="lg"
        />
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
          prepend-icon="mdi-cog"
          title="Settings"
          to="/settings"
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
// App shell — navigation is handled by Vue Router
</script>
