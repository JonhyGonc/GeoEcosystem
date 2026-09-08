import type { SelectedNetworkFeature } from './map/networkLayer'

interface SelectionSummaryProps {
  readonly selection: SelectedNetworkFeature
  readonly onClear: () => void
}

export function SelectionSummary({
  selection,
  onClear,
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
      <button type="button" onClick={onClear}>
        Limpar seleção
      </button>
    </aside>
  )
}
