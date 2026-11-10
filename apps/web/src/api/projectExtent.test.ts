import { afterEach, describe, expect, it, vi } from 'vitest'
import { buildProjectExtentUrl, fetchProjectLocation } from './projectExtent'

const projectId = '60000000-0000-4000-8000-000000000001'
const extent = {
  minLongitude: -48.9,
  minLatitude: -26.35,
  maxLongitude: -48.71,
  maxLatitude: -26.14,
}
const focus = { longitude: -48.8, latitude: -26.25 }

function stubResponse(body: unknown, ok = true, status = 200) {
  const fetchMock = vi.fn().mockResolvedValue({ ok, status, json: async () => body })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('project extent', () => {
  it('fetches the extent and focus of a project', async () => {
    const fetchMock = stubResponse({ projectId, extent, focus })

    await expect(fetchProjectLocation('/api', projectId)).resolves.toEqual({
      extent,
      focus,
    })
    expect(buildProjectExtentUrl('/api', projectId)).toBe(
      `/api/projects/${projectId}/extent`,
    )
    expect(fetchMock).toHaveBeenCalledWith(
      `/api/projects/${projectId}/extent`,
      expect.objectContaining({ headers: { Accept: 'application/json' } }),
    )
  })

  it('returns null for a project without features', async () => {
    stubResponse({ projectId, extent: null, focus: null })

    await expect(fetchProjectLocation('/api', projectId)).resolves.toBeNull()
  })

  it.each([
    { projectId: '60000000-0000-4000-8000-000000000002', extent, focus },
    { projectId, extent: { ...extent, minLongitude: -48.5 }, focus },
    { projectId, extent: { ...extent, maxLatitude: 120 }, focus },
    { projectId, extent, focus: null },
    { projectId, extent: null, focus },
  ])('rejects an invalid response: %o', async (payload) => {
    stubResponse(payload)

    await expect(fetchProjectLocation('/api', projectId)).rejects.toThrow(
      'Resposta inválida da extensão do projeto',
    )
  })

  it('reports an unavailable extent', async () => {
    stubResponse(null, false, 503)

    await expect(fetchProjectLocation('/api', projectId)).rejects.toThrow(
      'Extensão do projeto indisponível (503)',
    )
  })
})
