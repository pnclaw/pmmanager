import { createRouter, createWebHistory } from 'vue-router'
import HealthView from '../features/health/HealthView.vue'
import IndexersView from '../features/indexers/IndexersView.vue'
import IndexerRowsView from '../features/indexers/IndexerRowsView.vue'
import IndexerStatsView from '../features/indexers/IndexerStatsView.vue'
import DownloadClientsView from '../features/download-clients/DownloadClientsView.vue'
import DownloadsView from '../features/downloads/DownloadsView.vue'
import SettingsView from '../features/settings/SettingsView.vue'
import PrdbSitesView from '../features/prdb/PrdbSitesView.vue'
import PrdbSiteVideosView from '../features/prdb/PrdbSiteVideosView.vue'
import PrdbActorsView from '../features/prdb/PrdbActorsView.vue'
import PrdbStatusView from '../features/prdb/PrdbStatusView.vue'
import PrdbWantedVideosView from '../features/prdb/PrdbWantedVideosView.vue'
import PrdbVideoDetailView from '../features/prdb/PrdbVideoDetailView.vue'
import PrdbVideosView from '../features/prdb/PrdbVideosView.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/prdb/wanted' },
    { path: '/health', component: HealthView, meta: { title: 'Health' } },
    { path: '/indexers', component: IndexersView, meta: { title: 'Indexers' } },
    { path: '/indexers/:id/rows', component: IndexerRowsView, meta: { title: 'Indexer Rows' } },
    { path: '/indexers/:id/stats', component: IndexerStatsView, meta: { title: 'Indexer Stats' } },
    { path: '/download-clients', component: DownloadClientsView, meta: { title: 'Download Clients' } },
    { path: '/downloads', component: DownloadsView, meta: { title: 'Downloads' } },
    { path: '/settings', component: SettingsView, meta: { title: 'Settings' } },
    { path: '/prdb/sites', component: PrdbSitesView, meta: { title: 'PRDB Sites' } },
    { path: '/prdb/sites/:id/videos', component: PrdbSiteVideosView, meta: { title: 'PRDB Site Videos' } },
    { path: '/prdb/actors', component: PrdbActorsView, meta: { title: 'PRDB Actors' } },
    { path: '/prdb/status', component: PrdbStatusView, meta: { title: 'PRDB Status' } },
    { path: '/prdb/wanted', component: PrdbWantedVideosView, meta: { title: 'Wanted' } },
    { path: '/prdb/videos', component: PrdbVideosView, meta: { title: 'Videos' } },
    { path: '/prdb/videos/:id', component: PrdbVideoDetailView, meta: { title: 'Video' } },
  ],
})

export default router
