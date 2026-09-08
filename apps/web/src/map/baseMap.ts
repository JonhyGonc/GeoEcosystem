import type {
  LayerSpecification,
  SourceSpecification,
  StyleSpecification,
} from 'maplibre-gl'

export const BASE_MAP_SOURCE_ID = 'basemap-raster-source'
export const BASE_MAP_BACKGROUND_LAYER_ID = 'basemap-background'
export const BASE_MAP_RASTER_LAYER_ID = 'basemap-raster'
export const GEOMAP_LAYER_PREFIX = 'geomap-'

const DEFAULT_TILE_URL = 'https://tile.openstreetmap.org/{z}/{x}/{y}.png'
const DEFAULT_ATTRIBUTION_TEXT = '© OpenStreetMap contributors'
const DEFAULT_ATTRIBUTION_URL = 'https://www.openstreetmap.org/copyright'

export interface BaseMapEnvironment {
  readonly VITE_BASE_MAP_ATTRIBUTION_TEXT?: string
  readonly VITE_BASE_MAP_ATTRIBUTION_URL?: string
  readonly VITE_BASE_MAP_MAX_ZOOM?: string
  readonly VITE_BASE_MAP_MIN_ZOOM?: string
  readonly VITE_BASE_MAP_NAME?: string
  readonly VITE_BASE_MAP_TILE_SIZE?: string
  readonly VITE_BASE_MAP_TILE_URL?: string
}

export interface BaseMapConfig {
  readonly attribution: {
    readonly text: string
    readonly url: string
  }
  readonly backgroundColor: string
  readonly kind: 'raster-xyz'
  readonly maxZoom: number
  readonly minZoom: number
  readonly name: string
  readonly tileSize: 256 | 512
  readonly tiles: readonly string[]
}

interface BaseMapTarget {
  addLayer(layer: LayerSpecification, beforeId?: string): unknown
  addSource(id: string, source: SourceSpecification): unknown
  getLayer(id: string): unknown
  getSource(id: string): unknown
  getStyle(): StyleSpecification
  removeLayer(id: string): unknown
  removeSource(id: string): unknown
}

export function loadBaseMapConfig(
  environment: BaseMapEnvironment,
): BaseMapConfig {
  const tileUrl = valueOrDefault(
    environment.VITE_BASE_MAP_TILE_URL,
    DEFAULT_TILE_URL,
  )
  const usesCustomTileUrl = tileUrl !== DEFAULT_TILE_URL
  const attributionText = requiredCustomValue(
    environment.VITE_BASE_MAP_ATTRIBUTION_TEXT,
    DEFAULT_ATTRIBUTION_TEXT,
    usesCustomTileUrl,
    'VITE_BASE_MAP_ATTRIBUTION_TEXT',
  )
  const attributionUrl = requiredCustomValue(
    environment.VITE_BASE_MAP_ATTRIBUTION_URL,
    DEFAULT_ATTRIBUTION_URL,
    usesCustomTileUrl,
    'VITE_BASE_MAP_ATTRIBUTION_URL',
  )

  const config: BaseMapConfig = {
    attribution: {
      text: attributionText,
      url: attributionUrl,
    },
    backgroundColor: '#dce7eb',
    kind: 'raster-xyz',
    maxZoom: readInteger(
      environment.VITE_BASE_MAP_MAX_ZOOM,
      19,
      'VITE_BASE_MAP_MAX_ZOOM',
    ),
    minZoom: readInteger(
      environment.VITE_BASE_MAP_MIN_ZOOM,
      0,
      'VITE_BASE_MAP_MIN_ZOOM',
    ),
    name: valueOrDefault(
      environment.VITE_BASE_MAP_NAME,
      usesCustomTileUrl ? 'Mapa base configurado' : 'OpenStreetMap Standard',
    ),
    tileSize: readTileSize(environment.VITE_BASE_MAP_TILE_SIZE),
    tiles: [tileUrl],
  }

  validateBaseMapConfig(config)
  return config
}

export const baseMapConfig = loadBaseMapConfig(import.meta.env)

export function buildBaseMapStyle(config: BaseMapConfig): StyleSpecification {
  validateBaseMapConfig(config)

  return {
    version: 8,
    sources: {
      [BASE_MAP_SOURCE_ID]: buildRasterSource(config),
    },
    layers: [buildBackgroundLayer(config), buildRasterLayer(config)],
  }
}

