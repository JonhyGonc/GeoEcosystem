import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import type { NetworkConfiguration } from './map/networkLayer'

const mapMocks = vi.hoisted(() => {
  const listeners: Record<string, () => void> = {}
  return {
    addControl: vi.fn(),
    cameraForBounds: vi.fn(() => ({ zoom: 16 })),
    createMap: vi.fn(),
    createNavigationControl: vi.fn(),
    createScaleControl: vi.fn(),
    listeners,
    on: vi.fn((event: string, listener: () => void) => {
      listeners[event] = listener
      return { unsubscribe: vi.fn() }
    }),
    fitBounds: vi.fn(),
    flyTo: vi.fn(),
    remove: vi.fn(),
  }
})

vi.mock('maplibre-gl', () => {
  class Map {
    addControl = mapMocks.addControl
    cameraForBounds = mapMocks.cameraForBounds
    fitBounds = mapMocks.fitBounds
    flyTo = mapMocks.flyTo
    on = mapMocks.on
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

const alfa = { id: '60000000-0000-4000-8000-000000000001', name: 'Projeto Alfa' }
const beta = { id: '60000000-0000-4000-8000-000000000002', name: 'Projeto Beta' }

function stubApi(
  routes: Record<string, unknown>,
  failures: Record<string, number> = {},
) {
  const fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
    if (init?.signal?.aborted) throw new DOMException('Aborted', 'AbortError')
    if (url in failures) return { ok: false, status: failures[url] }
    if (!(url in routes)) throw new Error(`Unexpected request: ${url}`)
    return { ok: true, json: async () => routes[url] }
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function emptyNetworkCatalog(projectId: string) {
  return { projectId, networks: [] }
}

const betaLocation = {
  projectId: beta.id,
  extent: {
    minLongitude: -39.6,
    minLatitude: -17.1,
    maxLongitude: -39.59,
    maxLatitude: -17.09,
  },
  focus: { longitude: -39.595, latitude: -17.095 },
}

afterEach(() => {
  cleanup()
  vi.clearAllMocks()
  vi.unstubAllGlobals()
  for (const event of Object.keys(mapMocks.listeners)) {
    delete mapMocks.listeners[event]
  }
})

describe('GeoMap app', () => {
  it('renders the map and enables navigation controls', async () => {
    stubApi({ '/api/projects': { projects: [] } })
    const { unmount } = render(<App />)

    expect(
      screen.getByRole('heading', { level: 1, name: 'GeoMap' }),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Mapa interativo GeoMap')).toBeInTheDocument()
    expect(screen.getByLabelText('Status das redes')).toBeInTheDocument()
    expect(await screen.findByText('Nenhum projeto importado')).toBeInTheDocument()
    expect(screen.queryByRole('combobox', { name: 'Projeto' })).not.toBeInTheDocument()

    expect(mapMocks.createMap).toHaveBeenCalledWith(
      expect.objectContaining({
        center: [-48.845, -26.304],
        maxZoom: 22,
        minZoom: 1,
        attributionControl: { compact: false },
        zoom: 15,
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
    expect(mapMocks.on).toHaveBeenCalledWith('style.load', expect.any(Function))
    expect(mapMocks.addControl).toHaveBeenCalledTimes(2)

    unmount()
    expect(mapMocks.remove).toHaveBeenCalledOnce()
  })

  it('opens the configured project and loads only the chosen project networks', async () => {
    const fetchMock = stubApi({
      '/api/projects': { projects: [alfa, beta] },
      [`/api/projects/${alfa.id}/networks`]: emptyNetworkCatalog(alfa.id),
      [`/api/projects/${beta.id}/networks`]: emptyNetworkCatalog(beta.id),
      [`/api/projects/${alfa.id}/extent`]: {
        projectId: alfa.id,
        extent: null,
        focus: null,
      },
      [`/api/projects/${beta.id}/extent`]: betaLocation,
    })
    const configuration: NetworkConfiguration = {
      kind: 'ready',
      value: { apiBaseUrl: '/api', initialProjectId: beta.id },
    }

    render(<App configuration={configuration} />)

    const select = await screen.findByRole('combobox', { name: 'Projeto' })
    expect(select).toHaveValue(beta.id)

    const locate = screen.getByRole('button', { name: 'Localizar projeto no mapa' })
    await waitFor(() => expect(locate).toBeEnabled())
    const betaBounds = [
      [-39.6, -17.1],
      [-39.59, -17.09],
    ]
    expect(mapMocks.fitBounds).toHaveBeenCalledOnce()
    expect(mapMocks.fitBounds).toHaveBeenCalledWith(betaBounds, expect.anything())

    fireEvent.click(locate)
    expect(mapMocks.fitBounds).toHaveBeenCalledTimes(2)

    act(() => {
      mapMocks.listeners['style.load']?.()
    })

    expect(await screen.findByText('Este projeto não possui redes')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledWith(
      `/api/projects/${beta.id}/networks`,
      expect.anything(),
    )
    expect(fetchMock).not.toHaveBeenCalledWith(
      `/api/projects/${alfa.id}/networks`,
      expect.anything(),
    )

    fireEvent.change(select, { target: { value: alfa.id } })

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        `/api/projects/${alfa.id}/networks`,
        expect.anything(),
      ),
    )
    expect(select).toHaveValue(alfa.id)
    await waitFor(() => expect(locate).toBeDisabled())
    expect(locate).toHaveAttribute('title', 'Projeto sem feições para localizar')
    expect(mapMocks.fitBounds).toHaveBeenCalledTimes(2)
    expect(mapMocks.flyTo).not.toHaveBeenCalled()
  })

  it('keeps the base map when the project catalog fails', async () => {
    stubApi({}, { '/api/projects': 503 })

    render(<App />)

    expect(
      await screen.findByText('Catálogo de projetos indisponível (503)'),
    ).toBeInTheDocument()
    expect(screen.queryByRole('combobox', { name: 'Projeto' })).not.toBeInTheDocument()
    expect(screen.getByLabelText('Mapa interativo GeoMap')).toBeInTheDocument()
  })

  it('reports an invalid environment without requesting projects', () => {
    const fetchMock = stubApi({})
    const configuration: NetworkConfiguration = {
      kind: 'invalid',
      message: 'Projeto ou rede inicial possui identificador inválido',
    }

    render(<App configuration={configuration} />)

    expect(
      screen.getByText('Projeto ou rede inicial possui identificador inválido'),
    ).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })
})
