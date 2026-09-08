import { useEffect, useRef, useState } from 'react'
import * as maplibregl from 'maplibre-gl'
import { baseMapConfig, buildBaseMapStyle } from './map/baseMap'
import {
  initialNetworkStatus,
  installNetworkLayer,
  networkConfiguration,
  type NetworkMapTarget,
  type NetworkStatus,
} from './map/networkLayer'
import './App.css'

function App() {
  const mapContainer = useRef<HTMLDivElement>(null)
  const [networkStatus, setNetworkStatus] = useState<NetworkStatus>(() =>
    initialNetworkStatus(networkConfiguration),
  )

  useEffect(() => {
    if (!mapContainer.current) {
      return
    }

    const map = new maplibregl.Map({
      container: mapContainer.current,
      style: buildBaseMapStyle(baseMapConfig),
      center: [-48.845, -26.304],
      zoom: 15,
      minZoom: 1,
      maxZoom: 22,
      attributionControl: { compact: false },
    })
    let detachNetworkLayer: (() => void) | undefined

    const styleSubscription = map.on('style.load', () => {
      if (networkConfiguration.kind === 'ready') {
        detachNetworkLayer = installNetworkLayer(
          map as unknown as NetworkMapTarget,
          networkConfiguration.value,
          setNetworkStatus,
        )
      }
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
      detachNetworkLayer?.()
      map.remove()
    }
  }, [])

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

      <aside
        className={`map-status map-status--${networkStatus.kind}`}
        aria-label="Status da rede"
        aria-live="polite"
      >
        <span className="map-status__dot" aria-hidden="true" />
        <span>{networkStatus.message}</span>
      </aside>
    </main>
  )
}

export default App
