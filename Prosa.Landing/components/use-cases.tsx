import { BookOpen, PenLine, Library } from "lucide-react"

interface UseCase {
  icon: React.ComponentType<{ className?: string }>
  title: string
  description: string
  outcomes: string[]
}

const useCases: UseCase[] = [
  {
    icon: BookOpen,
    title: "Writing a novel",
    description:
      "Draft scene by scene, keep your structure visible, and build momentum toward a finished manuscript. Prosa lets you focus on the writing while the workspace handles the bookkeeping.",
    outcomes: [
      "Maintain story flow across chapters",
      "Track scene-level notes and ideas",
      "Export a clean manuscript when done",
    ],
  },
  {
    icon: PenLine,
    title: "Revising a manuscript",
    description:
      "Use versioning and diff tools to compare drafts, track what changed, and restore earlier versions without fear. AI tools help tighten prose and catch inconsistencies.",
    outcomes: [
      "Compare any two versions side by side",
      "Targeted AI rewrites on selections",
      "Restore earlier versions in one click",
    ],
  },
  {
    icon: Library,
    title: "Series bible & continuity",
    description:
      "Keep a living reference of characters, locations, timelines, and rules. AI-powered continuity checks flag potential conflicts before they reach your readers.",
    outcomes: [
      "Central reference for recurring details",
      "AI continuity checks across scenes",
      "Never lose track of your story world",
    ],
  },
]

export function UseCases() {
  return (
    <section className="py-20 md:py-28">
      <div className="mx-auto max-w-6xl px-6">
        <div className="mb-14 text-center">
          <p className="mb-2 text-sm font-medium uppercase tracking-wider text-accent">
            Use cases
          </p>
          <h2 className="font-serif text-3xl font-bold tracking-tight text-foreground md:text-4xl">
            Built for every stage of your writing
          </h2>
        </div>

        <div className="grid gap-8 md:grid-cols-3">
          {useCases.map((uc) => (
            <div
              key={uc.title}
              className="flex flex-col gap-4 rounded-lg border border-border bg-card p-6"
            >
              <div className="flex size-10 items-center justify-center rounded-lg bg-accent/15">
                <uc.icon className="size-5 text-accent" />
              </div>
              <h3 className="font-serif text-xl font-semibold text-foreground">
                {uc.title}
              </h3>
              <p className="text-sm leading-relaxed text-muted-foreground">
                {uc.description}
              </p>
              <ul className="mt-auto flex flex-col gap-2 border-t border-border pt-4" role="list">
                {uc.outcomes.map((outcome) => (
                  <li
                    key={outcome}
                    className="flex items-start gap-2 text-sm text-muted-foreground"
                  >
                    <span className="mt-1.5 size-1.5 shrink-0 rounded-full bg-accent" />
                    {outcome}
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      </div>
    </section>
  )
}
