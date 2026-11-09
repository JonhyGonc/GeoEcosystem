const UUID_PATTERN =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i

export interface ProjectDefinition {
  readonly id: string
  readonly name: string
}

export type ProjectCatalogState =
  | { readonly kind: 'loading' }
  | { readonly kind: 'ready'; readonly projects: readonly ProjectDefinition[] }
  | { readonly kind: 'error'; readonly message: string }

export function buildProjectCatalogUrl(apiBaseUrl: string): string {
  return `${apiBaseUrl}/projects`
}

export async function fetchProjects(
  apiBaseUrl: string,
  signal?: AbortSignal,
): Promise<readonly ProjectDefinition[]> {
  const response = await fetch(buildProjectCatalogUrl(apiBaseUrl), {
    headers: { Accept: 'application/json' },
    signal,
  })
  if (!response.ok) {
    throw new Error(`Catálogo de projetos indisponível (${response.status})`)
  }

  const payload: unknown = await response.json()
  return validateProjectCatalog(payload)
}

/**
 * Abre o projeto preferido quando ele existe no catálogo; caso contrário,
 * abre o primeiro projeto na ordem retornada pela API.
 */
export function chooseInitialProjectId(
  projects: readonly ProjectDefinition[],
  preferredProjectId?: string,
): string | null {
  if (
    preferredProjectId &&
    projects.some(({ id }) => id === preferredProjectId)
  ) {
    return preferredProjectId
  }
  return projects[0]?.id ?? null
}

function validateProjectCatalog(payload: unknown): ProjectDefinition[] {
  if (!isRecord(payload) || !Array.isArray(payload.projects)) {
    throw new Error('Resposta inválida do catálogo de projetos')
  }

  const ids = new Set<string>()
  return payload.projects.map((item) => {
    if (
      !isRecord(item) ||
      typeof item.id !== 'string' ||
      !UUID_PATTERN.test(item.id) ||
      typeof item.name !== 'string' ||
      !item.name.trim() ||
      ids.has(item.id)
    ) {
      throw new Error('Resposta inválida do catálogo de projetos')
    }
    ids.add(item.id)
    return { id: item.id, name: item.name.trim() }
  })
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null
}
