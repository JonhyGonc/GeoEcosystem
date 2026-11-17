import type { OrthophotoInfo } from './api/orthophoto'
import type { OrthophotoDisplay } from './map/orthophotoLayer'

export type OrthophotoState =
  | { readonly kind: 'loading' }
  | { readonly kind: 'ready'; readonly info: OrthophotoInfo }
  | { readonly kind: 'error'; readonly message: string }

interface OrthophotoControlProps {
  readonly state: OrthophotoState
  readonly display: OrthophotoDisplay
  readonly onDisplayChange: (display: OrthophotoDisplay) => void
}

/** Liga, desliga e ajusta a transparência da ortofoto do projeto (DEC-035). */
export function OrthophotoControl({ state, display, onDisplayChange }: OrthophotoControlProps) {
  if (state.kind === 'loading') {
    return <p className="orthophoto-control__hint">Procurando ortofoto…</p>
  }

  if (state.kind === 'error') {
    return <p className="orthophoto-control__hint orthophoto-control__hint--error">{state.message}</p>
  }

  if (!state.info.available) {
    return (
      <p className="orthophoto-control__hint">
        Sem ortofoto. Para mostrar, coloque <code>{state.info.fileName}</code> na pasta das ortofotos.
      </p>
    )
  }

  return (
    <div className="orthophoto-control">
      <label className="orthophoto-control__toggle">
        <input
          type="checkbox"
          checked={display.visible}
          onChange={(event) => onDisplayChange({ ...display, visible: event.target.checked })}
        />
        Ortofoto
      </label>
      <label className="orthophoto-control__opacity">
        <span>Opacidade</span>
        <input
          type="range"
          min={0}
          max={100}
          step={5}
          value={Math.round(display.opacity * 100)}
          disabled={!display.visible}
          aria-label="Opacidade da ortofoto"
          onChange={(event) => onDisplayChange({ ...display, opacity: Number(event.target.value) / 100 })}
        />
      </label>
    </div>
  )
}
