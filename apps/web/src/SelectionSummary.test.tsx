import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { SelectionSummary } from './SelectionSummary'

describe('SelectionSummary', () => {
  it('shows the minimal identity and lets the user clear the selection', () => {
    const onClear = vi.fn()
    render(
      <SelectionSummary
        selection={{
          color: '#167c80',
          featureId: '62000000-0000-4000-8000-000000000001',
          networkId: '61000000-0000-4000-8000-000000000001',
          networkName: 'Rede de drenagem',
          networkType: 'stormwater',
        }}
        onClear={onClear}
      />,
    )

    expect(screen.getByLabelText('Elemento selecionado')).toHaveTextContent(
      'Rede de drenagem',
    )
    expect(screen.getByText('stormwater')).toBeInTheDocument()
    expect(
      screen.getByText('62000000-0000-4000-8000-000000000001'),
    ).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Limpar seleção' }))
    expect(onClear).toHaveBeenCalledOnce()
  })
})
