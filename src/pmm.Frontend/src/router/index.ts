import { createRouter, createWebHistory } from 'vue-router'
import HealthView from '../features/health/HealthView.vue'
import ItemsView from '../features/items/ItemsView.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/items' },
    { path: '/health', component: HealthView, meta: { title: 'Health' } },
    { path: '/items', component: ItemsView, meta: { title: 'Items' } },
  ],
})

export default router
