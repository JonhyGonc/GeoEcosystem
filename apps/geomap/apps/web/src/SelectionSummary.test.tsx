import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { SelectionSummary } from './SelectionSummary'

describe('SelectionSummary', () => {
  const selection = {
    color: '#167c80',
    featureId: '62000000-0000-4000-8000-000000000001',
    networkId: '61000000-0000-4000-8000-000000000001',
    networkName: 'Rede de drenagem',
    networkType: 'stormwater',
  }

  it('shows basic metadata and variable properties without editing controls', () => {
    const onClear = vi.fn()
    render(
      <SelectionSummary
        selection={selection}
        detailsState={{
          kind: 'ready',
          details: {
            projectId: '60000000-0000-4000-8000-000000000001',
            id: selection.featureId,
            networkId: selection.networkId,
            networkName: selection.networkName,
            networkType: selection.networkType,
            sourceId: 'DREN-001',
            sourceLayer: 'DRENAGEM',
            sourceType: 'Polyline',
            createdAt: '2026-09-08T12:00:00Z',
            properties: {
              material: 'PVC',
              diameter: 300,
              inspected: true,
            },
          },
        }}
        onClear={onClear}
        onRetry={vi.fn()}
      />,
    )

    expect(screen.getByLabelText('Elemento selecionado')).toHaveTextContent(
      'Rede de drenagem',
    )
    expect(screen.getByText('stormwater')).toBeInTheDocument()
    expect(
      screen.getByText('62000000-0000-4000-8000-000000000001'),
    ).toBeInTheDocument()
    expect(screen.getByText('PVC')).toBeInTheDocument()
    expect(screen.getByText('300')).toBeInTheDocument()
    expect(screen.getByText('Sim')).toBeInTheDocument()
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Limpar seleção' }))
    expect(onClear).toHaveBeenCalledOnce()
  })

  it('handles loading, empty, missing and retryable error states', () => {
    const { rerender } = render(
      <SelectionSummary
        selection={selection}
        detailsState={{ kind: 'loading' }}
        onClear={vi.fn()}
        onRetry={vi.fn()}
      />,
    )
    expect(screen.getByText('Carregando atributos…')).toBeInTheDocument()

    rerender(
      <SelectionSummary
        selection={selection}
        detailsState={{
          kind: 'ready',
          details: {
            projectId: '60000000-0000-4000-8000-000000000001',
            id: selection.featureId,
            networkId: selection.networkId,
            networkName: selection.networkName,
            networkType: selection.networkType,
            sourceId: null,
            sourceLayer: null,
            sourceType: 'Polyline',
            createdAt: '2026-09-08T12:00:00Z',
            properties: {},
          },
        }}
        onClear={vi.fn()}
        onRetry={vi.fn()}
      />,
    )
    expect(screen.getByText('Nenhum atributo adicional.')).toBeInTheDocument()

    rerender(
      <SelectionSummary
        selection={selection}
        detailsState={{ kind: 'not-found' }}
        onClear={vi.fn()}
        onRetry={vi.fn()}
      />,
    )
    expect(screen.getByText('A feição não foi encontrada.')).toBeInTheDocument()

    const onRetry = vi.fn()
    rerender(
      <SelectionSummary
        selection={selection}
        detailsState={{ kind: 'error', message: 'Serviço indisponível' }}
        onClear={vi.fn()}
        onRetry={onRetry}
      />,
    )
    fireEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }))
    expect(onRetry).toHaveBeenCalledOnce()
  })
})
