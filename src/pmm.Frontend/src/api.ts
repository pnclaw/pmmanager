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

export enum ParsingType {
  Newznab = 0,
}

export interface Indexer {
  id: string
  title: string
  url: string
  apiPath: string
  parsingType: number
  isEnabled: boolean
  apiKey: string
  createdAt: string
  updatedAt: string
}

export interface CreateIndexerRequest {
  title: string
  url: string
  apiPath: string
  parsingType: ParsingType
  isEnabled: boolean
  apiKey: string
}

export interface UpdateIndexerRequest {
  title: string
  url: string
  apiPath: string
  parsingType: ParsingType
  isEnabled: boolean
  apiKey: string
}

export interface ScrapeResult {
  newRows: number
}

export interface IndexerRow {
  id: string
  indexerId: string
  title: string
  nzbId: string
  nzbUrl: string
  nzbSize: number
  nzbPublishedAt: string | null
  fileSize: number | null
  category: number
  createdAt: string
}

export interface IndexerRowsQuery {
  page?: number
  pageSize?: number
  search?: string
  categories?: number[]
  from?: string
  to?: string
  minSize?: number
  maxSize?: number
}

export interface PagedResult<T> {
  items: T[]
  total: number
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
  indexers: {
    list: () => request<Indexer[]>('/indexers'),
    get: (id: string) => request<Indexer>(`/indexers/${id}`),
    create: (data: CreateIndexerRequest) =>
      request<Indexer>('/indexers', { method: 'POST', body: JSON.stringify(data) }),
    update: (id: string, data: UpdateIndexerRequest) =>
      request<Indexer>(`/indexers/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
    delete: (id: string) =>
      request<void>(`/indexers/${id}`, { method: 'DELETE' }),
    scrape: (id: string) =>
      request<ScrapeResult>(`/indexers/${id}/scrape`, { method: 'POST' }),
    backfill: (id: string, pages: number) =>
      request<ScrapeResult>(`/indexers/${id}/backfill?pages=${pages}`, { method: 'POST' }),
    rows: (id: string, query: IndexerRowsQuery = {}) => {
      const params = new URLSearchParams()
      if (query.page) params.set('page', String(query.page))
      if (query.pageSize) params.set('pageSize', String(query.pageSize))
      if (query.search) params.set('search', query.search)
      if (query.categories?.length) query.categories.forEach(c => params.append('categories', String(c)))
      if (query.from) params.set('from', query.from)
      if (query.to) params.set('to', query.to)
      if (query.minSize != null) params.set('minSize', String(query.minSize))
      if (query.maxSize != null) params.set('maxSize', String(query.maxSize))
      return request<PagedResult<IndexerRow>>(`/indexers/${id}/rows?${params}`)
    },
    rowCategories: (id: string) =>
      request<number[]>(`/indexers/${id}/rows/categories`),
    clearRows: (id: string) =>
      request<void>(`/indexers/${id}/rows`, { method: 'DELETE' }),
  },
}
