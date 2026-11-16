import type { LayerSpecification, SourceSpecification } from 'maplibre-gl'
import type { OrthophotoInfo } from '../api/orthophoto'

export const ORTHOPHOTO_SOURCE_ID = 'geomap-orthophoto-source'
export const ORTHOPHOTO_LAYER_ID = 'geomap-orthophoto'
const NETWORK_LAYER_PREFIX = 'geomap-network-'

/** O que a camada da ortofoto usa do mapa; separado para testar sem o MapLibre. */
export interface OrthophotoMapTarget {
  addLayer(layer: LayerSpecification, beforeId?: string): unknown
  addSource(id: string, source: SourceSpecification): unknown
  getLayer(id: string): unknown
  getSource(id: string): unknown
  getStyle(): { readonly layers?: readonly { readonly id: string }[] }
  removeLayer(id: string): unknown
  removeSource(id: string): unknown
  setLayoutProperty(layerId: string, name: 'visibility', value: 'none' | 'visible'): unknown
  setPaintProperty(layerId: string, name: 'raster-opacity', value: number): unknown
}

export interface OrthophotoDisplay {
  readonly visible: boolean
  /** De 0 (transparente) a 1 (opaca). */
  readonly opacity: number
}

export function buildOrthophotoSource(
  info: Extract<OrthophotoInfo, { available: true }>,
): SourceSpecification {
  return {
    type: 'raster',
    tiles: [info.tileUrl],
    tileSize: 256,
    minzoom: info.minZoom,
    maxzoom: info.maxZoom,
    ...(info.bounds ? { bounds: [...info.bounds] as [number, number, number, number] } : {}),
  }
}

/**
 * Põe a ortofoto acima do mapa base e abaixo das redes. Depois do zoom máximo do arquivo, o MapLibre
 * amplia os últimos tiles.
 */
export function installOrthophoto(
  map: OrthophotoMapTarget,
  info: Extract<OrthophotoInfo, { available: true }>,
  display: OrthophotoDisplay,
): void {
  removeOrthophoto(map)
  map.addSource(ORTHOPHOTO_SOURCE_ID, buildOrthophotoSource(info))
  const firstNetworkLayer = map
    .getStyle()
    .layers?.find((layer) => layer.id.startsWith(NETWORK_LAYER_PREFIX))?.id
  map.addLayer(
    {
      id: ORTHOPHOTO_LAYER_ID,
      type: 'raster',
      source: ORTHOPHOTO_SOURCE_ID,
      layout: { visibility: display.visible ? 'visible' : 'none' },
      paint: { 'raster-opacity': clampOpacity(display.opacity) },
    },
    firstNetworkLayer,
  )
}

export function updateOrthophoto(map: OrthophotoMapTarget, display: OrthophotoDisplay): void {
  if (!map.getLayer(ORTHOPHOTO_LAYER_ID)) return
  map.setLayoutProperty(ORTHOPHOTO_LAYER_ID, 'visibility', display.visible ? 'visible' : 'none')
  map.setPaintProperty(ORTHOPHOTO_LAYER_ID, 'raster-opacity', clampOpacity(display.opacity))
}

export function removeOrthophoto(map: OrthophotoMapTarget): void {
  if (map.getLayer(ORTHOPHOTO_LAYER_ID)) map.removeLayer(ORTHOPHOTO_LAYER_ID)
  if (map.getSource(ORTHOPHOTO_SOURCE_ID)) map.removeSource(ORTHOPHOTO_SOURCE_ID)
}

function clampOpacity(value: number): number {
  return Number.isFinite(value) ? Math.min(1, Math.max(0, value)) : 1
}
