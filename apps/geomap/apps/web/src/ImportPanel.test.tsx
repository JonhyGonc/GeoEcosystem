import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ImportPanel } from './ImportPanel'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

const projectId = '60000000-0000-4000-8000-000000000001'

function file(name: string) {
  return new File(['{}'], name, { type: 'application/json' })
}

describe('ImportPanel', () => {
  it('imports each chosen file and opens the imported project', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({ success: true, imported: 191, replaced: 0, projectId, errors: [] }),
      })
      .mockResolvedValueOnce({
        ok: false,
        status: 400,
        json: async () => ({
          success: false,
          errors: [{ code: 'GMC_CRS_INVALID', message: 'CRS inválido' }],
        }),
      })
    vi.stubGlobal('fetch', fetchMock)
    const onImported = vi.fn()

    render(<ImportPanel apiBaseUrl="/api" onImported={onImported} />)
    fireEvent.change(screen.getByTestId('import-file-input'), {
      target: { files: [file('linhas.geomap.json'), file('ruim.geomap.json')] },
    })

    const results = await screen.findByRole('list', { name: 'Resultado da importação' })
    await waitFor(() => expect(onImported).toHaveBeenCalledWith(projectId))
    expect(fetchMock).toHaveBeenCalledTimes(2)
    expect(results).toHaveTextContent('linhas.geomap.json importado: 191 feição(ões)')
    expect(results).toHaveTextContent('ruim.geomap.json recusado: CRS inválido (GMC_CRS_INVALID)')
    expect(screen.getByRole('button', { name: 'Importar arquivos .geomap.json' })).toBeEnabled()
  })

  it('does not reload the map when every file is rejected', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: false,
        status: 400,
        json: async () => ({ success: false, errors: [{ code: 'GMC_CRS_INVALID', message: 'CRS inválido' }] }),
      }),
    )
    const onImported = vi.fn()

    render(<ImportPanel apiBaseUrl="/api" onImported={onImported} />)
    fireEvent.change(screen.getByTestId('import-file-input'), {
      target: { files: [file('ruim.geomap.json')] },
    })

    expect(await screen.findByText(/recusado/)).toBeInTheDocument()
    expect(onImported).not.toHaveBeenCalled()
  })
})
