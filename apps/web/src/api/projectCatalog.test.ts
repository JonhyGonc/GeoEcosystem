import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  buildProjectCatalogUrl,
  chooseInitialProjectId,
  fetchProjects,
  type ProjectDefinition,
} from './projectCatalog'

const projects: ProjectDefinition[] = [
  { id: '60000000-0000-4000-8000-000000000001', name: 'Projeto Alfa' },
  { id: '60000000-0000-4000-8000-000000000002', name: 'Projeto Beta' },
]

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('project catalog', () => {
  it('fetches and validates the project list', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        projects: [
          { id: projects[0].id, name: '  Projeto Alfa ' },
          projects[1],
        ],
      }),
    })
    vi.stubGlobal('fetch', fetchMock)

    await expect(fetchProjects('/api')).resolves.toEqual(projects)
    expect(buildProjectCatalogUrl('/api')).toBe('/api/projects')
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/projects',
      expect.objectContaining({ headers: { Accept: 'application/json' } }),
    )
  })

  it('accepts an empty catalog', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({ ok: true, json: async () => ({ projects: [] }) }),
    )

    await expect(fetchProjects('/api')).resolves.toEqual([])
  })

  it.each([
    {},
    { projects: [projects[0], projects[0]] },
    { projects: [{ id: 'invalid', name: 'Projeto' }] },
    { projects: [{ id: projects[0].id, name: '   ' }] },
  ])('rejects an invalid catalog: %o', async (payload) => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({ ok: true, json: async () => payload }),
    )

    await expect(fetchProjects('/api')).rejects.toThrow(
      'Resposta inválida do catálogo de projetos',
    )
  })

  it('reports an unavailable catalog', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false, status: 503 }))

    await expect(fetchProjects('/api')).rejects.toThrow(
      'Catálogo de projetos indisponível (503)',
    )
  })

  it('opens the preferred project when it exists, otherwise the first one', () => {
    expect(chooseInitialProjectId(projects, projects[1].id)).toBe(projects[1].id)
    expect(
      chooseInitialProjectId(projects, '60000000-0000-4000-8000-000000000099'),
    ).toBe(projects[0].id)
    expect(chooseInitialProjectId(projects)).toBe(projects[0].id)
    expect(chooseInitialProjectId([], projects[0].id)).toBeNull()
  })
})
