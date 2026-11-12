import type {
  NetworkCatalogConfig,
  SelectedNetworkFeature,
} from '../map/networkLayer'

const UUID_PATTERN =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i

export interface FeatureDetails {
  readonly createdAt: string
  readonly id: string
  readonly networkId: string
  readonly networkName: string
  readonly networkType: string
  readonly projectId: string
  readonly properties: Readonly<Record<string, unknown>>
  readonly sourceId: string | null
  readonly sourceLayer: string | null
  readonly sourceType: string
}

export type FeatureDetailsState =
  | { readonly kind: 'idle' }
  | { readonly kind: 'loading' }
  | { readonly kind: 'ready'; readonly details: FeatureDetails }
  | { readonly kind: 'not-found' }
  | { readonly kind: 'error'; readonly message: string }

export function buildFeatureDetailsUrl(
  config: NetworkCatalogConfig,
  selection: SelectedNetworkFeature,
): string {
  return `${config.apiBaseUrl}/projects/${encodeURIComponent(config.projectId)}/networks/${encodeURIComponent(selection.networkId)}/features/${encodeURIComponent(selection.featureId)}`
}

export async function fetchFeatureDetails(
  config: NetworkCatalogConfig,
  selection: SelectedNetworkFeature,
  signal?: AbortSignal,
): Promise<FeatureDetails | null> {
  const response = await fetch(buildFeatureDetailsUrl(config, selection), {
    headers: { Accept: 'application/json' },
    signal,
  })
  if (response.status === 404) return null
  if (!response.ok) {
    throw new Error(`Atributos indisponíveis (${response.status})`)
  }

  const payload: unknown = await response.json()
  return validateFeatureDetails(payload, config, selection)
}

function validateFeatureDetails(
  payload: unknown,
  config: NetworkCatalogConfig,
  selection: SelectedNetworkFeature,
): FeatureDetails {
  if (
    !isRecord(payload) ||
    payload.projectId !== config.projectId ||
    payload.id !== selection.featureId ||
    payload.networkId !== selection.networkId ||
    typeof payload.networkName !== 'string' ||
    !payload.networkName.trim() ||
    typeof payload.networkType !== 'string' ||
    !payload.networkType.trim() ||
    typeof payload.sourceType !== 'string' ||
    !payload.sourceType.trim() ||
    (payload.sourceId !== null && typeof payload.sourceId !== 'string') ||
    (payload.sourceLayer !== null && typeof payload.sourceLayer !== 'string') ||
    typeof payload.createdAt !== 'string' ||
    Number.isNaN(Date.parse(payload.createdAt)) ||
    !isRecord(payload.properties) ||
    !UUID_PATTERN.test(payload.projectId) ||
    !UUID_PATTERN.test(payload.id) ||
    !UUID_PATTERN.test(payload.networkId)
  ) {
    throw new Error('Resposta inválida dos atributos da feição')
  }

  return {
    projectId: payload.projectId,
    id: payload.id,
    networkId: payload.networkId,
    networkName: payload.networkName.trim(),
    networkType: payload.networkType.trim(),
    sourceId: payload.sourceId,
    sourceLayer: payload.sourceLayer,
    sourceType: payload.sourceType.trim(),
    properties: payload.properties,
    createdAt: payload.createdAt,
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}
