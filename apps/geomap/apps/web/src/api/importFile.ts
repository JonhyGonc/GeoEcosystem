/**
 * Importação de um `.geomap.json` pela API (M4.5): o arquivo vai inteiro no corpo de
 * `POST {apiBaseUrl}/imports`, e a resposta é o mesmo resumo do importador do terminal.
 */
export interface ImportError {
  readonly code: string
  readonly message: string
}

export type ImportFileResult =
  | {
      readonly kind: 'imported'
      readonly fileName: string
      readonly projectId: string
      readonly imported: number
      readonly replaced: number
    }
  | {
      readonly kind: 'rejected'
      readonly fileName: string
      readonly errors: readonly ImportError[]
    }

export function buildImportUrl(apiBaseUrl: string): string {
  return `${apiBaseUrl}/imports`
}

export async function importGeoMapFile(
  apiBaseUrl: string,
  file: File,
  signal?: AbortSignal,
): Promise<ImportFileResult> {
  let response: Response
  try {
    response = await fetch(buildImportUrl(apiBaseUrl), {
      method: 'POST',
      headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
      body: file,
      signal,
    })
  } catch (error) {
    if (signal?.aborted) throw error
    return rejected(file.name, 'Não foi possível falar com a API do GeoMap')
  }

  const payload: unknown = await response.json().catch(() => null)
  if (!isRecord(payload) || typeof payload.success !== 'boolean') {
    return rejected(file.name, `Resposta inesperada da API (${response.status})`)
  }

  if (payload.success && response.ok && typeof payload.projectId === 'string') {
    return {
      kind: 'imported',
      fileName: file.name,
      projectId: payload.projectId,
      imported: numberOrZero(payload.imported),
      replaced: numberOrZero(payload.replaced),
    }
  }

  const errors = Array.isArray(payload.errors)
    ? payload.errors.filter(isRecord).map((error) => ({
        code: typeof error.code === 'string' ? error.code : 'GM_UNKNOWN',
        message: typeof error.message === 'string' ? error.message : '',
      }))
    : []
  return {
    kind: 'rejected',
    fileName: file.name,
    errors: errors.length > 0 ? errors : [{ code: 'GM_UNKNOWN', message: `Falha na importação (${response.status})` }],
  }
}

function rejected(fileName: string, message: string): ImportFileResult {
  return { kind: 'rejected', fileName, errors: [{ code: 'GM_NETWORK', message }] }
}

function numberOrZero(value: unknown): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : 0
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null
}
