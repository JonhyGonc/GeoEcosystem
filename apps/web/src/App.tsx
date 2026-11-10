import { useEffect, useMemo, useRef, useState } from 'react'
import * as maplibregl from 'maplibre-gl'
import { LayerControl } from './LayerControl'
import { ProjectSelector } from './ProjectSelector'
import { SelectionSummary } from './SelectionSummary'
import {
  fetchFeatureDetails,
  type FeatureDetailsState,
} from './api/featureDetails'
import {
  fetchProjectLocation,
  type ProjectLocation,
} from './api/projectExtent'
import {
  chooseInitialProjectId,
  fetchProjects,
  type ProjectCatalogState,
} from './api/projectCatalog'
import { baseMapConfig, buildBaseMapStyle } from './map/baseMap'
import {
  buildNetworkCatalogConfig,
  createNetworkLayerViews,
  fetchProjectNetworks,
  installNetworkLayers,
  networkConfiguration,
  summarizeNetworkLayers,
  type NetworkConfiguration,
  type NetworkLayerController,
  type NetworkLayerStatus,
  type NetworkLayerView,
  type NetworkMapTarget,
  type NetworkStatus,
  type SelectedNetworkFeature,
} from './map/networkLayer'
import {
  locateProject,
  type ProjectLocatorTarget,
} from './map/projectLocator'
import './App.css'

interface AppProps {
  readonly configuration?: NetworkConfiguration
}

type ProjectLocationState =
  | { readonly kind: 'idle' }
  | { readonly kind: 'loading' }
  | { readonly kind: 'ready'; readonly location: ProjectLocation }
  | { readonly kind: 'empty' }
  | { readonly kind: 'error'; readonly message: string }

const LOADING_LAYERS_STATUS: NetworkStatus = {
  kind: 'loading',
  message: 'Carregando camadas…',
}

