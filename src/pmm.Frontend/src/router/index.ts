import { createRouter, createWebHistory } from 'vue-router'
import HealthView from '../features/health/HealthView.vue'
import ItemsView from '../features/items/ItemsView.vue'
import IndexersView from '../features/indexers/IndexersView.vue'
import IndexerRowsView from '../features/indexers/IndexerRowsView.vue'
import DownloadClientsView from '../features/download-clients/DownloadClientsView.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/items' },
    { path: '/health', component: HealthView, meta: { title: 'Health' } },
    { path: '/items', component: ItemsView, meta: { title: 'Items' } },
    { path: '/indexers', component: IndexersView, meta: { title: 'Indexers' } },
    { path: '/indexers/:id/rows', component: IndexerRowsView, meta: { title: 'Indexer Rows' } },
    { path: '/download-clients', component: DownloadClientsView, meta: { title: 'Download Clients' } },
  ],
})

export default router
