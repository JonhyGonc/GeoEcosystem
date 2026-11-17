/**
 * Ortofoto do projeto (DEC-035): a API diz se existe o arquivo `<nome do projeto>.mbtiles` na pasta das
 * ortofotos e, se existir, onde ficam os tiles e até que zoom eles vão.
 */
export type OrthophotoInfo =
  | {
      readonly available: true
      readonly fileName: string
      readonly tileUrl: string
      readonly bounds?: readonly [number, number, number, number]
      readonly minZoom: number
      readonly maxZoom: number
    }
  | { readonly available: false; readonly fileName: string }

export function buildOrthophotoUrl(apiBaseUrl: string, projectId: string): string {
  return `${apiBaseUrl}/projects/${encodeURIComponent(projectId)}/orthophoto`
}

export function buildOrthophotoTileUrl(apiBaseUrl: string, projectId: string): string {
  return `${buildOrthophotoUrl(apiBaseUrl, projectId)}/tiles/{z}/{x}/{y}`
}

export async function fetchOrthophoto(
  apiBaseUrl: string,
  projectId: string,
  signal?: AbortSignal,
): Promise<OrthophotoInfo> {
  const response = await fetch(buildOrthophotoUrl(apiBaseUrl, projectId), {
    headers: { Accept: 'application/json' },
    signal,
  })
  if (!response.ok) {
    throw new Error(`Ortofoto indisponível (${response.status})`)
  }

  const payload: unknown = await response.json()
  if (!isRecord(payload) || typeof payload.available !== 'boolean' || typeof payload.fileName !== 'string') {
    throw new Error('Resposta inválida da ortofoto')
  }

  if (!payload.available) {
    return { available: false, fileName: payload.fileName }
  }

  const minZoom = zoom(payload.minZoom, 0)
  const maxZoom = Math.max(minZoom, zoom(payload.maxZoom, 22))
  return {
    available: true,
    fileName: payload.fileName,
    tileUrl: buildOrthophotoTileUrl(apiBaseUrl, projectId),
    bounds: bounds(payload.bounds),
    minZoom,
    maxZoom,
  }
}

function zoom(value: unknown, fallback: number): number {
  return typeof value === 'number' && Number.isInteger(value) && value >= 0 && value <= 24 ? value : fallback
}

function bounds(value: unknown): readonly [number, number, number, number] | undefined {
  if (
    Array.isArray(value) &&
    value.length === 4 &&
    value.every((item) => typeof item === 'number' && Number.isFinite(item))
  ) {
    const [west, south, east, north] = value as number[]
    if (west < east && south < north) return [west, south, east, north]
  }
  return undefined
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null
}
