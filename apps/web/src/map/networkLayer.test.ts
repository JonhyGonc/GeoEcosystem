import type {
  LayerSpecification,
  MapGeoJSONFeature,
  SourceSpecification,
} from 'maplibre-gl'
import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  NETWORK_MAX_ZOOM,
  NETWORK_MIN_ZOOM,
  buildNetworkCatalogUrl,
  buildNetworkLayer,
  buildNetworkLayerId,
  buildNetworkSourceId,
  buildNetworkTileUrl,
  createNetworkLayerViews,
  fetchProjectNetworks,
  initialNetworkStatus,
  installNetworkLayers,
  loadNetworkConfiguration,
  summarizeNetworkLayers,
  type NetworkCatalogConfig,
  type NetworkDefinition,
  type NetworkLayerStatus,
  type NetworkMapTarget,
} from './networkLayer'

const projectId = '60000000-0000-4000-8000-000000000001'
const otherProjectId = '60000000-0000-4000-8000-000000000002'
const networkId = '61000000-0000-4000-8000-000000000001'
const otherNetworkId = '61000000-0000-4000-8000-000000000002'

const networks: NetworkDefinition[] = [
  { id: networkId, name: 'Rede de drenagem', type: 'stormwater' },
  { id: otherNetworkId, name: 'Rede de esgoto', type: 'sewer' },
]

function readyConfiguration(project = projectId): NetworkCatalogConfig {
  return { apiBaseUrl: '/api', projectId: project, initialNetworkId: networkId }
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('network catalog configuration', () => {
  it('requires one project and keeps the former network id as an optional initial layer', () => {
    const configuration = loadNetworkConfiguration({
      VITE_GEOMAP_PROJECT_ID: projectId,
      VITE_GEOMAP_NETWORK_ID: networkId,
    })

    expect(configuration).toEqual({
      kind: 'ready',
      value: readyConfiguration(),
    })
    expect(loadNetworkConfiguration({ VITE_GEOMAP_PROJECT_ID: projectId })).toMatchObject({
      kind: 'ready',
    })
    expect(loadNetworkConfiguration({})).toMatchObject({ kind: 'missing' })
    expect(
      loadNetworkConfiguration({
        VITE_GEOMAP_PROJECT_ID: projectId,
        VITE_GEOMAP_NETWORK_ID: 'invalid',
      }),
    ).toMatchObject({ kind: 'invalid' })
    expect(
      loadNetworkConfiguration({
        VITE_GEOMAP_API_BASE_URL: 'http://example.com/api',
        VITE_GEOMAP_PROJECT_ID: projectId,
      }),
    ).toMatchObject({ kind: 'invalid' })
    expect(initialNetworkStatus(loadNetworkConfiguration({}))).toEqual({
      kind: 'unconfigured',
      message: 'Projeto não configurado',
    })
  })

  it('fetches only the selected project catalog and validates the response', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ projectId, networks }),
    })
    vi.stubGlobal('fetch', fetchMock)

    await expect(fetchProjectNetworks(readyConfiguration())).resolves.toEqual(networks)
    expect(buildNetworkCatalogUrl(readyConfiguration())).toBe(
      `/api/projects/${projectId}/networks`,
    )
    expect(fetchMock).toHaveBeenCalledWith(
      `/api/projects/${projectId}/networks`,
      expect.objectContaining({ headers: { Accept: 'application/json' } }),
    )
  })

  it.each([
    { projectId: otherProjectId, networks },
    { projectId, networks: [...networks, networks[0]] },
    { projectId, networks: [{ id: 'invalid', name: 'Rede', type: 'test' }] },
  ])('rejects an invalid or cross-project catalog: %o', async (payload) => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({ ok: true, json: async () => payload }),
    )

    await expect(fetchProjectNetworks(readyConfiguration())).rejects.toThrow(
      'Resposta inválida do catálogo de redes',
    )
  })
})

