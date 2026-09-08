import { useEffect, useRef, useState } from 'react'
import * as maplibregl from 'maplibre-gl'
import { LayerControl } from './LayerControl'
import { baseMapConfig, buildBaseMapStyle } from './map/baseMap'
import {
  createNetworkLayerViews,
  fetchProjectNetworks,
  initialNetworkStatus,
  installNetworkLayers,
  networkConfiguration,
  summarizeNetworkLayers,
  type NetworkConfiguration,
  type NetworkLayerController,
  type NetworkLayerStatus,
  type NetworkLayerView,
  type NetworkMapTarget,
  type NetworkStatus,
} from './map/networkLayer'
import './App.css'

interface AppProps {
  readonly configuration?: NetworkConfiguration
}

function App({ configuration = networkConfiguration }: AppProps) {
  const mapContainer = useRef<HTMLDivElement>(null)
  const mapInstance = useRef<NetworkMapTarget>(null)
  const layerController = useRef<NetworkLayerController>(null)
  const [styleRevision, setStyleRevision] = useState(0)
  const [layers, setLayers] = useState<NetworkLayerView[]>([])
  const [networkStatus, setNetworkStatus] = useState<NetworkStatus>(() =>
    initialNetworkStatus(configuration),
  )

  useEffect(() => {
    if (!mapContainer.current) return

    const map = new maplibregl.Map({
      container: mapContainer.current,
      style: buildBaseMapStyle(baseMapConfig),
      center: [-48.845, -26.304],
      zoom: 15,
      minZoom: 1,
      maxZoom: 22,
      attributionControl: { compact: false },
    })
    mapInstance.current = map as unknown as NetworkMapTarget
    const styleSubscription = map.on('style.load', () => {
      setStyleRevision((revision) => revision + 1)
    })

    map.addControl(
      new maplibregl.NavigationControl({
        showCompass: true,
        showZoom: true,
        visualizePitch: false,
      }),
      'top-right',
    )
    map.addControl(
      new maplibregl.ScaleControl({ maxWidth: 120, unit: 'metric' }),
      'bottom-right',
    )

    return () => {
      styleSubscription.unsubscribe()
      layerController.current?.cleanup()
      layerController.current = null
      mapInstance.current = null
      map.remove()
    }
  }, [])

  useEffect(() => {
    if (styleRevision === 0 || !mapInstance.current) return

    layerController.current?.cleanup()
    layerController.current = null
    setLayers([])
    setNetworkStatus(initialNetworkStatus(configuration))
    if (configuration.kind !== 'ready') return

    const abortController = new AbortController()
    const map = mapInstance.current

    void fetchProjectNetworks(configuration.value, abortController.signal)
      .then((networks) => {
        if (abortController.signal.aborted) return
        const nextLayers = createNetworkLayerViews(
          networks,
          configuration.value.initialNetworkId,
        )
        setLayers(nextLayers)
        setNetworkStatus(summarizeNetworkLayers(nextLayers))
        if (nextLayers.length === 0) return

        layerController.current = installNetworkLayers(
          map,
          configuration.value,
          nextLayers,
          (networkId, status) => {
            setLayers((current) =>
              current.map((layer) =>
                layer.id === networkId ? { ...layer, status } : layer,
              ),
            )
          },
        )
      })
      .catch((error: unknown) => {
        if (abortController.signal.aborted) return
        setNetworkStatus({
          kind: 'error',
          message:
            error instanceof Error
              ? error.message
              : 'Não foi possível listar as redes',
        })
      })

    return () => {
      abortController.abort()
      layerController.current?.cleanup()
      layerController.current = null
    }
  }, [configuration, styleRevision])

  const changeVisibility = (networkId: string, visible: boolean) => {
    layerController.current?.setVisibility(networkId, visible)
    setLayers((current) =>
      current.map((layer) =>
        layer.id === networkId
          ? { ...layer, visible, status: visibilityStatus(visible) }
          : layer,
      ),
    )
  }

  const changeAllVisibility = (visible: boolean) => {
    for (const layer of layers) {
      layerController.current?.setVisibility(layer.id, visible)
    }
    setLayers((current) =>
      current.map((layer) => ({
        ...layer,
        visible,
        status: visibilityStatus(visible),
      })),
    )
  }

  const displayedNetworkStatus =
    layers.length > 0 ? summarizeNetworkLayers(layers) : networkStatus

  return (
    <main className="map-shell">
      <div
        ref={mapContainer}
        className="map-canvas"
        aria-label="Mapa interativo GeoMap"
      />

      <header className="map-header">
        <span className="map-header__eyebrow">Protótipo 0.1</span>
        <h1>GeoMap</h1>
        <p>Visualizador geográfico de redes de tubulação</p>
      </header>

      {layers.length > 0 && (
        <LayerControl
          layers={layers}
          onVisibilityChange={changeVisibility}
          onShowAll={() => changeAllVisibility(true)}
          onHideAll={() => changeAllVisibility(false)}
        />
      )}

      <aside
        className={`map-status map-status--${displayedNetworkStatus.kind}`}
        aria-label="Status das redes"
        aria-live="polite"
      >
        <span className="map-status__dot" aria-hidden="true" />
        <span>{displayedNetworkStatus.message}</span>
      </aside>
    </main>
  )
}

function visibilityStatus(visible: boolean): NetworkLayerStatus {
  return visible
    ? { kind: 'loading', message: 'Carregando…' }
    : { kind: 'hidden', message: 'Oculta' }
}

export default App