function App({ configuration = networkConfiguration }: AppProps) {
  const mapContainer = useRef<HTMLDivElement>(null)
  const mapInstance = useRef<NetworkMapTarget>(null)
  const locatorTarget = useRef<ProjectLocatorTarget>(null)
  const layerController = useRef<NetworkLayerController>(null)
  const [styleRevision, setStyleRevision] = useState(0)
  const [projectCatalog, setProjectCatalog] = useState<ProjectCatalogState>({
    kind: 'loading',
  })
  const [selectedProjectId, setSelectedProjectId] = useState<string | null>(
    null,
  )
  const [startupProjectId, setStartupProjectId] = useState<string | null>(null)
  const [projectLocation, setProjectLocation] = useState<ProjectLocationState>({
    kind: 'idle',
  })
  const [layers, setLayers] = useState<NetworkLayerView[]>([])
  const [networkStatus, setNetworkStatus] =
    useState<NetworkStatus>(LOADING_LAYERS_STATUS)
  const [selectedFeature, setSelectedFeature] =
    useState<SelectedNetworkFeature | null>(null)
  const [featureDetails, setFeatureDetails] = useState<FeatureDetailsState>({
    kind: 'idle',
  })
  const [detailsRevision, setDetailsRevision] = useState(0)

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
    locatorTarget.current = map as unknown as ProjectLocatorTarget
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
      locatorTarget.current = null
      map.remove()
    }
  }, [])

  useEffect(() => {
    if (configuration.kind !== 'ready') return

    const abortController = new AbortController()
    void fetchProjects(configuration.value.apiBaseUrl, abortController.signal)
      .then((projects) => {
        if (abortController.signal.aborted) return
        const initialProjectId = chooseInitialProjectId(
          projects,
          configuration.value.initialProjectId,
        )
        setProjectCatalog({ kind: 'ready', projects })
        setSelectedProjectId(initialProjectId)
        setStartupProjectId(initialProjectId)
      })
      .catch((error: unknown) => {
        if (abortController.signal.aborted) return
        setProjectCatalog({
          kind: 'error',
          message:
            error instanceof Error
              ? error.message
              : 'Não foi possível listar os projetos',
        })
      })

    return () => abortController.abort()
  }, [configuration])

  useEffect(() => {
    if (configuration.kind !== 'ready' || !selectedProjectId) return

    const abortController = new AbortController()
    setProjectLocation({ kind: 'loading' })
    void fetchProjectLocation(
      configuration.value.apiBaseUrl,
      selectedProjectId,
      abortController.signal,
    )
      .then((location) => {
        if (abortController.signal.aborted) return
        if (!location) {
          setProjectLocation({ kind: 'empty' })
          return
        }
        setProjectLocation({ kind: 'ready', location })
        if (locatorTarget.current) locateProject(locatorTarget.current, location)
      })
      .catch((error: unknown) => {
        if (abortController.signal.aborted) return
        setProjectLocation({
          kind: 'error',
          message:
            error instanceof Error
              ? error.message
              : 'Não foi possível localizar o projeto',
        })
      })

    return () => abortController.abort()
  }, [configuration, selectedProjectId])

  const catalogConfig = useMemo(() => {
    if (configuration.kind !== 'ready' || !selectedProjectId) return null
    return buildNetworkCatalogConfig(
      configuration.value,
      selectedProjectId,
      selectedProjectId === startupProjectId,
    )
  }, [configuration, selectedProjectId, startupProjectId])

  useEffect(() => {
    if (styleRevision === 0 || !mapInstance.current) return

    layerController.current?.cleanup()
    layerController.current = null
    setSelectedFeature(null)
    setFeatureDetails({ kind: 'idle' })
    setLayers([])
    setNetworkStatus(LOADING_LAYERS_STATUS)
    if (!catalogConfig) return

    const abortController = new AbortController()
    const map = mapInstance.current

    void fetchProjectNetworks(catalogConfig, abortController.signal)
      .then((networks) => {
        if (abortController.signal.aborted) return
        const nextLayers = createNetworkLayerViews(
          networks,
          catalogConfig.initialNetworkId,
        )
        setLayers(nextLayers)
        setNetworkStatus(summarizeNetworkLayers(nextLayers))
        if (nextLayers.length === 0) return

        layerController.current = installNetworkLayers(
          map,
          catalogConfig,
          nextLayers,
          (networkId, status) => {
            setLayers((current) =>
              current.map((layer) =>
                layer.id === networkId ? { ...layer, status } : layer,
              ),
            )
          },
          (selection) => {
            setSelectedFeature(selection)
            setFeatureDetails(selection ? { kind: 'loading' } : { kind: 'idle' })
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
  }, [catalogConfig, styleRevision])

  useEffect(() => {
    if (!selectedFeature || !catalogConfig) return

    const abortController = new AbortController()
    void fetchFeatureDetails(
      catalogConfig,
      selectedFeature,
      abortController.signal,
    )
      .then((details) => {
        if (abortController.signal.aborted) return
        setFeatureDetails(
          details ? { kind: 'ready', details } : { kind: 'not-found' },
        )
      })
      .catch((error: unknown) => {
        if (abortController.signal.aborted) return
        setFeatureDetails({
          kind: 'error',
          message:
            error instanceof Error
              ? error.message
              : 'Não foi possível carregar os atributos',
        })
      })

    return () => abortController.abort()
  }, [catalogConfig, detailsRevision, selectedFeature])

  const locateSelectedProject = () => {
    if (projectLocation.kind !== 'ready' || !locatorTarget.current) return
    locateProject(locatorTarget.current, projectLocation.location)
  }

  const changeProject = (projectId: string) => {
    setStartupProjectId(null)
    setSelectedProjectId(projectId)
  }

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
    projectCatalogStatus(configuration, projectCatalog) ??
    (layers.length > 0 ? summarizeNetworkLayers(layers) : networkStatus)

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
        {projectCatalog.kind === 'ready' && selectedProjectId && (
          <ProjectSelector
            projects={projectCatalog.projects}
            selectedProjectId={selectedProjectId}
            onProjectChange={changeProject}
            onLocate={locateSelectedProject}
            locateDisabledReason={locateDisabledReason(projectLocation)}
          />
        )}
      </header>

      {layers.length > 0 && (
        <LayerControl
          layers={layers}
          onVisibilityChange={changeVisibility}
          onShowAll={() => changeAllVisibility(true)}
          onHideAll={() => changeAllVisibility(false)}
        />
      )}

      {selectedFeature && (
        <SelectionSummary
          selection={selectedFeature}
          detailsState={featureDetails}
          onClear={() => layerController.current?.clearSelection()}
          onRetry={() => {
            setFeatureDetails({ kind: 'loading' })
            setDetailsRevision((revision) => revision + 1)
          }}
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

function locateDisabledReason(
  state: ProjectLocationState,
): string | undefined {
  switch (state.kind) {
    case 'ready':
      return undefined
    case 'empty':
      return 'Projeto sem feições para localizar'
    case 'error':
      return state.message
    default:
      return 'Carregando localização do projeto…'
  }
}

function projectCatalogStatus(
  configuration: NetworkConfiguration,
  projectCatalog: ProjectCatalogState,
): NetworkStatus | null {
  if (configuration.kind === 'invalid') {
    return { kind: 'error', message: configuration.message }
  }
  if (projectCatalog.kind === 'loading') {
    return { kind: 'loading', message: 'Carregando projetos…' }
  }
  if (projectCatalog.kind === 'error') {
    return { kind: 'error', message: projectCatalog.message }
  }
  if (projectCatalog.projects.length === 0) {
    return { kind: 'unconfigured', message: 'Nenhum projeto importado' }
  }
  return null
}

function visibilityStatus(visible: boolean): NetworkLayerStatus {
  return visible
    ? { kind: 'loading', message: 'Carregando…' }
    : { kind: 'hidden', message: 'Oculta' }
}

export default App
