export interface ProjectExtent {
  readonly minLongitude: number
  readonly minLatitude: number
  readonly maxLongitude: number
  readonly maxLatitude: number
}

export interface ProjectFocus {
  readonly longitude: number
  readonly latitude: number
}

export interface ProjectLocation {
  readonly extent: ProjectExtent
  readonly focus: ProjectFocus
}

export function buildProjectExtentUrl(
  apiBaseUrl: string,
  projectId: string,
): string {
  return `${apiBaseUrl}/projects/${encodeURIComponent(projectId)}/extent`
}

/** Retorna `null` quando o projeto não possui feições. */
export async function fetchProjectLocation(
  apiBaseUrl: string,
  projectId: string,
  signal?: AbortSignal,
): Promise<ProjectLocation | null> {
  const response = await fetch(buildProjectExtentUrl(apiBaseUrl, projectId), {
    headers: { Accept: 'application/json' },
    signal,
  })
  if (!response.ok) {
    throw new Error(`Extensão do projeto indisponível (${response.status})`)
  }

  const payload: unknown = await response.json()
  return validateProjectLocation(payload, projectId)
}

function validateProjectLocation(
  payload: unknown,
  projectId: string,
): ProjectLocation | null {
  if (!isRecord(payload) || payload.projectId !== projectId) {
    throw new Error('Resposta inválida da extensão do projeto')
  }
  if (payload.extent === null && payload.focus === null) return null

  const { extent, focus } = payload
  if (
    !isRecord(extent) ||
    !isRecord(focus) ||
    !isLongitude(extent.minLongitude) ||
    !isLongitude(extent.maxLongitude) ||
    !isLatitude(extent.minLatitude) ||
    !isLatitude(extent.maxLatitude) ||
    extent.minLongitude > extent.maxLongitude ||
    extent.minLatitude > extent.maxLatitude ||
    !isLongitude(focus.longitude) ||
    !isLatitude(focus.latitude)
  ) {
    throw new Error('Resposta inválida da extensão do projeto')
  }

  return {
    extent: {
      minLongitude: extent.minLongitude,
      minLatitude: extent.minLatitude,
      maxLongitude: extent.maxLongitude,
      maxLatitude: extent.maxLatitude,
    },
    focus: { longitude: focus.longitude, latitude: focus.latitude },
  }
}

function isLongitude(value: unknown): value is number {
  return typeof value === 'number' && Number.isFinite(value) && Math.abs(value) <= 180
}

function isLatitude(value: unknown): value is number {
  return typeof value === 'number' && Number.isFinite(value) && Math.abs(value) <= 90
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null
}
