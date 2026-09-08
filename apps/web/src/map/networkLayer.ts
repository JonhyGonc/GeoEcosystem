import type {
  LayerSpecification,
  MapGeoJSONFeature,
  SourceSpecification,
} from 'maplibre-gl'

export const NETWORK_SOURCE_LAYER = 'network_features'
export const NETWORK_MIN_ZOOM = 14
export const NETWORK_MAX_ZOOM = 22

const NETWORK_SOURCE_PREFIX = 'geomap-network-source-'
const NETWORK_LAYER_PREFIX = 'geomap-network-lines-'
const UUID_PATTERN =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i
const NETWORK_COLORS = [
  '#d94a32',
  '#167c80',
  '#7257a6',
  '#bd7423',
  '#3976ad',
  '#a33f68',
  '#4d7d38',
  '#8c5a3c',
] as const

export interface NetworkEnvironment {
  readonly VITE_GEOMAP_API_BASE_URL?: string
  readonly VITE_GEOMAP_NETWORK_ID?: string
  readonly VITE_GEOMAP_PROJECT_ID?: string
}

export interface NetworkCatalogConfig {
  readonly apiBaseUrl: string
  readonly initialNetworkId?: string
  readonly projectId: string
}

export interface NetworkDefinition {
  readonly id: string
  readonly name: string
  readonly type: string
}

export interface NetworkCatalogResponse {
  readonly projectId: string
  readonly networks: readonly NetworkDefinition[]
}

export type NetworkConfiguration =
  | { readonly kind: 'ready'; readonly value: NetworkCatalogConfig }
  | { readonly kind: 'missing'; readonly message: string }
  | { readonly kind: 'invalid'; readonly message: string }

export type NetworkLayerStatus =
  | { readonly kind: 'loading'; readonly message: string }
  | { readonly kind: 'ready'; readonly message: string }
  | { readonly kind: 'empty'; readonly message: string }
  | { readonly kind: 'error'; readonly message: string }
  | { readonly kind: 'zoom-required'; readonly message: string }
  | { readonly kind: 'hidden'; readonly message: string }

export interface NetworkLayerView extends NetworkDefinition {
  readonly color: string
  readonly status: NetworkLayerStatus
  readonly visible: boolean
}

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
  getLayer(id: string): unknown
  getSource(id: string): unknown
  getZoom(): number
  on(
    event: 'error' | 'idle' | 'movestart' | 'sourcedata' | 'sourcedataloading',
    listener: (event: NetworkMapEvent) => void,
  ): NetworkMapSubscription
  queryRenderedFeatures(options: { layers: string[] }): MapGeoJSONFeature[]
  removeLayer(id: string): unknown
  removeSource(id: string): unknown
  setLayoutProperty(
    layerId: string,
    name: 'visibility',
    value: 'none' | 'visible',
  ): unknown
}

export interface NetworkLayerController {
  cleanup(): void
  setVisibility(networkId: string, visible: boolean): void
}

