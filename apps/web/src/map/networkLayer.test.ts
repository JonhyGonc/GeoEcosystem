import type {
  LayerSpecification,
  MapGeoJSONFeature,
  SourceSpecification,
} from 'maplibre-gl'
import { describe, expect, it, vi } from 'vitest'
import {
  NETWORK_LAYER_ID,
  NETWORK_MAX_ZOOM,
  NETWORK_MIN_ZOOM,
  NETWORK_SOURCE_ID,
  buildNetworkLayer,
  buildNetworkSource,
  buildNetworkTileUrl,
  initialNetworkStatus,
  installNetworkLayer,
  loadNetworkConfiguration,
  type NetworkMapTarget,
} from './networkLayer'

const projectId = '60000000-0000-4000-8000-000000000001'
const networkId = '61000000-0000-4000-8000-000000000001'

function readyConfiguration() {
  return loadNetworkConfiguration({
    VITE_GEOMAP_PROJECT_ID: projectId,
    VITE_GEOMAP_NETWORK_ID: networkId,
  })
}

describe('network configuration', () => {
  it('builds the exact regional MVT endpoint for one project and network', () => {
    const configuration = readyConfiguration()
    expect(configuration.kind).toBe('ready')
    if (configuration.kind !== 'ready') return

    const tileUrl = buildNetworkTileUrl(configuration.value)
    const source = buildNetworkSource(configuration.value)
    const layer = buildNetworkLayer()

    expect(tileUrl).toBe(
      `/api/projects/${projectId}/networks/${networkId}/tiles/{z}/{x}/{y}.mvt`,
    )
    expect(tileUrl).not.toContain('/features')
    expect(source).toMatchObject({
      type: 'vector',
      tiles: [tileUrl],
      minzoom: NETWORK_MIN_ZOOM,
      maxzoom: NETWORK_MAX_ZOOM,
    })
    expect(layer).toMatchObject({
      id: NETWORK_LAYER_ID,
      type: 'line',
      source: NETWORK_SOURCE_ID,
      'source-layer': 'network_features',
      minzoom: NETWORK_MIN_ZOOM,
    })
  })

  it.each([
    [{}, 'missing'],
    [{ VITE_GEOMAP_PROJECT_ID: projectId }, 'invalid'],
    [
      {
        VITE_GEOMAP_PROJECT_ID: 'not-a-uuid',
        VITE_GEOMAP_NETWORK_ID: networkId,
      },
      'invalid',
    ],
    [
      {
        VITE_GEOMAP_API_BASE_URL: 'http://example.com/api',
        VITE_GEOMAP_PROJECT_ID: projectId,
        VITE_GEOMAP_NETWORK_ID: networkId,
      },
      'invalid',
    ],
  ])('returns %s configuration state without breaking the map', (env, kind) => {
    expect(loadNetworkConfiguration(env).kind).toBe(kind)
  })

  it('maps missing and invalid configuration to safe UI states', () => {
    expect(initialNetworkStatus(loadNetworkConfiguration({}))).toMatchObject({
      kind: 'unconfigured',
    })
    expect(
      initialNetworkStatus(
        loadNetworkConfiguration({
          VITE_GEOMAP_PROJECT_ID: 'invalid',
          VITE_GEOMAP_NETWORK_ID: networkId,
        }),
      ),
    ).toMatchObject({ kind: 'error' })
  })
})

describe('network map state', () => {
  it('handles zoom, pan, empty tiles, loaded features and endpoint errors', () => {
    let zoom = 13
    let features: MapGeoJSONFeature[] = []
    const listeners = new Map<
      string,
      Array<(event: { sourceId?: string; isSourceLoaded?: boolean }) => void>
    >()
    const addSource = vi.fn<(id: string, source: SourceSpecification) => void>()
    const addLayer = vi.fn<(layer: LayerSpecification) => void>()
    const updateStatus = vi.fn()
    const unsubscribes: ReturnType<typeof vi.fn>[] = []
    const map: NetworkMapTarget = {
      addLayer,
      addSource,
      getZoom: () => zoom,
      on: (event, listener) => {
        listeners.set(event, [...(listeners.get(event) ?? []), listener])
        const unsubscribe = vi.fn()
        unsubscribes.push(unsubscribe)
        return { unsubscribe }
      },
      queryRenderedFeatures: () => features,
    }
    const emit = (
      event: string,
      data: { sourceId?: string; isSourceLoaded?: boolean } = {},
    ) => listeners.get(event)?.forEach((listener) => listener(data))
    const configuration = readyConfiguration()
    if (configuration.kind !== 'ready') throw new Error('Test setup failed')

    const cleanup = installNetworkLayer(
      map,
      configuration.value,
      updateStatus,
    )

    expect(addSource).toHaveBeenCalledWith(
      NETWORK_SOURCE_ID,
      expect.objectContaining({ minzoom: 14, maxzoom: 22 }),
    )
    expect(addLayer).toHaveBeenCalledWith(
      expect.objectContaining({ id: NETWORK_LAYER_ID }),
    )
    expect(updateStatus).toHaveBeenLastCalledWith(
      expect.objectContaining({ kind: 'zoom-required' }),
    )

    zoom = 15
    emit('movestart')
    expect(updateStatus).toHaveBeenLastCalledWith(
      expect.objectContaining({ kind: 'loading' }),
    )

    emit('idle')
    expect(updateStatus).toHaveBeenLastCalledWith(
      expect.objectContaining({ kind: 'empty' }),
    )

    features = [{} as MapGeoJSONFeature]
    emit('sourcedata', {
      sourceId: NETWORK_SOURCE_ID,
      isSourceLoaded: true,
    })
    expect(updateStatus).toHaveBeenLastCalledWith(
      expect.objectContaining({ kind: 'ready' }),
    )

    emit('error', { sourceId: 'basemap-raster-source' })
    expect(updateStatus).toHaveBeenLastCalledWith(
      expect.objectContaining({ kind: 'ready' }),
    )
    emit('error', { sourceId: NETWORK_SOURCE_ID })
    expect(updateStatus).toHaveBeenLastCalledWith(
      expect.objectContaining({ kind: 'error' }),
    )
    emit('idle')
    expect(updateStatus).toHaveBeenLastCalledWith(
      expect.objectContaining({ kind: 'error' }),
    )

    cleanup()
    expect(unsubscribes).toHaveLength(5)
    unsubscribes.forEach((unsubscribe) => {
      expect(unsubscribe).toHaveBeenCalledOnce()
    })
  })
})
