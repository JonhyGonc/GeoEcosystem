import { describe, expect, it, vi } from 'vitest'
import { NETWORK_MIN_ZOOM } from './networkLayer'
import {
  FOCUS_ZOOM,
  LOCATE_MAX_ZOOM,
  LOCATE_PADDING,
  locateProject,
  projectBounds,
  type ProjectLocatorTarget,
} from './projectLocator'

const location = {
  extent: {
    minLongitude: -48.9,
    minLatitude: -26.35,
    maxLongitude: -48.71,
    maxLatitude: -26.14,
  },
  focus: { longitude: -48.8, latitude: -26.25 },
}

function mapWithCameraZoom(zoom: number | undefined) {
  return {
    cameraForBounds: vi.fn(() => (zoom === undefined ? undefined : { zoom })),
    fitBounds: vi.fn(),
    flyTo: vi.fn(),
  } satisfies ProjectLocatorTarget
}

describe('project locator', () => {
  it('fits the whole project when the networks stay visible', () => {
    const map = mapWithCameraZoom(NETWORK_MIN_ZOOM + 0.5)

    expect(locateProject(map, location)).toBe('bounds')
    expect(map.cameraForBounds).toHaveBeenCalledWith(projectBounds(location), {
      padding: LOCATE_PADDING,
    })
    expect(map.fitBounds).toHaveBeenCalledWith(projectBounds(location), {
      padding: LOCATE_PADDING,
      maxZoom: LOCATE_MAX_ZOOM,
    })
    expect(map.flyTo).not.toHaveBeenCalled()
  })

  it('flies to the focus point when the whole project needs a zoom below the network minimum', () => {
    const map = mapWithCameraZoom(NETWORK_MIN_ZOOM - 2)

    expect(locateProject(map, location)).toBe('focus')
    expect(map.flyTo).toHaveBeenCalledWith({
      center: [-48.8, -26.25],
      zoom: FOCUS_ZOOM,
    })
    expect(map.fitBounds).not.toHaveBeenCalled()
    expect(FOCUS_ZOOM).toBeGreaterThanOrEqual(NETWORK_MIN_ZOOM)
  })

  it('caps a single tiny feature at the maximum locate zoom', () => {
    const map = mapWithCameraZoom(24)

    expect(locateProject(map, location)).toBe('bounds')
    expect(map.fitBounds).toHaveBeenCalledWith(
      projectBounds(location),
      expect.objectContaining({ maxZoom: LOCATE_MAX_ZOOM }),
    )
  })

  it('falls back to the focus point when the camera cannot be computed', () => {
    const map = mapWithCameraZoom(undefined)

    expect(locateProject(map, location)).toBe('focus')
  })
})
