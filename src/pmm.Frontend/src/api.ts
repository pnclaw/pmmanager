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

export enum VideoQuality {
  P720  = 0,
  P1080 = 1,
  P2160 = 2,
}

export const VideoQualityLabels: Record<VideoQuality, string> = {
  [VideoQuality.P720]:  '720p',
  [VideoQuality.P1080]: '1080p',
  [VideoQuality.P2160]: '2160p',
}

export interface AppSettings {
  prdbApiKey: string
  prdbApiUrl: string
  preferredVideoQuality: VideoQuality
}

export interface UpdateSettingsRequest {
  prdbApiKey: string
  prdbApiUrl: string
  preferredVideoQuality: VideoQuality
}

export enum ClientType {
  Sabnzbd = 0,
  Nzbget = 1,
}

export interface DownloadClient {
  id: string
  title: string
  clientType: number
  host: string
  port: number
  useSsl: boolean
  apiKey: string
  username: string
  password: string
  category: string
  isEnabled: boolean
  createdAt: string
  updatedAt: string
}

export interface CreateDownloadClientRequest {
  title: string
  clientType: ClientType
  host: string
  port: number
  useSsl: boolean
  apiKey: string
  username: string
  password: string
  category: string
  isEnabled: boolean
}

export type UpdateDownloadClientRequest = CreateDownloadClientRequest


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

export interface IndexerStats {
  totalSearchRequests: number
  totalGrabs: number
  totalRows: number
  avgResponseTimeMs: number | null
  searchSuccess: number
  searchFailure: number
  requestsPerDay: { date: string; search: number; grab: number }[]
  avgResponseTimePerDay: { date: string; avgMs: number }[]
  rowsByCategory: { category: number; count: number }[]
}

export interface PrdbSite {
  id: string
  title: string
  url: string
  networkId: string | null
  networkTitle: string | null
  isFavorite: boolean
  favoritedAtUtc: string | null
  videoCount: number
}

export interface PrdbVideo {
  id: string
  title: string
  releaseDate: string | null
  actorCount: number
  preNames: { id: string; title: string }[]
}

export interface PrdbActor {
  id: string
  name: string
  gender: number
  nationality: number
  birthday: string | null
  isFavorite: boolean
  favoritedAtUtc: string | null
  aliases: string[]
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
    test: (data: { url: string; apiPath: string; apiKey: string }) =>
      request<{ success: boolean; message: string }>('/indexers/test', {
        method: 'POST',
        body: JSON.stringify(data),
      }),
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
    stats: (id: string) =>
      request<IndexerStats>(`/indexers/${id}/stats`),
    rowCategories: (id: string) =>
      request<number[]>(`/indexers/${id}/rows/categories`),
    clearRows: (id: string) =>
      request<void>(`/indexers/${id}/rows`, { method: 'DELETE' }),
  },
  prdbSites: {
    list: (params?: { search?: string; favoritesOnly?: boolean }) => {
      const q = new URLSearchParams()
      if (params?.search) q.set('search', params.search)
      if (params?.favoritesOnly) q.set('favoritesOnly', 'true')
      return request<PrdbSite[]>(`/prdb-sites?${q}`)
    },
    videos: (id: string, params?: { search?: string }) => {
      const q = new URLSearchParams()
      if (params?.search) q.set('search', params.search)
      return request<PrdbVideo[]>(`/prdb-sites/${id}/videos?${q}`)
    },
  },
  prdbActors: {
    list: (params?: { search?: string; favoritesOnly?: boolean }) => {
      const q = new URLSearchParams()
      if (params?.search) q.set('search', params.search)
      if (params?.favoritesOnly) q.set('favoritesOnly', 'true')
      return request<PrdbActor[]>(`/prdb-actors?${q}`)
    },
  },
  prdbSync: {
    syncAll: () => request<{ networksUpserted: number; sitesUpserted: number; favoriteSitesSynced: number; favoriteActorsSynced: number; videosUpserted: number }>('/prdb-sync', { method: 'POST' }),
  },
  settings: {
    get: () => request<AppSettings>('/settings'),
    update: (data: UpdateSettingsRequest) =>
      request<AppSettings>('/settings', { method: 'PUT', body: JSON.stringify(data) }),
  },
  downloadClients: {
    list: () => request<DownloadClient[]>('/download-clients'),
    get: (id: string) => request<DownloadClient>(`/download-clients/${id}`),
    create: (data: CreateDownloadClientRequest) =>
      request<DownloadClient>('/download-clients', { method: 'POST', body: JSON.stringify(data) }),
    update: (id: string, data: UpdateDownloadClientRequest) =>
      request<DownloadClient>(`/download-clients/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
    delete: (id: string) =>
      request<void>(`/download-clients/${id}`, { method: 'DELETE' }),
    test: (data: { clientType: ClientType; host: string; port: number; useSsl: boolean; apiKey: string; username: string; password: string }) =>
      request<{ success: boolean; message: string }>('/download-clients/test', { method: 'POST', body: JSON.stringify(data) }),
    send: (id: string, nzbUrl: string, name: string, indexerId: string) =>
      request<{ success: boolean; message: string }>(`/download-clients/${id}/send`, {
        method: 'POST',
        body: JSON.stringify({ nzbUrl, name, indexerId }),
      }),
  },
}
