const BASE_URL = '/api'

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
  safeForWork: boolean
}

export interface UpdateSettingsRequest {
  prdbApiKey: string
  prdbApiUrl: string
  preferredVideoQuality: VideoQuality
  safeForWork: boolean
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
  backfillDays: number
  backfillStartedAtUtc: string | null
  backfillCutoffUtc: string | null
  backfillCompletedAtUtc: string | null
  backfillLastRunAtUtc: string | null
  backfillCurrentOffset: number | null
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
  backfillDays: number
}

export interface UpdateIndexerRequest {
  title: string
  url: string
  apiPath: string
  parsingType: ParsingType
  isEnabled: boolean
  apiKey: string
  backfillDays: number
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
  prdbVideoId: string | null
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
  hasVideoLink?: boolean
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
  thumbnailCdnPath: string | null
}

export interface PrdbVideo {
  id: string
  title: string
  releaseDate: string | null
  actorCount: number
  preNames: { id: string; title: string }[]
}

export interface PrdbPreNameItem {
  id: string
  title: string
}

export interface PrdbPreNameGroup {
  videoId: string
  videoTitle: string
  siteId: string
  siteTitle: string
  preNames: PrdbPreNameItem[]
}

export interface PrdbPreNamesSearchResult {
  items: PrdbPreNameGroup[]
  totalGroups: number
}

export interface PrdbPreDbEntry {
  id: string
  title: string
  createdAtUtc: string
  videoId: string | null
  videoTitle: string | null
  siteId: string | null
  siteTitle: string | null
  releaseDate: string | null
  hasLinkedVideo: boolean
}

export interface PrdbPreDbFilterOptions {
  sites: { id: string; title: string }[]
}

export interface PrdbVideoDetail {
  id: string
  title: string
  releaseDate: string | null
  siteId: string
  siteTitle: string
  siteUrl: string | null
  imageCdnPaths: string[]
  actors: { id: string; name: string }[]
  preNames: string[]
  isFulfilled: boolean | null
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
  profileImageUrl: string | null
}

export interface PrdbWantedVideo {
  videoId: string
  videoTitle: string
  siteId: string
  siteTitle: string
  releaseDate: string | null
  thumbnailCdnPath: string | null
  isFulfilled: boolean
  fulfilledAtUtc: string | null
  fulfilledInQuality: number | null
  addedAtUtc: string
}

export interface PrdbWantedFilterOptions {
  sites: { id: string; title: string }[]
  actors: { id: string; name: string }[]
}

export interface PrdbVideoListItem {
  id: string
  title: string
  releaseDate: string | null
  siteId: string
  siteTitle: string
  thumbnailCdnPath: string | null
  actorCount: number
  isWanted: boolean
  isFulfilled: boolean | null
}

export interface PrdbVideoFilterOptions {
  sites: { id: string; title: string }[]
}

export interface PrdbStatus {
  syncWorker: {
    intervalMinutes: number
    lastRunAt: string | null
    nextRunAt: string | null
  }
  actorBackfill: {
    isComplete: boolean
    currentPage: number | null
    totalActors: number | null
    actorsInDb: number
    lastSyncedAt: string | null
  }
  actorDetailSync: {
    actorsWithDetail: number
    actorsPending: number
    totalActors: number
    favoriteActors: number
  }
  videoDetailSync: {
    videosWithDetail: number
    videosPending: number
    totalVideos: number
    videosWithCast: number
  }
  preNameSync: {
    totalPreNames: number
    isBackfilling: boolean
    backfillPage: number | null
    backfillTotalCount: number | null
    lastSyncedAt: string | null
  }
  wantedVideoSync: {
    total: number
    unfulfilled: number
    fulfilled: number
    pendingDetail: number
    lastSyncedAt: string | null
  }
  indexerBackfills: {
    indexerId: string
    indexerTitle: string
    isEnabled: boolean
    days: number
    isComplete: boolean
    startedAtUtc: string | null
    cutoffUtc: string | null
    completedAtUtc: string | null
    lastRunAtUtc: string | null
    currentOffset: number | null
  }[]
  indexerRowMatchSync: {
    totalMatches: number
    lastRunAt: string | null
    topIndexers: { title: string; totalRows: number; rowsLastWeek: number }[]
  }
  library: {
    networks: number
    sites: number
    favoriteSites: number
    videos: number
    preNames: number
    actors: number
    favoriteActors: number
    actorImages: number
    videoImages: number
    wantedVideos: number
  }
  rateLimit: {
    isEnforced: boolean
    hourly: { limit: number; used: number; remaining: number; resetsInSeconds: number }
    monthly: { limit: number; used: number; remaining: number; resetsInSeconds: number }
  } | null
}

