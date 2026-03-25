const BASE_URL = '/api'

export interface Item {
  id: number
  name: string
  description: string | null
  createdAt: string
}

export interface CreateItemRequest {
  name: string
  description?: string
}

export interface UpdateItemRequest {
  name: string
  description?: string
}

export interface HealthResponse {
  status: string
  timestamp: string
}

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(`${BASE_URL}${path}`, {
    headers: { 'Content-Type': 'application/json' },
    ...options,
  })
  if (!response.ok) {
    throw new Error(`HTTP ${response.status}: ${response.statusText}`)
  }
  // 204 No Content has no body
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

export const api = {
  health: {
    get: () => request<HealthResponse>('/health'),
  },
  items: {
    list: () => request<Item[]>('/items'),
    get: (id: number) => request<Item>(`/items/${id}`),
    create: (data: CreateItemRequest) =>
      request<Item>('/items', { method: 'POST', body: JSON.stringify(data) }),
    update: (id: number, data: UpdateItemRequest) =>
      request<Item>(`/items/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
    delete: (id: number) =>
      request<void>(`/items/${id}`, { method: 'DELETE' }),
  },
}
