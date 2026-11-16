import { useRef, useState } from 'react'
import { importGeoMapFile, type ImportFileResult } from './api/importFile'

interface ImportPanelProps {
  readonly apiBaseUrl: string
  /** Chamado ao fim, com o projeto do último arquivo importado com sucesso. */
  readonly onImported: (projectId: string) => void
}

type PanelState =
  | { readonly kind: 'idle' }
  | { readonly kind: 'importing'; readonly current: string; readonly results: readonly ImportFileResult[] }
  | { readonly kind: 'done'; readonly results: readonly ImportFileResult[] }

/**
 * Botão "Importar" (M4.5): envia um ou mais `.geomap.json` (por exemplo, as linhas e o arquivo
 * `-blocos` do GeoAutoEngine), um de cada vez, e mostra o resultado de cada arquivo.
 */
export function ImportPanel({ apiBaseUrl, onImported }: ImportPanelProps) {
  const input = useRef<HTMLInputElement>(null)
  const [state, setState] = useState<PanelState>({ kind: 'idle' })

  const importFiles = async (files: readonly File[]) => {
    const results: ImportFileResult[] = []
    for (const file of files) {
      setState({ kind: 'importing', current: file.name, results: [...results] })
      results.push(await importGeoMapFile(apiBaseUrl, file))
    }
    setState({ kind: 'done', results })

    const lastImported = results.filter((result) => result.kind === 'imported').at(-1)
    if (lastImported) onImported(lastImported.projectId)
  }

  const results = state.kind === 'idle' ? [] : state.results

  return (
    <div className="import-panel">
      <input
        ref={input}
        type="file"
        accept=".json,application/json"
        multiple
        hidden
        data-testid="import-file-input"
        onChange={(event) => {
          const files = Array.from(event.target.files ?? [])
          event.target.value = ''
          if (files.length > 0) void importFiles(files)
        }}
      />
      <button
        type="button"
        className="import-panel__button"
        onClick={() => input.current?.click()}
        disabled={state.kind === 'importing'}
      >
        {state.kind === 'importing' ? `Importando ${state.current}…` : 'Importar arquivos .geomap.json'}
      </button>
      {results.length > 0 && (
        <ul className="import-panel__results" aria-label="Resultado da importação" aria-live="polite">
          {results.map((result, index) => (
            <li key={`${index}-${result.fileName}`} className={`import-panel__result import-panel__result--${result.kind}`}>
              <strong>{result.fileName}</strong>{' '}
              {result.kind === 'imported'
                ? `importado: ${result.imported} feição(ões)${result.replaced > 0 ? `, ${result.replaced} substituída(s)` : ''}`
                : `recusado: ${result.errors.map((error) => `${error.message} (${error.code})`).join('; ')}`}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