export function loadNetworkConfiguration(
  environment: NetworkEnvironment,
): NetworkConfiguration {
  const projectId = environment.VITE_GEOMAP_PROJECT_ID?.trim() ?? ''
  const initialNetworkId = environment.VITE_GEOMAP_NETWORK_ID?.trim() || undefined

  if (!projectId) {
    return { kind: 'missing', message: 'Projeto não configurado' }
  }

  if (
    !UUID_PATTERN.test(projectId) ||
    (initialNetworkId && !UUID_PATTERN.test(initialNetworkId))
  ) {
    return {
      kind: 'invalid',
      message: 'Projeto ou rede inicial possui identificador inválido',
    }
  }

  try {
    return {
      kind: 'ready',
      value: {
        apiBaseUrl: normalizeApiBaseUrl(environment.VITE_GEOMAP_API_BASE_URL),
        projectId,
        initialNetworkId,
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

export function initialNetworkStatus(
  configuration: NetworkConfiguration,
): NetworkStatus {
  if (configuration.kind === 'missing') {
    return { kind: 'unconfigured', message: configuration.message }
  }
  if (configuration.kind === 'invalid') {
    return { kind: 'error', message: configuration.message }
  }
  return { kind: 'loading', message: 'Carregando camadas…' }
}

export function buildNetworkCatalogUrl(config: NetworkCatalogConfig): string {
  return `${config.apiBaseUrl}/projects/${encodeURIComponent(config.projectId)}/networks`
}

export async function fetchProjectNetworks(
  config: NetworkCatalogConfig,
  signal?: AbortSignal,
): Promise<readonly NetworkDefinition[]> {
  const response = await fetch(buildNetworkCatalogUrl(config), {
    headers: { Accept: 'application/json' },
    signal,
  })
  if (!response.ok) {
    throw new Error(`Catálogo de redes indisponível (${response.status})`)
  }

  const payload: unknown = await response.json()
  return validateNetworkCatalog(payload, config.projectId).networks
}

export function createNetworkLayerViews(
  networks: readonly NetworkDefinition[],
  initialNetworkId?: string,
): NetworkLayerView[] {
  const selectOne =
    initialNetworkId && networks.some(({ id }) => id === initialNetworkId)
  return networks.map((network) => {
    const visible = !selectOne || network.id === initialNetworkId
    return {
      ...network,
      color: colorForNetwork(network),
      visible,
      status: visible
        ? { kind: 'loading', message: 'Carregando…' }
        : { kind: 'hidden', message: 'Oculta' },
    }
  })
}

export function buildNetworkSourceId(networkId: string): string {
  return `${NETWORK_SOURCE_PREFIX}${networkId}`
}

export function buildNetworkLayerId(networkId: string): string {
  return `${NETWORK_LAYER_PREFIX}${networkId}`
}

export function buildNetworkTileUrl(
  config: NetworkCatalogConfig,
  networkId: string,
): string {
  return `${config.apiBaseUrl}/projects/${encodeURIComponent(config.projectId)}/networks/${encodeURIComponent(networkId)}/tiles/{z}/{x}/{y}.mvt`
}

export function buildNetworkSource(
  config: NetworkCatalogConfig,
  networkId: string,
): SourceSpecification {
  return {
    type: 'vector',
    tiles: [buildNetworkTileUrl(config, networkId)],
    minzoom: NETWORK_MIN_ZOOM,
    maxzoom: NETWORK_MAX_ZOOM,
  }
}

export function buildNetworkLayer(
  network: NetworkLayerView,
): LayerSpecification {
  return {
    id: buildNetworkLayerId(network.id),
    type: 'line',
    source: buildNetworkSourceId(network.id),
    'source-layer': NETWORK_SOURCE_LAYER,
    minzoom: NETWORK_MIN_ZOOM,
    layout: {
      'line-cap': 'round',
      'line-join': 'round',
      visibility: network.visible ? 'visible' : 'none',
    },
    paint: {
      'line-color': network.color,
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

export function installNetworkLayers(
  map: NetworkMapTarget,
  config: NetworkCatalogConfig,
  networks: readonly NetworkLayerView[],
  updateStatus: (networkId: string, status: NetworkLayerStatus) => void,
): NetworkLayerController {
  const visibility = new Map(
    networks.map((network) => [network.id, network.visible]),
  )
  const sourceToNetwork = new Map(
    networks.map((network) => [buildNetworkSourceId(network.id), network.id]),
  )
  const networkErrors = new Set<string>()

  for (const network of networks) {
    map.addSource(
      buildNetworkSourceId(network.id),
      buildNetworkSource(config, network.id),
    )
    map.addLayer(buildNetworkLayer(network))
  }

  const updateVisibleState = (network: NetworkLayerView) => {
    if (!visibility.get(network.id) || networkErrors.has(network.id)) return
    if (map.getZoom() < NETWORK_MIN_ZOOM) {
      updateStatus(network.id, {
        kind: 'zoom-required',
        message: `Zoom ${NETWORK_MIN_ZOOM}+`,
      })
      return
    }

    const features = map.queryRenderedFeatures({
      layers: [buildNetworkLayerId(network.id)],
    })
    updateStatus(
      network.id,
      features.length > 0
        ? { kind: 'ready', message: 'Visível' }
        : { kind: 'empty', message: 'Sem feições nesta área' },
    )
  }
  const updateVisibleStates = () => {
    for (const network of networks) updateVisibleState(network)
  }

  const loadingSubscription = map.on('sourcedataloading', (event) => {
    const networkId = event.sourceId && sourceToNetwork.get(event.sourceId)
    if (networkId && visibility.get(networkId)) {
      networkErrors.delete(networkId)
      updateStatus(networkId, { kind: 'loading', message: 'Carregando…' })
    }
  })
  const dataSubscription = map.on('sourcedata', (event) => {
    const networkId = event.sourceId && sourceToNetwork.get(event.sourceId)
    if (networkId && visibility.get(networkId) && event.isSourceLoaded) {
      const network = networks.find(({ id }) => id === networkId)
      if (network) updateVisibleState(network)
    }
  })
  const idleSubscription = map.on('idle', updateVisibleStates)
  const moveSubscription = map.on('movestart', () => {
    for (const network of networks) {
      if (!visibility.get(network.id)) continue
      networkErrors.delete(network.id)
      updateStatus(
        network.id,
        map.getZoom() < NETWORK_MIN_ZOOM
          ? { kind: 'zoom-required', message: `Zoom ${NETWORK_MIN_ZOOM}+` }
          : { kind: 'loading', message: 'Carregando…' },
      )
    }
  })
  const errorSubscription = map.on('error', (event) => {
    const networkId = event.sourceId && sourceToNetwork.get(event.sourceId)
    if (networkId && visibility.get(networkId)) {
      networkErrors.add(networkId)
      updateStatus(networkId, { kind: 'error', message: 'Falha ao carregar' })
    }
  })

  if (map.getZoom() < NETWORK_MIN_ZOOM) updateVisibleStates()

  return {
    setVisibility(networkId, visible) {
      if (!visibility.has(networkId)) return
      visibility.set(networkId, visible)
      networkErrors.delete(networkId)
      map.setLayoutProperty(
        buildNetworkLayerId(networkId),
        'visibility',
        visible ? 'visible' : 'none',
      )
      updateStatus(
        networkId,
        visible
          ? map.getZoom() < NETWORK_MIN_ZOOM
            ? { kind: 'zoom-required', message: `Zoom ${NETWORK_MIN_ZOOM}+` }
            : { kind: 'loading', message: 'Carregando…' }
          : { kind: 'hidden', message: 'Oculta' },
      )
    },
    cleanup() {
      loadingSubscription.unsubscribe()
      dataSubscription.unsubscribe()
      idleSubscription.unsubscribe()
      moveSubscription.unsubscribe()
      errorSubscription.unsubscribe()
      for (const network of [...networks].reverse()) {
        const layerId = buildNetworkLayerId(network.id)
        const sourceId = buildNetworkSourceId(network.id)
        if (map.getLayer(layerId)) map.removeLayer(layerId)
        if (map.getSource(sourceId)) map.removeSource(sourceId)
      }
    },
  }
}

export function summarizeNetworkLayers(
  networks: readonly NetworkLayerView[],
): NetworkStatus {
  if (networks.length === 0) {
    return { kind: 'empty', message: 'Este projeto não possui redes' }
  }
  const visible = networks.filter((network) => network.visible)
  if (visible.length === 0) {
    return { kind: 'empty', message: 'Todas as redes estão ocultas' }
  }
  if (visible.some((network) => network.status.kind === 'error')) {
    return { kind: 'error', message: 'Uma ou mais redes falharam' }
  }
  if (visible.some((network) => network.status.kind === 'loading')) {
    return { kind: 'loading', message: 'Carregando redes…' }
  }
  if (visible.every((network) => network.status.kind === 'zoom-required')) {
    return {
      kind: 'zoom-required',
      message: `Aproxime até o zoom ${NETWORK_MIN_ZOOM}`,
    }
  }
  if (visible.every((network) => network.status.kind === 'empty')) {
    return { kind: 'empty', message: 'Nenhuma feição visível nesta área' }
  }
  return {
    kind: 'ready',
    message: `${visible.length} de ${networks.length} redes visíveis`,
  }
}

function validateNetworkCatalog(
  payload: unknown,
  projectId: string,
): NetworkCatalogResponse {
  if (
    !isRecord(payload) ||
    payload.projectId !== projectId ||
    !Array.isArray(payload.networks)
  ) {
    throw new Error('Resposta inválida do catálogo de redes')
  }

  const ids = new Set<string>()
  const networks = payload.networks.map((item) => {
    if (
      !isRecord(item) ||
      typeof item.id !== 'string' ||
      !UUID_PATTERN.test(item.id) ||
      typeof item.name !== 'string' ||
      !item.name.trim() ||
      typeof item.type !== 'string' ||
      !item.type.trim() ||
      ids.has(item.id)
    ) {
      throw new Error('Resposta inválida do catálogo de redes')
    }
    ids.add(item.id)
    return { id: item.id, name: item.name.trim(), type: item.type.trim() }
  })

  return { projectId, networks }
}

function colorForNetwork(network: NetworkDefinition): string {
  let hash = 0
  for (const character of `${network.type}:${network.id}`) {
    hash = (hash * 31 + character.charCodeAt(0)) >>> 0
  }
  return NETWORK_COLORS[hash % NETWORK_COLORS.length]
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null
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
