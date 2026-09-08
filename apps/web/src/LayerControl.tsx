import type { NetworkLayerView } from './map/networkLayer'

interface LayerControlProps {
  readonly layers: readonly NetworkLayerView[]
  readonly onHideAll: () => void
  readonly onShowAll: () => void
  readonly onVisibilityChange: (networkId: string, visible: boolean) => void
}

export function LayerControl({
  layers,
  onHideAll,
  onShowAll,
  onVisibilityChange,
}: LayerControlProps) {
  const visibleCount = layers.filter(({ visible }) => visible).length

  return (
    <aside className="layer-control" aria-label="Camadas de rede">
      <div className="layer-control__heading">
        <div>
          <span className="layer-control__eyebrow">Projeto atual</span>
          <h2>Camadas</h2>
        </div>
        <span className="layer-control__count">
          {visibleCount}/{layers.length}
        </span>
      </div>

      <div className="layer-control__actions" aria-label="Ações das camadas">
        <button
          type="button"
          onClick={onShowAll}
          disabled={visibleCount === layers.length}
        >
          Mostrar todas
        </button>
        <button type="button" onClick={onHideAll} disabled={visibleCount === 0}>
          Ocultar todas
        </button>
      </div>

      <ul className="layer-control__list">
        {layers.map((layer) => (
          <li key={layer.id} className="layer-control__item">
            <label>
              <input
                type="checkbox"
                checked={layer.visible}
                onChange={(event) =>
                  onVisibilityChange(layer.id, event.target.checked)
                }
              />
              <span
                className="layer-control__swatch"
                style={{ backgroundColor: layer.color }}
                aria-hidden="true"
              />
              <span className="layer-control__details">
                <strong>{layer.name}</strong>
                <span>{layer.type}</span>
              </span>
              <span
                className={`layer-control__state layer-control__state--${layer.status.kind}`}
              >
                {layer.status.message}
              </span>
            </label>
          </li>
        ))}
      </ul>
    </aside>
  )
}
