import type {
  LayerSpecification,
  MapGeoJSONFeature,
  SourceSpecification,
} from 'maplibre-gl'

export const NETWORK_SOURCE_ID = 'geomap-network'
export const NETWORK_LAYER_ID = 'geomap-network-lines'
export const NETWORK_SOURCE_LAYER = 'network_features'
export const NETWORK_MIN_ZOOM = 14
export const NETWORK_MAX_ZOOM = 22

const UUID_PATTERN =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i

export interface NetworkEnvironment {
  readonly VITE_GEOMAP_API_BASE_URL?: string
  readonly VITE_GEOMAP_NETWORK_ID?: string
  readonly VITE_GEOMAP_PROJECT_ID?: string
}

export interface NetworkConfig {
  readonly apiBaseUrl: string
  readonly networkId: string
  readonly projectId: string
}

export type NetworkConfiguration =
  | { readonly kind: 'ready'; readonly value: NetworkConfig }
  | { readonly kind: 'missing'; readonly message: string }
  | { readonly kind: 'invalid'; readonly message: string }

export type NetworkStatus =
  | { readonly kind: 'loading'; readonly message: string }
  | { readonly kind: 'ready'; readonly message: string }
  | { readonly kind: 'empty'; readonly message: string }
  | { readonly kind: 'error'; readonly message: string }
  | { readonly kind: 'zoom-required'; readonly message: string }
  | { readonly kind: 'unconfigured'; readonly message: string }

interface NetworkMapEvent {
  readonly isSourceLoaded?: boolean
  readonly sourceId?: string
}

interface NetworkMapSubscription {
  unsubscribe(): void
}

export interface NetworkMapTarget {
  addLayer(layer: LayerSpecification): unknown
  addSource(id: string, source: SourceSpecification): unknown
  getZoom(): number
  on(
    event: 'error' | 'idle' | 'movestart' | 'sourcedata' | 'sourcedataloading',
    listener: (event: NetworkMapEvent) => void,
  ): NetworkMapSubscription
  queryRenderedFeatures(options: { layers: string[] }): MapGeoJSONFeature[]
}

export function loadNetworkConfiguration(
  environment: NetworkEnvironment,
): NetworkConfiguration {
  const projectId = environment.VITE_GEOMAP_PROJECT_ID?.trim() ?? ''
  const networkId = environment.VITE_GEOMAP_NETWORK_ID?.trim() ?? ''

  if (!projectId && !networkId) {
    return {
      kind: 'missing',
      message: 'Rede não configurada',
    }
  }

  if (!projectId || !networkId) {
    return {
      kind: 'invalid',
      message: 'Informe projeto e rede na configuração local',
    }
  }

  if (!UUID_PATTERN.test(projectId) || !UUID_PATTERN.test(networkId)) {
    return {
      kind: 'invalid',
      message: 'Projeto ou rede possui identificador inválido',
    }
  }

  try {
    return {
      kind: 'ready',
      value: {
        apiBaseUrl: normalizeApiBaseUrl(
          environment.VITE_GEOMAP_API_BASE_URL,
        ),
        projectId,
        networkId,
      },
    }
  } catch (error) {
    return {
      kind: 'invalid',
      message: error instanceof Error ? error.message : 'Configuração inválida',
    }
  }
}

export const networkConfiguration = loadNetworkConfiguration(import.meta.env)

export function buildNetworkTileUrl(config: NetworkConfig): string {
  return `${config.apiBaseUrl}/projects/${encodeURIComponent(config.projectId)}/networks/${encodeURIComponent(config.networkId)}/tiles/{z}/{x}/{y}.mvt`
}

export function buildNetworkSource(
  config: NetworkConfig,
): SourceSpecification {
  return {
    type: 'vector',
    tiles: [buildNetworkTileUrl(config)],
    minzoom: NETWORK_MIN_ZOOM,
    maxzoom: NETWORK_MAX_ZOOM,
  }
}

export function buildNetworkLayer(): LayerSpecification {
  return {
    id: NETWORK_LAYER_ID,
    type: 'line',
    source: NETWORK_SOURCE_ID,
    'source-layer': NETWORK_SOURCE_LAYER,
    minzoom: NETWORK_MIN_ZOOM,
    layout: {
      'line-cap': 'round',
      'line-join': 'round',
    },
    paint: {
      'line-color': '#d94a32',
      'line-opacity': 0.94,
      'line-width': [
        'interpolate',
        ['linear'],
        ['zoom'],
        NETWORK_MIN_ZOOM,
        2,
        18,
        4,
        NETWORK_MAX_ZOOM,
        7,
      ],
    },
  }
}