export enum DownloadStatus {
  Queued         = 0,
  Downloading    = 1,
  PostProcessing = 2,
  Completed      = 3,
  Failed         = 4,
}

export const DownloadStatusLabels: Record<DownloadStatus, string> = {
  [DownloadStatus.Queued]:         'Queued',
  [DownloadStatus.Downloading]:    'Downloading',
  [DownloadStatus.PostProcessing]: 'Post-processing',
  [DownloadStatus.Completed]:      'Completed',
  [DownloadStatus.Failed]:         'Failed',
}

export interface DownloadLog {
  id: string
  indexerRowId: string
  downloadClientId: string
  downloadClientTitle: string
  nzbName: string
  nzbUrl: string
  clientItemId: string | null
  status: DownloadStatus
  storagePath: string | null
  fileNames: string[] | null
  totalSizeBytes: number | null
  downloadedBytes: number | null
  errorMessage: string | null
  lastPolledAt: string | null
  completedAt: string | null
  createdAt: string
  updatedAt: string
}

export interface FolderMapping {
  id: string
  originalFolder: string
  mappedToFolder: string
  createdAt: string
  updatedAt: string
}

export interface FolderMappingRequest {
  originalFolder: string
  mappedToFolder: string
}

export interface IndexerRowMatchDebugResult {
  rowsChecked: number
  rows: {
    rowId: string
    title: string
    indexerTitle: string
    matchStatus: 'Matched' | 'AlreadyMatched' | 'MultipleMatches' | 'NoMatch'
    candidatePreNames: string[]
    matchedVideoTitle: string | null
  }[]
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
      if (query.hasVideoLink != null) params.set('hasVideoLink', String(query.hasVideoLink))
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
    list: (params?: { search?: string; favoritesOnly?: boolean; page?: number; pageSize?: number }) => {
      const q = new URLSearchParams()
      if (params?.search) q.set('search', params.search)
      if (params?.favoritesOnly) q.set('favoritesOnly', 'true')
      if (params?.page) q.set('page', String(params.page))
      if (params?.pageSize) q.set('pageSize', String(params.pageSize))
      return request<PagedResult<PrdbSite>>(`/prdb-sites?${q}`)
    },
    videos: (id: string, params?: { search?: string; page?: number; pageSize?: number }) => {
      const q = new URLSearchParams()
      if (params?.search) q.set('search', params.search)
      if (params?.page) q.set('page', String(params.page))
      if (params?.pageSize) q.set('pageSize', String(params.pageSize))
      return request<PagedResult<PrdbVideo>>(`/prdb-sites/${id}/videos?${q}`)
    },
    setFavorite: (id: string, favorite: boolean) =>
      request<void>(`/prdb-sites/${id}/favorite`, { method: favorite ? 'POST' : 'DELETE' }),
  },
  prdbActors: {
    list: (params?: { search?: string; favoritesOnly?: boolean; page?: number; pageSize?: number }) => {
      const q = new URLSearchParams()
      if (params?.search) q.set('search', params.search)
      if (params?.favoritesOnly) q.set('favoritesOnly', 'true')
      if (params?.page) q.set('page', String(params.page))
      if (params?.pageSize) q.set('pageSize', String(params.pageSize))
      return request<PagedResult<PrdbActor>>(`/prdb-actors?${q}`)
    },
    setFavorite: (id: string, favorite: boolean) =>
      request<void>(`/prdb-actors/${id}/favorite`, { method: favorite ? 'POST' : 'DELETE' }),
  },
  prdbWantedVideos: {
    list: (params?: { search?: string; isFulfilled?: boolean; siteId?: string; actorId?: string; page?: number; pageSize?: number }) => {
      const q = new URLSearchParams()
      if (params?.search) q.set('search', params.search)
      if (params?.isFulfilled !== undefined) q.set('isFulfilled', String(params.isFulfilled))
      if (params?.siteId) q.set('siteId', params.siteId)
      if (params?.actorId) q.set('actorId', params.actorId)
      if (params?.page) q.set('page', String(params.page))
      if (params?.pageSize) q.set('pageSize', String(params.pageSize))
      return request<PagedResult<PrdbWantedVideo>>(`/prdb-wanted-videos?${q}`)
    },
    filterOptions: () => request<PrdbWantedFilterOptions>('/prdb-wanted-videos/filter-options'),
    add: (videoId: string) => request<void>(`/prdb-wanted-videos/${videoId}`, { method: 'POST' }),
    update: (videoId: string, data: { isFulfilled: boolean }) =>
      request<void>(`/prdb-wanted-videos/${videoId}`, { method: 'PATCH', body: JSON.stringify(data) }),
    remove: (videoId: string) => request<void>(`/prdb-wanted-videos/${videoId}`, { method: 'DELETE' }),
  },
  prdbVideos: {
    list: (params?: { search?: string; siteId?: string; page?: number; pageSize?: number }) => {
      const q = new URLSearchParams()
      if (params?.search) q.set('search', params.search)
      if (params?.siteId) q.set('siteId', params.siteId)
      if (params?.page) q.set('page', String(params.page))
      if (params?.pageSize) q.set('pageSize', String(params.pageSize))
      return request<PagedResult<PrdbVideoListItem>>(`/prdb-videos?${q}`)
    },
    filterOptions: () => request<PrdbVideoFilterOptions>('/prdb-videos/filter-options'),
    get: (id: string) => request<PrdbVideoDetail>(`/prdb-videos/${id}`),
  },
  prdbPreNames: {
    search: (params: { q?: string; releaseDateFrom?: string; releaseDateTo?: string }) => {
      const q = new URLSearchParams()
      if (params.q) q.set('q', params.q)
      if (params.releaseDateFrom) q.set('releaseDateFrom', params.releaseDateFrom)
      if (params.releaseDateTo) q.set('releaseDateTo', params.releaseDateTo)
      return request<PrdbPreNamesSearchResult>(`/prdb-prenames/search?${q}`)
    },
  },
  prdbPreDb: {
    list: (params?: { search?: string; siteId?: string; hasLinkedVideo?: boolean; page?: number; pageSize?: number }) => {
      const q = new URLSearchParams()
      if (params?.search) q.set('search', params.search)
      if (params?.siteId) q.set('siteId', params.siteId)
      if (params?.hasLinkedVideo !== undefined) q.set('hasLinkedVideo', String(params.hasLinkedVideo))
      if (params?.page) q.set('page', String(params.page))
      if (params?.pageSize) q.set('pageSize', String(params.pageSize))
      return request<PagedResult<PrdbPreDbEntry>>(`/prdb-predb?${q}`)
    },
    filterOptions: () => request<PrdbPreDbFilterOptions>('/prdb-predb/filter-options'),
  },
  prdbSync: {
    syncAll: () => request<{ networksUpserted: number; sitesUpserted: number; favoriteSitesSynced: number; favoriteActorsSynced: number; videosUpserted: number }>('/prdb-sync', { method: 'POST' }),
  },
  prdbStatus: {
    get: () => request<PrdbStatus>('/prdb-status'),
    runBackfill: () => request<void>('/prdb-status/backfill/run', { method: 'POST' }),
    runVideoDetailSync: () => request<void>('/prdb-status/video-detail-sync/run', { method: 'POST' }),
    runWantedVideoSync: () => request<void>('/prdb-status/wanted-video-sync/run', { method: 'POST' }),
    runPreNameSync: () => request<void>('/prdb-status/prename-sync/run', { method: 'POST' }),
    resetPreNameCursor: () => request<void>('/prdb-status/prename-sync/reset-cursor', { method: 'POST' }),
    runIndexerBackfill: (id: string) => request<void>(`/prdb-status/indexer-backfill/${id}/run`, { method: 'POST' }),
    runIndexerRowMatch: () => request<void>('/prdb-status/indexer-row-match/run', { method: 'POST' }),
    debugIndexerRowMatch: (search: string) => request<IndexerRowMatchDebugResult>('/prdb-status/indexer-row-match/debug', { method: 'POST', body: JSON.stringify({ search }) }),
  },
  settings: {
    get: () => request<AppSettings>('/settings'),
    update: (data: UpdateSettingsRequest) =>
      request<AppSettings>('/settings', { method: 'PUT', body: JSON.stringify(data) }),
    resetPrdbData: () =>
      request<void>('/settings/reset-prdb-data', { method: 'POST' }),
  },
  downloadLogs: {
    list: () => request<DownloadLog[]>('/download-logs'),
    get: (id: string) => request<DownloadLog>(`/download-logs/${id}`),
  },
  folderMappings: {
    list: () => request<FolderMapping[]>('/folder-mappings'),
    create: (data: FolderMappingRequest) =>
      request<FolderMapping>('/folder-mappings', { method: 'POST', body: JSON.stringify(data) }),
    update: (id: string, data: FolderMappingRequest) =>
      request<FolderMapping>(`/folder-mappings/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
    delete: (id: string) =>
      request<void>(`/folder-mappings/${id}`, { method: 'DELETE' }),
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
    send: (id: string, nzbUrl: string, name: string, indexerId: string, indexerRowId?: string) =>
      request<{ success: boolean; message: string; downloadLogId: string | null }>(`/download-clients/${id}/send`, {
        method: 'POST',
        body: JSON.stringify({ nzbUrl, name, indexerId, indexerRowId }),
      }),
  },
}
