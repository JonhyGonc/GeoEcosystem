/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_BASE_MAP_ATTRIBUTION_TEXT?: string
  readonly VITE_BASE_MAP_ATTRIBUTION_URL?: string
  readonly VITE_BASE_MAP_MAX_ZOOM?: string
  readonly VITE_BASE_MAP_MIN_ZOOM?: string
  readonly VITE_BASE_MAP_NAME?: string
  readonly VITE_BASE_MAP_TILE_SIZE?: string
  readonly VITE_BASE_MAP_TILE_URL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