export function initialNetworkStatus(
  configuration: NetworkConfiguration,
): NetworkStatus {
  if (configuration.kind === 'missing') {
    return { kind: 'unconfigured', message: configuration.message }
  }
  if (configuration.kind === 'invalid') {
    return { kind: 'error', message: configuration.message }
  }
  return { kind: 'loading', message: 'Carregando rede…' }
}

export function installNetworkLayer(
  map: NetworkMapTarget,
  config: NetworkConfig,
  updateStatus: (status: NetworkStatus) => void,
): () => void {
  map.addSource(NETWORK_SOURCE_ID, buildNetworkSource(config))
  map.addLayer(buildNetworkLayer())
  let hasNetworkError = false

  const updateVisibleState = () => {
    if (hasNetworkError) {
      return
    }

    if (map.getZoom() < NETWORK_MIN_ZOOM) {
      updateStatus({
        kind: 'zoom-required',
        message: `Aproxime até o zoom ${NETWORK_MIN_ZOOM} para carregar a rede`,
      })
      return
    }

    const visibleFeatures = map.queryRenderedFeatures({
      layers: [NETWORK_LAYER_ID],
    })
    updateStatus(
      visibleFeatures.length > 0
        ? { kind: 'ready', message: 'Rede visível nesta área' }
        : { kind: 'empty', message: 'Nenhuma feição nesta área' },
    )
  }

  const loadingSubscription = map.on('sourcedataloading', (event) => {
    if (event.sourceId === NETWORK_SOURCE_ID) {
      hasNetworkError = false
      updateStatus({ kind: 'loading', message: 'Carregando rede…' })
    }
  })
  const dataSubscription = map.on('sourcedata', (event) => {
    if (event.sourceId === NETWORK_SOURCE_ID && event.isSourceLoaded) {
      updateVisibleState()
    }
  })
  const idleSubscription = map.on('idle', updateVisibleState)
  const moveSubscription = map.on('movestart', () => {
    hasNetworkError = false
    updateStatus(
      map.getZoom() < NETWORK_MIN_ZOOM
        ? {
            kind: 'zoom-required',
            message: `Aproxime até o zoom ${NETWORK_MIN_ZOOM} para carregar a rede`,
          }
        : { kind: 'loading', message: 'Carregando rede…' },
    )
  })
  const errorSubscription = map.on('error', (event) => {
    if (event.sourceId === NETWORK_SOURCE_ID) {
      hasNetworkError = true
      updateStatus({
        kind: 'error',
        message: 'Não foi possível carregar a rede',
      })
    }
  })

  updateStatus(
    map.getZoom() < NETWORK_MIN_ZOOM
      ? {
          kind: 'zoom-required',
          message: `Aproxime até o zoom ${NETWORK_MIN_ZOOM} para carregar a rede`,
        }
      : { kind: 'loading', message: 'Carregando rede…' },
  )

  return () => {
    loadingSubscription.unsubscribe()
    dataSubscription.unsubscribe()
    idleSubscription.unsubscribe()
    moveSubscription.unsubscribe()
    errorSubscription.unsubscribe()
  }
}

function normalizeApiBaseUrl(value: string | undefined): string {
  const normalized = value?.trim() || '/api'
  if (normalized.startsWith('/')) {
    if (normalized.includes('?') || normalized.includes('#')) {
      throw new Error('A URL da API não pode conter consulta ou fragmento')
    }
    return normalized.replace(/\/+$/, '') || '/api'
  }

  let parsed: URL
  try {
    parsed = new URL(normalized)
  } catch {
    throw new Error('A URL da API é inválida')
  }

  const localHttp =
    parsed.protocol === 'http:' &&
    ['localhost', '127.0.0.1', '[::1]'].includes(parsed.hostname)
  if (parsed.protocol !== 'https:' && !localHttp) {
    throw new Error('A URL da API deve usar HTTPS ou HTTP local')
  }
  if (parsed.search || parsed.hash) {
    throw new Error('A URL da API não pode conter consulta ou fragmento')
  }

  return normalized.replace(/\/+$/, '')
}
