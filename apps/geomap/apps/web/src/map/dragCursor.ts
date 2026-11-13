/**
 * Classe aplicada ao contêiner do mapa enquanto o usuário arrasta.
 *
 * Sobre o mapa o cursor é a seta padrão; a mão fechada só aparece durante o
 * arrasto (Issue #56). O MapLibre usa a mão o tempo todo e a mão fechada em
 * qualquer clique, por isso o arrasto é sinalizado pelos eventos do mapa.
 */
export const MAP_DRAGGING_CLASS = 'map-canvas--dragging'

interface Subscription {
  unsubscribe(): void
}

export interface DragCursorTarget {
  getContainer(): { classList: Pick<DOMTokenList, 'add' | 'remove'> }
  on(type: 'dragstart' | 'dragend', listener: () => void): Subscription
}

export function installDragCursor(map: DragCursorTarget): () => void {
  const container = map.getContainer()
  const start = map.on('dragstart', () => container.classList.add(MAP_DRAGGING_CLASS))
  const end = map.on('dragend', () => container.classList.remove(MAP_DRAGGING_CLASS))

  return () => {
    start.unsubscribe()
    end.unsubscribe()
    container.classList.remove(MAP_DRAGGING_CLASS)
  }
}
