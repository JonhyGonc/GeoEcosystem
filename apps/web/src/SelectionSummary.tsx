import type { SelectedNetworkFeature } from './map/networkLayer'
import type { FeatureDetailsState } from './api/featureDetails'

interface SelectionSummaryProps {
  readonly selection: SelectedNetworkFeature
  readonly detailsState: FeatureDetailsState
  readonly onClear: () => void
  readonly onRetry: () => void
}

export function SelectionSummary({
  selection,
  detailsState,
  onClear,
  onRetry,
}: SelectionSummaryProps) {
  return (
    <aside
      className="selection-summary"
      aria-label="Elemento selecionado"
      aria-live="polite"
    >
      <div className="selection-summary__heading">
        <span
          className="selection-summary__swatch"
          style={{ backgroundColor: selection.color }}
          aria-hidden="true"
        />
        <div>
          <span className="selection-summary__eyebrow">Elemento selecionado</span>
          <strong>{selection.networkName}</strong>
        </div>
      </div>
      <dl>
        <div>
          <dt>Tipo</dt>
          <dd>{selection.networkType}</dd>
        </div>
        <div>
          <dt>ID</dt>
          <dd title={selection.featureId}>{selection.featureId}</dd>
        </div>
      </dl>
      <section className="selection-summary__attributes" aria-label="Atributos">
        <h3>Atributos</h3>
        <FeatureAttributes state={detailsState} onRetry={onRetry} />
      </section>
      <button
        className="selection-summary__clear"
        type="button"
        onClick={onClear}
      >
        Limpar seleção
      </button>
    </aside>
  )
}

function FeatureAttributes({
  state,
  onRetry,
}: {
  readonly state: FeatureDetailsState
  readonly onRetry: () => void
}) {
  if (state.kind === 'idle' || state.kind === 'loading') {
    return <p className="selection-summary__message">Carregando atributos…</p>
  }
  if (state.kind === 'not-found') {
    return (
      <p className="selection-summary__message">
        A feição não foi encontrada.
      </p>
    )
  }
  if (state.kind === 'error') {
    return (
      <div className="selection-summary__feedback" role="alert">
        <p>{state.message}</p>
        <button type="button" onClick={onRetry}>
          Tentar novamente
        </button>
      </div>
    )
  }

  const properties = Object.entries(state.details.properties).sort(([left], [right]) =>
    left.localeCompare(right, 'pt-BR'),
  )
  return (
    <>
      <dl className="selection-summary__metadata">
        <Detail label="Origem" value={state.details.sourceType} />
        <Detail label="ID de origem" value={state.details.sourceId} />
        <Detail label="Camada de origem" value={state.details.sourceLayer} />
        <Detail
          label="Registrado em"
          value={new Intl.DateTimeFormat('pt-BR', {
            dateStyle: 'short',
            timeStyle: 'short',
          }).format(new Date(state.details.createdAt))}
        />
      </dl>
      {properties.length === 0 ? (
        <p className="selection-summary__message">
          Nenhum atributo adicional.
        </p>
      ) : (
        <dl className="selection-summary__properties">
          {properties.map(([name, value]) => (
            <Detail key={name} label={name} value={formatValue(value)} />
          ))}
        </dl>
      )}
    </>
  )
}

function Detail({
  label,
  value,
}: {
  readonly label: string
  readonly value: string | null
}) {
  return (
    <div>
      <dt>{label}</dt>
      <dd title={value ?? undefined}>{value || '—'}</dd>
    </div>
  )
}

function formatValue(value: unknown): string {
  if (value === null || value === undefined || value === '') return '—'
  if (typeof value === 'boolean') return value ? 'Sim' : 'Não'
  if (typeof value === 'string' || typeof value === 'number') return String(value)
  return JSON.stringify(value)
}
