import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { LayerControl } from './LayerControl'
import type { NetworkLayerView } from './map/networkLayer'

afterEach(cleanup)

function layer(index: number, visible = true): NetworkLayerView {
  return {
    id: `61000000-0000-4000-8000-${String(index).padStart(12, '0')}`,
    name: `Rede ${index}`,
    type: index % 2 === 0 ? 'drenagem' : 'esgoto',
    color: '#167c80',
    visible,
    status: visible
      ? { kind: 'ready', message: 'Visível' }
      : { kind: 'hidden', message: 'Oculta' },
  }
}

describe('LayerControl', () => {
  it('keeps individual and combined actions usable with several networks', () => {
    const layers = Array.from({ length: 12 }, (_, index) => layer(index + 1))
    const onVisibilityChange = vi.fn()
    const onShowAll = vi.fn()
    const onHideAll = vi.fn()

    render(
      <LayerControl
        layers={layers}
        onVisibilityChange={onVisibilityChange}
        onShowAll={onShowAll}
        onHideAll={onHideAll}
      />,
    )

    expect(screen.getByLabelText('Camadas de rede')).toBeInTheDocument()
    expect(screen.getByText('12/12')).toBeInTheDocument()
    expect(screen.getAllByRole('checkbox')).toHaveLength(12)
    fireEvent.click(screen.getByRole('checkbox', { name: /Rede 7/ }))
    expect(onVisibilityChange).toHaveBeenCalledWith(layers[6].id, false)
    fireEvent.click(screen.getByRole('button', { name: 'Ocultar todas' }))
    expect(onHideAll).toHaveBeenCalledOnce()
    expect(screen.getByRole('button', { name: 'Mostrar todas' })).toBeDisabled()
  })

  it('enables showing all when some layers are hidden', () => {
    render(
      <LayerControl
        layers={[layer(1), layer(2, false)]}
        onVisibilityChange={vi.fn()}
        onShowAll={vi.fn()}
        onHideAll={vi.fn()}
      />,
    )

    expect(screen.getByText('1/2')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Mostrar todas' })).toBeEnabled()
    expect(screen.getByRole('checkbox', { name: /Rede 2/ })).not.toBeChecked()
  })
})
