import type { LayerSpecification, SourceSpecification } from 'maplibre-gl'
import { describe, expect, it, vi } from 'vitest'
import {
  BASE_MAP_BACKGROUND_LAYER_ID,
  BASE_MAP_RASTER_LAYER_ID,
  BASE_MAP_SOURCE_ID,
  buildBaseMapStyle,
  loadBaseMapConfig,
  replaceBaseMap,
  type BaseMapConfig,
} from './baseMap'

describe('base map configuration', () => {
  it('loads the free default provider with visible attribution', () => {
    const config = loadBaseMapConfig({})
    const style = buildBaseMapStyle(config)
    const source = style.sources[BASE_MAP_SOURCE_ID]

    expect(config.name).toBe('OpenStreetMap Standard')
    expect(config.tiles).toEqual([
      'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
    ])
    expect(source).toMatchObject({
      type: 'raster',
      tileSize: 256,
      minzoom: 0,
      maxzoom: 19,
      attribution: expect.stringContaining('OpenStreetMap contributors'),
    })
  })

  it('loads a complete custom XYZ provider from environment values', () => {
    const config = loadBaseMapConfig({
      VITE_BASE_MAP_ATTRIBUTION_TEXT: 'Example Maps',
      VITE_BASE_MAP_ATTRIBUTION_URL: 'https://maps.example.com/terms',
      VITE_BASE_MAP_MAX_ZOOM: '22',
      VITE_BASE_MAP_MIN_ZOOM: '2',
      VITE_BASE_MAP_NAME: 'Example satellite',
      VITE_BASE_MAP_TILE_SIZE: '512',
      VITE_BASE_MAP_TILE_URL:
        'https://tiles.example.com/{z}/{x}/{y}.webp',
    })

    expect(config).toMatchObject({
      name: 'Example satellite',
      minZoom: 2,
      maxZoom: 22,
      tileSize: 512,
      tiles: ['https://tiles.example.com/{z}/{x}/{y}.webp'],
    })
  })

  it.each([
    {
      environment: {
        VITE_BASE_MAP_TILE_URL: 'https://tiles.example.com/{z}/{x}.png',
        VITE_BASE_MAP_ATTRIBUTION_TEXT: 'Example Maps',
        VITE_BASE_MAP_ATTRIBUTION_URL: 'https://maps.example.com/terms',
      },
      message: '{y}',
    },
    {
      environment: {
        VITE_BASE_MAP_TILE_URL:
          'https://tiles.example.com/{z}/{x}/{y}.png',
      },
      message: 'VITE_BASE_MAP_ATTRIBUTION_TEXT',
    },
    {
      environment: { VITE_BASE_MAP_MAX_ZOOM: '25' },
      message: 'limites de zoom',
    },
    {
      environment: { VITE_BASE_MAP_TILE_SIZE: '300' },
      message: '256 ou 512',
    },
  ])('rejects invalid configuration: $message', ({ environment, message }) => {
    expect(() => loadBaseMapConfig(environment)).toThrow(message)
  })
})

describe('base map replacement', () => {
  it('switches between configurations without removing GeoMap layers', () => {
    const existingLayers = new Set([
      BASE_MAP_BACKGROUND_LAYER_ID,
      BASE_MAP_RASTER_LAYER_ID,
      'geomap-network-lines',
    ])
    const existingSources = new Set([BASE_MAP_SOURCE_ID, 'geomap-network'])
    const addLayer = vi.fn((layer: LayerSpecification) => {
      existingLayers.add(layer.id)
    })
    const addSource = vi.fn((id: string) => existingSources.add(id))
    const removeLayer = vi.fn((id: string) => existingLayers.delete(id))
    const removeSource = vi.fn((id: string) => existingSources.delete(id))
    const map = {
      addLayer,
      addSource,
      getLayer: (id: string) => (existingLayers.has(id) ? {} : undefined),
      getSource: (id: string) => (existingSources.has(id) ? {} : undefined),
      getStyle: () => ({
        version: 8 as const,
        sources: {},
        layers: [...existingLayers].map(
          (id) => ({ id, type: 'background' }) as LayerSpecification,
        ),
      }),
      removeLayer,
      removeSource,
    }
    const firstConfig = loadBaseMapConfig({})
    const secondConfig: BaseMapConfig = {
      ...firstConfig,
      attribution: {
        text: 'Example Maps',
        url: 'https://maps.example.com/terms',
      },
      name: 'Example Maps',
      tiles: ['https://tiles.example.com/{z}/{x}/{y}.png'],
    }

    replaceBaseMap(map, firstConfig)
    replaceBaseMap(map, secondConfig)

    expect(removeLayer).not.toHaveBeenCalledWith('geomap-network-lines')
    expect(removeSource).not.toHaveBeenCalledWith('geomap-network')
    expect(existingLayers).toContain('geomap-network-lines')
    expect(existingSources).toContain('geomap-network')
    expect(addLayer).toHaveBeenCalledWith(
      expect.objectContaining({ id: BASE_MAP_RASTER_LAYER_ID }),
      'geomap-network-lines',
    )
    expect(addSource).toHaveBeenLastCalledWith(
      BASE_MAP_SOURCE_ID,
      expect.objectContaining<Partial<SourceSpecification>>({
        tiles: ['https://tiles.example.com/{z}/{x}/{y}.png'],
      }),
    )
  })
})
