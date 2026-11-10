import { describe, expect, it, vi } from 'vitest'
import { MAP_DRAGGING_CLASS, installDragCursor, type DragCursorTarget } from './dragCursor'

function fakeMap() {
  const classes = new Set<string>()
  const listeners = new Map<string, () => void>()
  const unsubscribes: Array<ReturnType<typeof vi.fn>> = []
  const map: DragCursorTarget = {
    getContainer: () => ({
      classList: {
        add: (...tokens: string[]) => tokens.forEach((token) => classes.add(token)),
        remove: (...tokens: string[]) => tokens.forEach((token) => classes.delete(token)),
      },
    }),
    on: (type, listener) => {
      listeners.set(type, listener)
      const unsubscribe = vi.fn()
      unsubscribes.push(unsubscribe)
      return { unsubscribe }
    },
  }
  return { map, classes, emit: (type: string) => listeners.get(type)?.(), unsubscribes }
}

describe('drag cursor', () => {
  it('marks the map as dragging only between dragstart and dragend', () => {
    const { map, classes, emit } = fakeMap()
    installDragCursor(map)

    expect(classes.has(MAP_DRAGGING_CLASS)).toBe(false)
    emit('dragstart')
    expect(classes.has(MAP_DRAGGING_CLASS)).toBe(true)
    emit('dragend')
    expect(classes.has(MAP_DRAGGING_CLASS)).toBe(false)
  })

  it('removes listeners and the dragging class on cleanup', () => {
    const { map, classes, emit, unsubscribes } = fakeMap()
    const cleanup = installDragCursor(map)

    emit('dragstart')
    cleanup()

    expect(classes.has(MAP_DRAGGING_CLASS)).toBe(false)
    expect(unsubscribes).toHaveLength(2)
    unsubscribes.forEach((unsubscribe) => expect(unsubscribe).toHaveBeenCalledOnce())
  })
})
