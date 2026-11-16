import { afterEach, describe, expect, it, vi } from 'vitest'
import { buildImportUrl, importGeoMapFile } from './importFile'

const projectId = '60000000-0000-4000-8000-000000000001'

afterEach(() => {
  vi.unstubAllGlobals()
})

function geoMapFile(name = 'rede.geomap.json') {
  return new File(['{"contractVersion":"0.1"}'], name, { type: 'application/json' })
}

describe('importGeoMapFile', () => {
  it('posts the file as the request body and reports the imported features', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({ success: true, imported: 191, replaced: 191, projectId, errors: [] }),
    })
    vi.stubGlobal('fetch', fetchMock)
    const file = geoMapFile()

    await expect(importGeoMapFile('/api', file)).resolves.toEqual({
      kind: 'imported',
      fileName: 'rede.geomap.json',
      projectId,
      imported: 191,
      replaced: 191,
    })
    expect(buildImportUrl('/api')).toBe('/api/imports')
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/imports',
      expect.objectContaining({ method: 'POST', body: file }),
    )
  })

  it('returns the contract errors of a rejected file', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: false,
        status: 400,
        json: async () => ({
          success: false,
          errors: [{ code: 'GMC_CRS_INVALID', path: '$.sourceCrs', message: 'sourceCrs must use EPSG:<code>.' }],
        }),
      }),
    )

    await expect(importGeoMapFile('/api', geoMapFile('ruim.geomap.json'))).resolves.toEqual({
      kind: 'rejected',
      fileName: 'ruim.geomap.json',
      errors: [{ code: 'GMC_CRS_INVALID', message: 'sourceCrs must use EPSG:<code>.' }],
    })
  })

  it('explains when the API cannot be reached or answers something unexpected', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))
    const offline = await importGeoMapFile('/api', geoMapFile())
    expect(offline.kind).toBe('rejected')

    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({ ok: false, status: 502, json: async () => { throw new Error('html') } }),
    )
    await expect(importGeoMapFile('/api', geoMapFile())).resolves.toEqual({
      kind: 'rejected',
      fileName: 'rede.geomap.json',
      errors: [{ code: 'GM_NETWORK', message: 'Resposta inesperada da API (502)' }],
    })
  })
})