export function replaceBaseMap(
  map: BaseMapTarget,
  config: BaseMapConfig,
): void {
  validateBaseMapConfig(config)

  const firstGeoMapLayerId = map
    .getStyle()
    .layers?.find((layer) => layer.id.startsWith(GEOMAP_LAYER_PREFIX))?.id

  for (const layerId of [
    BASE_MAP_RASTER_LAYER_ID,
    BASE_MAP_BACKGROUND_LAYER_ID,
  ]) {
    if (map.getLayer(layerId)) {
      map.removeLayer(layerId)
    }
  }

  if (map.getSource(BASE_MAP_SOURCE_ID)) {
    map.removeSource(BASE_MAP_SOURCE_ID)
  }

  map.addSource(BASE_MAP_SOURCE_ID, buildRasterSource(config))
  map.addLayer(buildBackgroundLayer(config), firstGeoMapLayerId)
  map.addLayer(buildRasterLayer(config), firstGeoMapLayerId)
}

export function validateBaseMapConfig(config: BaseMapConfig): void {
  if (!config.name.trim()) {
    throw new Error('O nome do mapa base é obrigatório.')
  }

  if (config.kind !== 'raster-xyz') {
    throw new Error('Somente fontes raster XYZ são suportadas nesta etapa.')
  }

  if (config.tiles.length === 0) {
    throw new Error('Ao menos uma URL de tile é obrigatória.')
  }

  for (const tileUrl of config.tiles) {
    validateUrl(tileUrl, 'URL de tile')
    for (const placeholder of ['{z}', '{x}', '{y}']) {
      if (!tileUrl.includes(placeholder)) {
        throw new Error(`A URL de tile deve conter ${placeholder}.`)
      }
    }
  }

  if (config.tileSize !== 256 && config.tileSize !== 512) {
    throw new Error('O tamanho do tile deve ser 256 ou 512 pixels.')
  }

  if (
    !Number.isInteger(config.minZoom) ||
    !Number.isInteger(config.maxZoom) ||
    config.minZoom < 0 ||
    config.maxZoom > 24 ||
    config.minZoom > config.maxZoom
  ) {
    throw new Error('Os limites de zoom devem ser inteiros entre 0 e 24.')
  }

  if (!config.attribution.text.trim()) {
    throw new Error('A atribuição do mapa base é obrigatória.')
  }
  validateUrl(config.attribution.url, 'URL de atribuição')
}

function buildRasterSource(config: BaseMapConfig): SourceSpecification {
  return {
    type: 'raster',
    tiles: [...config.tiles],
    tileSize: config.tileSize,
    minzoom: config.minZoom,
    maxzoom: config.maxZoom,
    scheme: 'xyz',
    attribution: `<a href="${escapeHtml(config.attribution.url)}">${escapeHtml(config.attribution.text)}</a>`,
  }
}

function buildBackgroundLayer(config: BaseMapConfig): LayerSpecification {
  return {
    id: BASE_MAP_BACKGROUND_LAYER_ID,
    type: 'background',
    paint: { 'background-color': config.backgroundColor },
  }
}

function buildRasterLayer(config: BaseMapConfig): LayerSpecification {
  return {
    id: BASE_MAP_RASTER_LAYER_ID,
    type: 'raster',
    source: BASE_MAP_SOURCE_ID,
    minzoom: config.minZoom,
    maxzoom: config.maxZoom,
  }
}

function valueOrDefault(value: string | undefined, fallback: string): string {
  return value?.trim() || fallback
}

function requiredCustomValue(
  value: string | undefined,
  fallback: string,
  customTileUrl: boolean,
  variableName: string,
): string {
  const normalized = value?.trim()
  if (customTileUrl && !normalized) {
    throw new Error(`${variableName} é obrigatória para uma fonte personalizada.`)
  }
  return normalized || fallback
}

function readInteger(
  value: string | undefined,
  fallback: number,
  variableName: string,
): number {
  if (!value?.trim()) {
    return fallback
  }

  const parsed = Number(value)
  if (!Number.isInteger(parsed)) {
    throw new Error(`${variableName} deve ser um número inteiro.`)
  }
  return parsed
}

function readTileSize(value: string | undefined): 256 | 512 {
  const parsed = readInteger(value, 256, 'VITE_BASE_MAP_TILE_SIZE')
  if (parsed !== 256 && parsed !== 512) {
    throw new Error('VITE_BASE_MAP_TILE_SIZE deve ser 256 ou 512.')
  }
  return parsed
}

function validateUrl(value: string, label: string): void {
  let parsed: URL
  try {
    parsed = new URL(value)
  } catch {
    throw new Error(`${label} inválida.`)
  }

  const localHttp =
    parsed.protocol === 'http:' &&
    ['localhost', '127.0.0.1', '[::1]'].includes(parsed.hostname)
  if (parsed.protocol !== 'https:' && !localHttp) {
    throw new Error(`${label} deve usar HTTPS ou HTTP local.`)
  }
}

function escapeHtml(value: string): string {
  return value.replace(
    /[&<>'"]/g,
    (character) =>
      ({
        '&': '&amp;',
        '<': '&lt;',
        '>': '&gt;',
        "'": '&#39;',
        '"': '&quot;',
      })[character]!,
  )
}
