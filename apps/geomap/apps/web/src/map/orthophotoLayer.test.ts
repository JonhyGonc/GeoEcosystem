import { describe, expect, it, vi } from 'vitest'
import {
  ORTHOPHOTO_LAYER_ID,
  ORTHOPHOTO_SOURCE_ID,
  installOrthophoto,
  removeOrthophoto,
  updateOrthophoto,
  type OrthophotoMapTarget,
} from './orthophotoLayer'

const info = {
  available: true as const,
  fileName: 'Obra.mbtiles',
  tileUrl: '/api/projects/p/orthophoto/tiles/{z}/{x}/{y}',
  bounds: [-39.1, -14.2, -39.0, -14.1] as const,
  minZoom: 12,
  maxZoom: 21,
}

function fakeMap(layerIds: string[]) {
  const layers = new Set(layerIds)
  const sources = new Set<string>()
  const map = {
    addLayer: vi.fn((layer: { id: string }) => layers.add(layer.id)),
    addSource: vi.fn((id: string) => sources.add(id)),
    getLayer: vi.fn((id: string) => (layers.has(id) ? {} : undefined)),
    getSource: vi.fn((id: string) => (sources.has(id) ? {} : undefined)),
    getStyle: vi.fn(() => ({ layers: [...layers].map((id) => ({ id })) })),
    removeLayer: vi.fn((id: string) => layers.delete(id)),
    removeSource: vi.fn((id: string) => sources.delete(id)),
    setLayoutProperty: vi.fn(),
    setPaintProperty: vi.fn(),
  }
  return map as typeof map & OrthophotoMapTarget
}

describe('orthophoto layer', () => {
  it('goes above the base map and below the first network layer', () => {
    const map = fakeMap(['basemap-raster', 'geomap-network-lines-1', 'geomap-network-points-1'])

    installOrthophoto(map, info, { visible: true, opacity: 0.6 })

    expect(map.addSource).toHaveBeenCalledWith(ORTHOPHOTO_SOURCE_ID, {
      type: 'raster',
      tiles: [info.tileUrl],
      tileSize: 256,
      minzoom: 12,
      maxzoom: 21,
      bounds: [-39.1, -14.2, -39.0, -14.1],
    })
    expect(map.addLayer).toHaveBeenCalledWith(
      expect.objectContaining({
        id: ORTHOPHOTO_LAYER_ID,
        type: 'raster',
        layout: { visibility: 'visible' },
        paint: { 'raster-opacity': 0.6 },
      }),
      'geomap-network-lines-1',
    )
  })

  it('is replaced on reinstall and follows visibility and opacity', () => {
    const map = fakeMap(['basemap-raster'])
    installOrthophoto(map, info, { visible: true, opacity: 1 })
    installOrthophoto(map, info, { visible: true, opacity: 1 })

    updateOrthophoto(map, { visible: false, opacity: 2 })

    expect(map.removeLayer).toHaveBeenCalledWith(ORTHOPHOTO_LAYER_ID)
    expect(map.addLayer).toHaveBeenLastCalledWith(expect.anything(), undefined)
    expect(map.setLayoutProperty).toHaveBeenCalledWith(ORTHOPHOTO_LAYER_ID, 'visibility', 'none')
    expect(map.setPaintProperty).toHaveBeenCalledWith(ORTHOPHOTO_LAYER_ID, 'raster-opacity', 1)

    removeOrthophoto(map)
    expect(map.getLayer(ORTHOPHOTO_LAYER_ID)).toBeUndefined()
    expect(map.getSource(ORTHOPHOTO_SOURCE_ID)).toBeUndefined()
  })
})
