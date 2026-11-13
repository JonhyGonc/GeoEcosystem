import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { setWorkerUrl } from 'maplibre-gl'
import maplibreWorkerUrl from 'maplibre-gl/dist/maplibre-gl-worker.mjs?url'
import 'maplibre-gl/dist/maplibre-gl.css'
import App from './App'
import './index.css'

// O MapLibre 6 localiza o worker relativo ao próprio módulo. O pré-empacotamento
// do Vite em desenvolvimento e o bundle de produção movem esse módulo, e o
// worker deixa de ser encontrado: os tiles vetoriais nunca são processados e
// nenhum erro aparece. A URL explícita mantém o worker acessível nos dois modos.
setWorkerUrl(maplibreWorkerUrl)

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
