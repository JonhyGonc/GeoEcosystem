import { useEffect, useRef } from 'react'
import * as maplibregl from 'maplibre-gl'
import { baseMapConfig, buildBaseMapStyle } from './map/baseMap'
import './App.css'

function App() {
  const mapContainer = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!mapContainer.current) {
      return
    }

    const map = new maplibregl.Map({
      container: mapContainer.current,
      style: buildBaseMapStyle(baseMapConfig),
      center: [-48.845, -26.304],
      zoom: 13,
      minZoom: 1,
      maxZoom: 22,
      attributionControl: { compact: false },
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

    return () => map.remove()
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

      <aside className="map-status" aria-label="Status do mapa">
        <span className="map-status__dot" aria-hidden="true" />
        <span>Mapa base: {baseMapConfig.name}</span>
      </aside>
    </main>
  )
}

export default App
