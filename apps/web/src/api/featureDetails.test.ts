import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  buildFeatureDetailsUrl,
  fetchFeatureDetails,
} from './featureDetails'

const config = {
  apiBaseUrl: '/api',
  projectId: '60000000-0000-4000-8000-000000000001',
}
const selection = {
  color: '#167c80',
  featureId: '62000000-0000-4000-8000-000000000001',
  networkId: '61000000-0000-4000-8000-000000000001',
  networkName: 'Rede de drenagem',
  networkType: 'stormwater',
}
const payload = {
  projectId: config.projectId,
  id: selection.featureId,
  networkId: selection.networkId,
  networkName: selection.networkName,
  networkType: selection.networkType,
  sourceId: 'DREN-001',
  sourceLayer: 'DRENAGEM',
  sourceType: 'Polyline',
  properties: { material: 'PVC', diameter: 300 },
  createdAt: '2026-09-08T12:00:00Z',
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('feature details API', () => {
  it('loads the selected feature only within its project and network', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => payload,
    })
    vi.stubGlobal('fetch', fetchMock)

    await expect(fetchFeatureDetails(config, selection)).resolves.toEqual(payload)
    expect(buildFeatureDetailsUrl(config, selection)).toBe(
      `/api/projects/${config.projectId}/networks/${selection.networkId}/features/${selection.featureId}`,
    )
    expect(fetchMock).toHaveBeenCalledWith(
      buildFeatureDetailsUrl(config, selection),
      expect.objectContaining({ headers: { Accept: 'application/json' } }),
    )
  })

  it('maps an absent feature to null', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({ ok: false, status: 404 }),
    )

    await expect(fetchFeatureDetails(config, selection)).resolves.toBeNull()
  })

  it('rejects a response from another project, network or feature', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ ...payload, projectId: '60000000-0000-4000-8000-000000000002' }),
      }),
    )

    await expect(fetchFeatureDetails(config, selection)).rejects.toThrow(
      'Resposta inválida dos atributos da feição',
    )
  })
})
