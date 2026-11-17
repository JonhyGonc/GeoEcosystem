import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { OrthophotoControl } from './OrthophotoControl'

afterEach(cleanup)

const available = {
  available: true as const,
  fileName: 'Obra.mbtiles',
  tileUrl: '/t/{z}/{x}/{y}',
  minZoom: 12,
  maxZoom: 21,
}

describe('OrthophotoControl', () => {
  it('toggles the orthophoto and changes its opacity', () => {
    const onDisplayChange = vi.fn()

    render(
      <OrthophotoControl
        state={{ kind: 'ready', info: available }}
        display={{ visible: true, opacity: 1 }}
        onDisplayChange={onDisplayChange}
      />,
    )

    fireEvent.click(screen.getByRole('checkbox', { name: 'Ortofoto' }))
    expect(onDisplayChange).toHaveBeenLastCalledWith({ visible: false, opacity: 1 })

    fireEvent.change(screen.getByRole('slider', { name: 'Opacidade da ortofoto' }), {
      target: { value: '40' },
    })
    expect(onDisplayChange).toHaveBeenLastCalledWith({ visible: true, opacity: 0.4 })
  })

  it('says which file to put in the orthophoto folder', () => {
    render(
      <OrthophotoControl
        state={{ kind: 'ready', info: { available: false, fileName: 'Obra GF-CV-02.mbtiles' } }}
        display={{ visible: true, opacity: 1 }}
        onDisplayChange={vi.fn()}
      />,
    )

    expect(screen.getByText('Obra GF-CV-02.mbtiles')).toBeInTheDocument()
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
  })
})
