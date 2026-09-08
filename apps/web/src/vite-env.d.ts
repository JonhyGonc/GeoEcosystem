/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_BASE_MAP_ATTRIBUTION_TEXT?: string
  readonly VITE_BASE_MAP_ATTRIBUTION_URL?: string
  readonly VITE_BASE_MAP_MAX_ZOOM?: string
  readonly VITE_BASE_MAP_MIN_ZOOM?: string
  readonly VITE_BASE_MAP_NAME?: string
  readonly VITE_BASE_MAP_TILE_SIZE?: string
  readonly VITE_BASE_MAP_TILE_URL?: string
  readonly VITE_GEOMAP_API_BASE_URL?: string
  readonly VITE_GEOMAP_NETWORK_ID?: string
  readonly VITE_GEOMAP_PROJECT_ID?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
