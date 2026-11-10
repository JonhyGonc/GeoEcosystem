import type { ProjectLocation } from '../api/projectExtent'
import { NETWORK_MIN_ZOOM } from './networkLayer'

export type LngLatBounds = [[number, number], [number, number]]

/** Subconjunto do `maplibregl.Map` usado para enquadrar um projeto. */
export interface ProjectLocatorTarget {
  cameraForBounds(
    bounds: LngLatBounds,
    options?: { padding?: number },
  ): { zoom?: number } | undefined
  fitBounds(
    bounds: LngLatBounds,
    options?: { padding?: number; maxZoom?: number },
  ): unknown
  flyTo(options: { center: [number, number]; zoom: number }): unknown
}

export const LOCATE_PADDING = 48
export const LOCATE_MAX_ZOOM = 18
export const FOCUS_ZOOM = NETWORK_MIN_ZOOM + 1

export function projectBounds({ extent }: ProjectLocation): LngLatBounds {
  return [
    [extent.minLongitude, extent.minLatitude],
    [extent.maxLongitude, extent.maxLatitude],
  ]
}

/**
 * Enquadra o projeto inteiro quando ele cabe na tela com as redes visíveis,
 * ou seja, com zoom de pelo menos NETWORK_MIN_ZOOM. Caso contrário, voa até o
 * ponto da rede mais próximo do centro do projeto, porque um enquadramento
 * completo abaixo do zoom mínimo mostraria um mapa sem nenhuma rede.
 */
export function locateProject(
  map: ProjectLocatorTarget,
  location: ProjectLocation,
): 'bounds' | 'focus' {
  const bounds = projectBounds(location)
  const camera = map.cameraForBounds(bounds, { padding: LOCATE_PADDING })
  const zoom = Math.min(camera?.zoom ?? 0, LOCATE_MAX_ZOOM)

  if (zoom >= NETWORK_MIN_ZOOM) {
    map.fitBounds(bounds, { padding: LOCATE_PADDING, maxZoom: LOCATE_MAX_ZOOM })
    return 'bounds'
  }

  map.flyTo({
    center: [location.focus.longitude, location.focus.latitude],
    zoom: FOCUS_ZOOM,
  })
  return 'focus'
}
