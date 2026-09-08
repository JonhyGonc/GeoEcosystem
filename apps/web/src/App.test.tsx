import { render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'

const mapMocks = vi.hoisted(() => ({
  addControl: vi.fn(),
  createMap: vi.fn(),
  createNavigationControl: vi.fn(),
  createScaleControl: vi.fn(),
  remove: vi.fn(),
}))

vi.mock('maplibre-gl', () => {
  class Map {
    addControl = mapMocks.addControl
    remove = mapMocks.remove

    constructor(options: unknown) {
      mapMocks.createMap(options)
    }
  }

  class NavigationControl {
    constructor(options: unknown) {
      mapMocks.createNavigationControl(options)
    }
  }

  class ScaleControl {
    constructor(options: unknown) {
      mapMocks.createScaleControl(options)
    }
  }

  return { Map, NavigationControl, ScaleControl }
})

afterEach(() => {
  vi.clearAllMocks()
})

describe('GeoMap app', () => {
  it('renders the map and enables navigation controls', () => {
    const { unmount } = render(<App />)

    expect(
      screen.getByRole('heading', { level: 1, name: 'GeoMap' }),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Mapa interativo GeoMap')).toBeInTheDocument()
    expect(screen.getByText('Mapa base: OpenStreetMap Standard')).toBeInTheDocument()

    expect(mapMocks.createMap).toHaveBeenCalledWith(
      expect.objectContaining({
        center: [-48.845, -26.304],
        maxZoom: 22,
        minZoom: 1,
        attributionControl: { compact: false },
        zoom: 13,
      }),
    )
    expect(mapMocks.createMap.mock.calls[0][0]).toEqual(
      expect.objectContaining({
        style: expect.objectContaining({
          sources: expect.objectContaining({
            'basemap-raster-source': expect.objectContaining({
              attribution: expect.stringContaining('OpenStreetMap contributors'),
            }),
          }),
        }),
      }),
    )
    expect(mapMocks.createNavigationControl).toHaveBeenCalledWith({
      showCompass: true,
      showZoom: true,
      visualizePitch: false,
    })
    expect(mapMocks.addControl).toHaveBeenCalledTimes(2)

    unmount()
    expect(mapMocks.remove).toHaveBeenCalledOnce()
  })
})
