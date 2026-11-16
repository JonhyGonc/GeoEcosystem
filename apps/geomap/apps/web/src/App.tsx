import { useEffect, useMemo, useRef, useState } from 'react'
import * as maplibregl from 'maplibre-gl'
import { ImportPanel } from './ImportPanel'
import { LayerControl } from './LayerControl'
import { OrthophotoControl, type OrthophotoState } from './OrthophotoControl'
import { ProjectSelector } from './ProjectSelector'
import { SelectionSummary } from './SelectionSummary'
import {
  fetchFeatureDetails,
  type FeatureDetailsState,
} from './api/featureDetails'
import { fetchOrthophoto } from './api/orthophoto'
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
import { installDragCursor } from './map/dragCursor'
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
  installOrthophoto,
  removeOrthophoto,
  updateOrthophoto,
  type OrthophotoDisplay,
  type OrthophotoMapTarget,
} from './map/orthophotoLayer'
import {
  locateProject,
  type ProjectLocatorTarget,
} from './map/projectLocator'
import './App.css'

interface AppProps {
  readonly configuration?: NetworkConfiguration
}

type ProjectLocationState = { readonly projectId: string } & (
  | { readonly kind: 'ready'; readonly location: ProjectLocation }
  | { readonly kind: 'empty' }
  | { readonly kind: 'error'; readonly message: string }
)

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
  const [projectLocation, setProjectLocation] =
    useState<ProjectLocationState | null>(null)
  const [layers, setLayers] = useState<NetworkLayerView[]>([])
  const [networkStatus, setNetworkStatus] =
    useState<NetworkStatus>(LOADING_LAYERS_STATUS)
  const [selectedFeature, setSelectedFeature] =
    useState<SelectedNetworkFeature | null>(null)
  const [featureDetails, setFeatureDetails] = useState<FeatureDetailsState>({
    kind: 'idle',
  })
  const [detailsRevision, setDetailsRevision] = useState(0)
  // Importação pelo site (M4.5): cada importação recarrega o catálogo e as redes, e abre o projeto importado.
  const [dataRevision, setDataRevision] = useState(0)
  const importedProjectId = useRef<string | null>(null)
  // Ortofoto do projeto (DEC-035): o arquivo é procurado a cada troca de projeto ou importação.
  const orthophotoTarget = useRef<OrthophotoMapTarget>(null)
  const [orthophoto, setOrthophoto] = useState<
    ({ readonly projectId: string } & { readonly state: OrthophotoState }) | null
  >(null)
  const [orthophotoDisplay, setOrthophotoDisplay] = useState<OrthophotoDisplay>({
    visible: true,
    opacity: 1,
  })
  const orthophotoDisplayRef = useRef(orthophotoDisplay)

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
    orthophotoTarget.current = map as unknown as OrthophotoMapTarget
    const styleSubscription = map.on('style.load', () => {
      setStyleRevision((revision) => revision + 1)
    })
    const removeDragCursor = installDragCursor(map)

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
      removeDragCursor()
      layerController.current?.cleanup()
      layerController.current = null
      mapInstance.current = null
      locatorTarget.current = null
      orthophotoTarget.current = null
      map.remove()
    }
  }, [])

  useEffect(() => {
    if (configuration.kind !== 'ready') return

    const abortController = new AbortController()
    void fetchProjects(configuration.value.apiBaseUrl, abortController.signal)
      .then((projects) => {
        if (abortController.signal.aborted) return
        const imported = importedProjectId.current
        const initialProjectId = chooseInitialProjectId(
          projects,
          imported ?? configuration.value.initialProjectId,
        )
        setProjectCatalog({ kind: 'ready', projects })
        setSelectedProjectId(initialProjectId)
        setStartupProjectId(imported ? null : initialProjectId)
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
  }, [configuration, dataRevision])

  useEffect(() => {
    if (configuration.kind !== 'ready' || !selectedProjectId) return

    const projectId = selectedProjectId
    const abortController = new AbortController()
    void fetchProjectLocation(
      configuration.value.apiBaseUrl,
      projectId,
      abortController.signal,
    )
      .then((location) => {
        if (abortController.signal.aborted) return
        if (!location) {
          setProjectLocation({ projectId, kind: 'empty' })
          return
        }
        setProjectLocation({ projectId, kind: 'ready', location })
        if (locatorTarget.current) locateProject(locatorTarget.current, location)
      })
      .catch((error: unknown) => {
        if (abortController.signal.aborted) return
        setProjectLocation({
          projectId,
          kind: 'error',
          message:
            error instanceof Error
              ? error.message
              : 'Não foi possível localizar o projeto',
        })
      })

    return () => abortController.abort()
  }, [configuration, selectedProjectId, dataRevision])

  useEffect(() => {
    if (configuration.kind !== 'ready' || !selectedProjectId) return

    const projectId = selectedProjectId
    const abortController = new AbortController()
    setOrthophoto({ projectId, state: { kind: 'loading' } })
    void fetchOrthophoto(configuration.value.apiBaseUrl, projectId, abortController.signal)
      .then((info) => {
        if (abortController.signal.aborted) return
        setOrthophoto({ projectId, state: { kind: 'ready', info } })
      })
      .catch((error: unknown) => {
        if (abortController.signal.aborted) return
        setOrthophoto({
          projectId,
          state: {
            kind: 'error',
            message: error instanceof Error ? error.message : 'Não foi possível procurar a ortofoto',
          },
        })
      })

    return () => abortController.abort()
  }, [configuration, selectedProjectId, dataRevision])

  // A ortofoto entra acima do mapa base e abaixo das redes; some ao trocar de projeto.
  const orthophotoInfo =
    orthophoto?.projectId === selectedProjectId && orthophoto.state.kind === 'ready'
      ? orthophoto.state.info
      : null
  const orthophotoInstalled = useRef(false)
  useEffect(() => {
    const map = orthophotoTarget.current
    if (styleRevision === 0 || !map) return
    if (orthophotoInfo?.available) {
      installOrthophoto(map, orthophotoInfo, orthophotoDisplayRef.current)
      orthophotoInstalled.current = true
    } else if (orthophotoInstalled.current) {
      removeOrthophoto(map)
      orthophotoInstalled.current = false
    }
  }, [orthophotoInfo, styleRevision])

  const changeOrthophotoDisplay = (display: OrthophotoDisplay) => {
    orthophotoDisplayRef.current = display
    setOrthophotoDisplay(display)
    if (orthophotoTarget.current && orthophotoInstalled.current) {
      updateOrthophoto(orthophotoTarget.current, display)
    }
  }

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
  // dataRevision: depois de uma importação, as redes do mesmo projeto são buscadas de novo.
  }, [catalogConfig, styleRevision, dataRevision])

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

  // A localização guardada só vale para o projeto em que foi obtida; enquanto
  // a do projeto atual não chega, o botão fica em estado de carregamento.
  const currentLocation =
    projectLocation?.projectId === selectedProjectId ? projectLocation : null

  const locateSelectedProject = () => {
    if (currentLocation?.kind !== 'ready' || !locatorTarget.current) return
    locateProject(locatorTarget.current, currentLocation.location)
  }

  const changeProject = (projectId: string) => {
    setStartupProjectId(null)
    setSelectedProjectId(projectId)
  }

  const showImportedProject = (projectId: string) => {
    importedProjectId.current = projectId
    setDataRevision((revision) => revision + 1)
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
            locateDisabledReason={locateDisabledReason(currentLocation)}
          />
        )}
        {orthophoto && orthophoto.projectId === selectedProjectId && (
          <OrthophotoControl
            state={orthophoto.state}
            display={orthophotoDisplay}
            onDisplayChange={changeOrthophotoDisplay}
          />
        )}
        {configuration.kind === 'ready' && (
          <ImportPanel
            apiBaseUrl={configuration.value.apiBaseUrl}
            onImported={showImportedProject}
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
  state: ProjectLocationState | null,
): string | undefined {
  if (!state) return 'Carregando localização do projeto…'
  switch (state.kind) {
    case 'ready':
      return undefined
    case 'empty':
      return 'Projeto sem feições para localizar'
    case 'error':
      return state.message
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