describe('network layer control', () => {
  it('builds one regional source and simple style for every network', () => {
    const views = createNetworkLayerViews(networks, networkId)

    expect(views.map(({ visible }) => visible)).toEqual([true, false])
    expect(views[0].color).toMatch(/^#[0-9a-f]{6}$/i)
    expect(buildNetworkTileUrl(readyConfiguration(), otherNetworkId)).toBe(
      `/api/projects/${projectId}/networks/${otherNetworkId}/tiles/{z}/{x}/{y}.mvt`,
    )
    expect(buildNetworkTileUrl(readyConfiguration(), otherNetworkId)).not.toContain(
      '/features',
    )
    expect(buildNetworkLayer(views[1])).toMatchObject({
      id: buildNetworkLayerId(otherNetworkId),
      source: buildNetworkSourceId(otherNetworkId),
      'source-layer': 'network_features',
      minzoom: NETWORK_MIN_ZOOM,
      layout: { visibility: 'none' },
    })
  })

  it('toggles layers without recreating sources and keeps states coherent on pan', () => {
    let zoom = 15
    const sourceIds = new Set<string>()
    const layerIds = new Set<string>()
    const visibleFeatures = new Map<string, MapGeoJSONFeature[]>()
    const listeners = new Map<string, Array<(event: { sourceId?: string; isSourceLoaded?: boolean }) => void>>()
    const addSource = vi.fn((id: string) => sourceIds.add(id))
    const addLayer = vi.fn((layer: LayerSpecification) => layerIds.add(layer.id))
    const removeLayer = vi.fn((id: string) => layerIds.delete(id))
    const removeSource = vi.fn((id: string) => sourceIds.delete(id))
    const setLayoutProperty = vi.fn()
    const updates: Array<{ id: string; status: NetworkLayerStatus }> = []
    const unsubscribes: ReturnType<typeof vi.fn>[] = []
    const map: NetworkMapTarget = {
      addLayer,
      addSource: addSource as unknown as (id: string, source: SourceSpecification) => unknown,
      getLayer: (id) => (layerIds.has(id) ? {} : undefined),
      getSource: (id) => (sourceIds.has(id) ? {} : undefined),
      getZoom: () => zoom,
      on: (event, listener) => {
        listeners.set(event, [...(listeners.get(event) ?? []), listener])
        const unsubscribe = vi.fn()
        unsubscribes.push(unsubscribe)
        return { unsubscribe }
      },
      queryRenderedFeatures: ({ layers }) => visibleFeatures.get(layers[0]) ?? [],
      removeLayer,
      removeSource,
      setLayoutProperty,
    }
    const emit = (
      event: string,
      data: { sourceId?: string; isSourceLoaded?: boolean } = {},
    ) => listeners.get(event)?.forEach((listener) => listener(data))
    const views = createNetworkLayerViews(networks, networkId)
    const controller = installNetworkLayers(
      map,
      readyConfiguration(),
      views,
      (id, status) => updates.push({ id, status }),
    )

    expect(addSource).toHaveBeenCalledTimes(2)
    expect(addLayer).toHaveBeenCalledTimes(2)

    controller.setVisibility(otherNetworkId, true)
    expect(setLayoutProperty).toHaveBeenCalledWith(
      buildNetworkLayerId(otherNetworkId),
      'visibility',
      'visible',
    )
    expect(addSource).toHaveBeenCalledTimes(2)
    expect(addLayer).toHaveBeenCalledTimes(2)

    emit('movestart')
    expect(updates.filter(({ status }) => status.kind === 'loading')).toHaveLength(3)

    visibleFeatures.set(buildNetworkLayerId(networkId), [])
    visibleFeatures.set(buildNetworkLayerId(otherNetworkId), [
      {} as MapGeoJSONFeature,
    ])
    emit('idle')
    expect(updates).toContainEqual({
      id: networkId,
      status: { kind: 'empty', message: 'Sem feições nesta área' },
    })
    expect(updates).toContainEqual({
      id: otherNetworkId,
      status: { kind: 'ready', message: 'Visível' },
    })

    emit('error', { sourceId: 'basemap-raster-source' })
    emit('error', { sourceId: buildNetworkSourceId(otherNetworkId) })
    expect(updates.at(-1)).toEqual({
      id: otherNetworkId,
      status: { kind: 'error', message: 'Falha ao carregar' },
    })
    emit('idle')
    expect(
      updates.filter(({ id }) => id === otherNetworkId).at(-1)?.status.kind,
    ).toBe('error')

    controller.setVisibility(otherNetworkId, false)
    expect(updates.at(-1)).toEqual({
      id: otherNetworkId,
      status: { kind: 'hidden', message: 'Oculta' },
    })

    zoom = 13
    emit('movestart')
    expect(updates.at(-1)).toEqual({
      id: networkId,
      status: { kind: 'zoom-required', message: 'Zoom 14+' },
    })

    controller.cleanup()
    expect(removeLayer).toHaveBeenCalledTimes(2)
    expect(removeSource).toHaveBeenCalledTimes(2)
    expect(unsubscribes).toHaveLength(5)
    unsubscribes.forEach((unsubscribe) => expect(unsubscribe).toHaveBeenCalledOnce())
  })

  it('removes the former project layers before installing another project', () => {
    const removedLayers: string[] = []
    const addedSources: Array<{ id: string; source: SourceSpecification }> = []
    const sourceIds = new Set<string>()
    const layerIds = new Set<string>()
    const map: NetworkMapTarget = {
      addLayer: (layer) => layerIds.add(layer.id),
      addSource: (id, source) => {
        sourceIds.add(id)
        addedSources.push({ id, source })
      },
      getLayer: (id) => (layerIds.has(id) ? {} : undefined),
      getSource: (id) => (sourceIds.has(id) ? {} : undefined),
      getZoom: () => 15,
      on: () => ({ unsubscribe: vi.fn() }),
      queryRenderedFeatures: () => [],
      removeLayer: (id) => {
        removedLayers.push(id)
        layerIds.delete(id)
      },
      removeSource: (id) => sourceIds.delete(id),
      setLayoutProperty: vi.fn(),
    }
    const former = installNetworkLayers(
      map,
      readyConfiguration(),
      createNetworkLayerViews(networks),
      vi.fn(),
    )
    former.cleanup()

    const nextNetwork = {
      id: '61000000-0000-4000-8000-000000000003',
      name: 'Rede do outro projeto',
      type: 'test',
    }
    installNetworkLayers(
      map,
      { apiBaseUrl: '/api', projectId: otherProjectId },
      createNetworkLayerViews([nextNetwork]),
      vi.fn(),
    )

    expect(removedLayers).toEqual([
      buildNetworkLayerId(otherNetworkId),
      buildNetworkLayerId(networkId),
    ])
    expect(addedSources.at(-1)?.source).toMatchObject({
      tiles: [
        `/api/projects/${otherProjectId}/networks/${nextNetwork.id}/tiles/{z}/{x}/{y}.mvt`,
      ],
      minzoom: NETWORK_MIN_ZOOM,
      maxzoom: NETWORK_MAX_ZOOM,
    })
  })

  it('summarizes combined, hidden, empty and error states', () => {
    const views = createNetworkLayerViews(networks)
    expect(summarizeNetworkLayers(views).kind).toBe('loading')
    expect(
      summarizeNetworkLayers(
        views.map((layer) => ({
          ...layer,
          visible: false,
          status: { kind: 'hidden', message: 'Oculta' },
        })),
      ),
    ).toMatchObject({ kind: 'empty', message: 'Todas as redes estão ocultas' })
    expect(
      summarizeNetworkLayers([
        { ...views[0], status: { kind: 'empty', message: 'Vazia' } },
        { ...views[1], status: { kind: 'error', message: 'Falha' } },
      ]),
    ).toMatchObject({ kind: 'error' })
    expect(summarizeNetworkLayers([])).toMatchObject({ kind: 'empty' })
  })
})
