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
  buildNetworkPointLayer,
  buildNetworkPointLayerId,
  buildNetworkPointSelectionLayer,
  buildNetworkPointSelectionLayerId,
  buildNetworkSelectionLayer,
  buildNetworkSelectionLayerId,
  buildNetworkSourceId,
  buildNetworkTileUrl,
  createNetworkLayerViews,
  fetchProjectNetworks,
  buildNetworkCatalogConfig,
  installNetworkLayers,
  loadNetworkConfiguration,
  summarizeNetworkLayers,
  type NetworkCatalogConfig,
  type NetworkDefinition,
  type NetworkLayerStatus,
  type NetworkMapTarget,
  type SelectedNetworkFeature,
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
  it('treats project and network ids as optional initial choices', () => {
    const configuration = loadNetworkConfiguration({
      VITE_GEOMAP_PROJECT_ID: projectId,
      VITE_GEOMAP_NETWORK_ID: networkId,
    })

    expect(configuration).toEqual({
      kind: 'ready',
      value: {
        apiBaseUrl: '/api',
        initialProjectId: projectId,
        initialNetworkId: networkId,
      },
    })
    expect(loadNetworkConfiguration({ VITE_GEOMAP_PROJECT_ID: projectId })).toMatchObject({
      kind: 'ready',
    })
    expect(loadNetworkConfiguration({})).toEqual({
      kind: 'ready',
      value: {
        apiBaseUrl: '/api',
        initialProjectId: undefined,
        initialNetworkId: undefined,
      },
    })
    expect(
      loadNetworkConfiguration({ VITE_GEOMAP_PROJECT_ID: 'invalid' }),
    ).toMatchObject({ kind: 'invalid' })
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
  })

  it('applies the initial network only when requested', () => {
    const config = {
      apiBaseUrl: '/api',
      initialProjectId: projectId,
      initialNetworkId: networkId,
    }

    expect(buildNetworkCatalogConfig(config, projectId, true)).toEqual(
      readyConfiguration(),
    )
    expect(buildNetworkCatalogConfig(config, otherProjectId, false)).toEqual({
      apiBaseUrl: '/api',
      projectId: otherProjectId,
      initialNetworkId: undefined,
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
    { projectId, networks: [{ ...networks[0], color: 'red' }] },
    { projectId, networks: [{ ...networks[0], color: '#12345' }] },
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

describe('CAD layer color', () => {
  it('keeps the CAD color from the catalog and ignores a null one', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          projectId,
          networks: [
            { ...networks[0], color: '#ff7f00' },
            { ...networks[1], color: null },
          ],
        }),
      }),
    )

    await expect(fetchProjectNetworks(readyConfiguration())).resolves.toEqual([
      { ...networks[0], color: '#FF7F00' },
      networks[1],
    ])
  })

  it('uses the CAD color instead of the automatic palette', () => {
    const [withColor, withoutColor] = createNetworkLayerViews([
      { ...networks[0], color: '#FF7F00' },
      networks[1],
    ])

    expect(withColor.color).toBe('#FF7F00')
    expect(withoutColor.color).toBe(createNetworkLayerViews([networks[1]])[0].color)
    expect(withoutColor.color).toMatch(/^#[0-9a-f]{6}$/i)
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
    expect(buildNetworkSelectionLayer(views[0])).toMatchObject({
      id: buildNetworkSelectionLayerId(networkId),
      source: buildNetworkSourceId(networkId),
      filter: ['==', ['get', 'id'], ''],
      layout: { visibility: 'visible' },
    })
    expect(buildNetworkLayer(views[0])).not.toHaveProperty('filter')
  })

  it('renders block points as colored circles limited to point geometries', () => {
    const views = createNetworkLayerViews([
      { ...networks[0], color: '#AA5500' },
    ])
    const pointGeometryFilter = [
      'match',
      ['geometry-type'],
      ['Point', 'MultiPoint'],
      true,
      false,
    ]

    expect(buildNetworkPointLayer(views[0])).toMatchObject({
      id: buildNetworkPointLayerId(networkId),
      type: 'circle',
      source: buildNetworkSourceId(networkId),
      'source-layer': 'network_features',
      minzoom: NETWORK_MIN_ZOOM,
      filter: pointGeometryFilter,
      layout: { visibility: 'visible' },
      paint: { 'circle-color': '#AA5500' },
    })
    expect(buildNetworkPointSelectionLayer(views[0])).toMatchObject({
      id: buildNetworkPointSelectionLayerId(networkId),
      type: 'circle',
      filter: ['all', pointGeometryFilter, ['==', ['get', 'id'], '']],
    })
  })

  it('toggles layers without recreating sources and keeps states coherent on pan', () => {
    let zoom = 15
    const sourceIds = new Set<string>()
    const layerIds = new Set<string>()
    const visibleFeatures = new Map<string, MapGeoJSONFeature[]>()
    let clickedFeatures: MapGeoJSONFeature[] = []
    const listeners = new Map<
      string,
      Array<
        (event: {
          sourceId?: string
          isSourceLoaded?: boolean
          point?: { x: number; y: number }
        }) => void
      >
    >()
    const addSource = vi.fn((id: string) => sourceIds.add(id))
    const addLayer = vi.fn((layer: LayerSpecification) => layerIds.add(layer.id))
    const removeLayer = vi.fn((id: string) => layerIds.delete(id))
    const removeSource = vi.fn((id: string) => sourceIds.delete(id))
    const setLayoutProperty = vi.fn()
    const setFilter = vi.fn()
    const updates: Array<{ id: string; status: NetworkLayerStatus }> = []
    const selections: Array<SelectedNetworkFeature | null> = []
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
      queryRenderedFeatures: (geometryOrOptions, options) => {
        if (options) return clickedFeatures
        const layers = 'layers' in geometryOrOptions ? geometryOrOptions.layers : []
        return layers.flatMap((layer) => visibleFeatures.get(layer) ?? [])
      },
      removeLayer,
      removeSource,
      setLayoutProperty,
      setFilter,
    }
    const emit = (
      event: string,
      data: {
        sourceId?: string
        isSourceLoaded?: boolean
        point?: { x: number; y: number }
      } = {},
    ) => listeners.get(event)?.forEach((listener) => listener(data))
    const views = createNetworkLayerViews(networks, networkId)
    const controller = installNetworkLayers(
      map,
      readyConfiguration(),
      views,
      (id, status) => updates.push({ id, status }),
      (selection) => selections.push(selection),
    )

    expect(addSource).toHaveBeenCalledTimes(2)
    expect(addLayer).toHaveBeenCalledTimes(8)

    controller.setVisibility(otherNetworkId, true)
    expect(setLayoutProperty).toHaveBeenCalledWith(
      buildNetworkLayerId(otherNetworkId),
      'visibility',
      'visible',
    )
    expect(setLayoutProperty).toHaveBeenCalledWith(
      buildNetworkSelectionLayerId(otherNetworkId),
      'visibility',
      'visible',
    )
    expect(addSource).toHaveBeenCalledTimes(2)
    expect(addLayer).toHaveBeenCalledTimes(8)

    clickedFeatures = [
      renderedFeature(buildNetworkLayerId(otherNetworkId), otherNetworkId),
      renderedFeature(buildNetworkLayerId(networkId), networkId),
    ]
    emit('click', { point: { x: 10, y: 20 } })
    expect(selections.at(-1)).toMatchObject({
      featureId: otherNetworkId,
      networkId: otherNetworkId,
      networkName: 'Rede de esgoto',
    })
    expect(setFilter).toHaveBeenCalledWith(
      buildNetworkSelectionLayerId(otherNetworkId),
      ['==', ['get', 'id'], otherNetworkId],
    )
    expect(setFilter).toHaveBeenCalledWith(
      buildNetworkPointSelectionLayerId(otherNetworkId),
      [
        'all',
        ['match', ['geometry-type'], ['Point', 'MultiPoint'], true, false],
        ['==', ['get', 'id'], otherNetworkId],
      ],
    )

    clickedFeatures = [
      renderedFeature(buildNetworkLayerId(networkId), networkId),
    ]
    emit('click', { point: { x: 10, y: 20 } })
    expect(selections.at(-1)?.networkId).toBe(networkId)

    clickedFeatures = [
      renderedFeature(buildNetworkPointLayerId(otherNetworkId), 'block-1'),
    ]
    emit('click', { point: { x: 10, y: 20 } })
    expect(selections.at(-1)).toMatchObject({
      featureId: 'block-1',
      networkId: otherNetworkId,
    })

    clickedFeatures = []
    emit('click', { point: { x: 10, y: 20 } })
    expect(selections.at(-1)).toBeNull()

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

    controller.setVisibility(otherNetworkId, true)
    clickedFeatures = [
      renderedFeature(buildNetworkLayerId(otherNetworkId), otherNetworkId),
    ]
    emit('click', { point: { x: 10, y: 20 } })
    controller.setVisibility(otherNetworkId, false)
    expect(selections.at(-1)).toBeNull()

    controller.clearSelection()
    expect(selections.at(-1)).toBeNull()

    zoom = 13
    emit('movestart')
    expect(updates.at(-1)).toEqual({
      id: networkId,
      status: { kind: 'zoom-required', message: 'Zoom 14+' },
    })

    controller.cleanup()
    expect(removeLayer).toHaveBeenCalledTimes(8)
    expect(removeSource).toHaveBeenCalledTimes(2)
    expect(unsubscribes).toHaveLength(6)
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
      setFilter: vi.fn(),
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
      buildNetworkPointSelectionLayerId(otherNetworkId),
      buildNetworkSelectionLayerId(otherNetworkId),
      buildNetworkPointLayerId(otherNetworkId),
      buildNetworkLayerId(otherNetworkId),
      buildNetworkPointSelectionLayerId(networkId),
      buildNetworkSelectionLayerId(networkId),
      buildNetworkPointLayerId(networkId),
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

function renderedFeature(layerId: string, featureId: string): MapGeoJSONFeature {
  return {
    layer: { id: layerId },
    properties: { id: featureId },
  } as unknown as MapGeoJSONFeature
}
