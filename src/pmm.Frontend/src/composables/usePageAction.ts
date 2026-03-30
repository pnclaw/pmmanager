import { ref } from 'vue'

interface PageAction {
  icon: string
  title: string
  onClick: () => void
}

// Module-level singleton — shared across all components
const pageAction = ref<PageAction | null>(null)
const pageActionLoading = ref(false)

export function usePageAction() {
  function setAction(icon: string, title: string, onClick: () => void) {
    pageAction.value = { icon, title, onClick }
    pageActionLoading.value = false
  }

  function clearAction() {
    pageAction.value = null
    pageActionLoading.value = false
  }

  function setActionLoading(loading: boolean) {
    pageActionLoading.value = loading
  }

  return { pageAction, pageActionLoading, setAction, clearAction, setActionLoading }
}
