import type { ProjectDefinition } from './api/projectCatalog'

interface ProjectSelectorProps {
  readonly onProjectChange: (projectId: string) => void
  readonly projects: readonly ProjectDefinition[]
  readonly selectedProjectId: string
}

export function ProjectSelector({
  onProjectChange,
  projects,
  selectedProjectId,
}: ProjectSelectorProps) {
  return (
    <label className="project-selector">
      <span className="project-selector__label">Projeto</span>
      <select
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
    </label>
  )
}
