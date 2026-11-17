import { afterEach, describe, expect, it, vi } from 'vitest'
import { buildOrthophotoUrl, fetchOrthophoto } from './orthophoto'

const projectId = '60000000-0000-4000-8000-000000000001'

afterEach(() => {
  vi.unstubAllGlobals()
})

function stub(body: unknown, ok = true, status = 200) {
  const fetchMock = vi.fn().mockResolvedValue({ ok, status, json: async () => body })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

describe('fetchOrthophoto', () => {
  it('describes an available orthophoto with tiles under the configured API', async () => {
    const fetchMock = stub({
      available: true,
      fileName: 'Obra.mbtiles',
      tileUrl: '/ignored',
      format: 'webp',
      bounds: [-39.1, -14.2, -39.0, -14.1],
      minZoom: 12,
      maxZoom: 21,
      version: '638700000000000000',
    })

    await expect(fetchOrthophoto('/api', projectId)).resolves.toEqual({
      available: true,
      fileName: 'Obra.mbtiles',
      tileUrl: `/api/projects/${projectId}/orthophoto/tiles/{z}/{x}/{y}?v=638700000000000000`,
      bounds: [-39.1, -14.2, -39.0, -14.1],
      minZoom: 12,
      maxZoom: 21,
    })
    expect(fetchMock).toHaveBeenCalledWith(buildOrthophotoUrl('/api', projectId), expect.anything())
  })

  it('tells which file is missing', async () => {
    stub({ available: false, fileName: 'Obra GF-CV-02.mbtiles' })

    await expect(fetchOrthophoto('/api', projectId)).resolves.toEqual({
      available: false,
      fileName: 'Obra GF-CV-02.mbtiles',
    })
  })

  it('ignores bad bounds and zooms instead of breaking the map', async () => {
    stub({ available: true, fileName: 'a.mbtiles', bounds: [1, 2, 0, 3], minZoom: 30, maxZoom: 'x' })

    const info = await fetchOrthophoto('/api', projectId)

    expect(info).toMatchObject({ available: true, minZoom: 0, maxZoom: 22 })
    expect(info.available && info.bounds).toBeUndefined()
  })

  it('fails with the HTTP status', async () => {
    stub({}, false, 404)

    await expect(fetchOrthophoto('/api', projectId)).rejects.toThrow('Ortofoto indisponível (404)')
  })
})
