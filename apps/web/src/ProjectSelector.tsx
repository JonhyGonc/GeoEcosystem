import type { ProjectDefinition } from './api/projectCatalog'

interface ProjectSelectorProps {
  readonly locateDisabledReason?: string
  readonly onLocate: () => void
  readonly onProjectChange: (projectId: string) => void
  readonly projects: readonly ProjectDefinition[]
  readonly selectedProjectId: string
}

export function ProjectSelector({
  locateDisabledReason,
  onLocate,
  onProjectChange,
  projects,
  selectedProjectId,
}: ProjectSelectorProps) {
  return (
    <div className="project-selector">
      <label className="project-selector__label" htmlFor="project-selector">
        Projeto
      </label>
      <div className="project-selector__row">
        <select
          id="project-selector"
          value={selectedProjectId}
          onChange={(event) => onProjectChange(event.target.value)}
          disabled={projects.length < 2}
        >
          {projects.map((project) => (
            <option key={project.id} value={project.id}>
              {project.name}
            </option>
          ))}
        </select>
        <button
          type="button"
          className="project-selector__locate"
          onClick={onLocate}
          disabled={Boolean(locateDisabledReason)}
          title={locateDisabledReason ?? 'Localizar projeto no mapa'}
          aria-label="Localizar projeto no mapa"
        >
          <svg aria-hidden="true" viewBox="0 0 24 24" width="18" height="18">
            <circle cx="12" cy="12" r="6.5" fill="none" stroke="currentColor" strokeWidth="2" />
            <circle cx="12" cy="12" r="2.2" fill="currentColor" />
            <path d="M12 1.5v4M12 18.5v4M1.5 12h4M18.5 12h4" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
          </svg>
        </button>
      </div>
    </div>
  )
}
