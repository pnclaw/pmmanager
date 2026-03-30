import { ref } from 'vue'

interface PageAction {
  icon: string
  title: string
  onClick: () => void
}

// Module-level singleton — shared across all components
const pageAction = ref<PageAction | null>(null)

export function usePageAction() {
  function setAction(icon: string, title: string, onClick: () => void) {
    pageAction.value = { icon, title, onClick }
  }

  function clearAction() {
    pageAction.value = null
  }

  return { pageAction, setAction, clearAction }
}
