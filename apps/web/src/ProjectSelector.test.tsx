import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ProjectSelector } from './ProjectSelector'

afterEach(cleanup)

const projects = [
  { id: '60000000-0000-4000-8000-000000000001', name: 'Projeto Alfa' },
  { id: '60000000-0000-4000-8000-000000000002', name: 'Projeto Beta' },
]

describe('ProjectSelector', () => {
  it('lists the projects and reports the chosen one', () => {
    const onProjectChange = vi.fn()

    render(
      <ProjectSelector
        projects={projects}
        selectedProjectId={projects[0].id}
        onProjectChange={onProjectChange}
        onLocate={vi.fn()}
      />,
    )

    const select = screen.getByRole('combobox', { name: 'Projeto' })
    expect(select).toHaveValue(projects[0].id)
    expect(select).toBeEnabled()
    expect(screen.getAllByRole('option').map((option) => option.textContent)).toEqual([
      'Projeto Alfa',
      'Projeto Beta',
    ])

    fireEvent.change(select, { target: { value: projects[1].id } })
    expect(onProjectChange).toHaveBeenCalledWith(projects[1].id)
  })

  it('shows a single project without offering a change', () => {
    render(
      <ProjectSelector
        projects={[projects[0]]}
        selectedProjectId={projects[0].id}
        onProjectChange={vi.fn()}
        onLocate={vi.fn()}
      />,
    )

    expect(screen.getByRole('combobox', { name: 'Projeto' })).toBeDisabled()
  })

  it('locates the project and explains when it cannot', () => {
    const onLocate = vi.fn()
    const { rerender } = render(
      <ProjectSelector
        projects={projects}
        selectedProjectId={projects[0].id}
        onProjectChange={vi.fn()}
        onLocate={onLocate}
      />,
    )

    const button = screen.getByRole('button', { name: 'Localizar projeto no mapa' })
    expect(button).toBeEnabled()
    fireEvent.click(button)
    expect(onLocate).toHaveBeenCalledOnce()

    rerender(
      <ProjectSelector
        projects={projects}
        selectedProjectId={projects[0].id}
        onProjectChange={vi.fn()}
        onLocate={onLocate}
        locateDisabledReason="Projeto sem feições para localizar"
      />,
    )
    expect(button).toBeDisabled()
    expect(button).toHaveAttribute('title', 'Projeto sem feições para localizar')
  })
})
