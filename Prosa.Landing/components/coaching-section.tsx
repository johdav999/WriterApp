import { Badge } from "@/components/ui/badge"
import {
  BookOpenText,
  Clapperboard,
  Compass,
  ShieldCheck,
  Sparkles,
  WandSparkles,
} from "lucide-react"

interface CoachingCard {
  icon: React.ComponentType<{ className?: string }>
  title: string
  description: string
  detail: string
  availability: string
}

const coachingCards: CoachingCard[] = [
  {
    icon: BookOpenText,
    title: "Synopsis coaching",
    description:
      "Evaluate your synopsis, surface guiding questions, and request alternatives one field at a time.",
    detail:
      "Useful when the premise is close but the shape of the story still needs pressure-testing.",
    availability: "Standard core, expanded in Professional",
  },
  {
    icon: Clapperboard,
    title: "Scene coaching",
    description:
      "Suggest scene improvements, refine a passage, and find open questions without leaving the editor.",
    detail:
      "Feedback stays anchored to the scene you are drafting instead of a generic AI chat.",
    availability: "Professional",
  },
  {
    icon: Compass,
    title: "Story coach",
    description:
      "Get broader story feedback built from your current story context, not a blank prompt window.",
    detail:
      "A strong fit when you need help with direction, structure, or what a scene should accomplish next.",
    availability: "Professional",
  },
  {
    icon: ShieldCheck,
    title: "Continuity coach",
    description:
      "Run continuity checks, refresh canon context, and apply supported fixes when conflicts appear.",
    detail:
      "Designed to protect timelines, character facts, and recurring story-world details.",
    availability: "Professional",
  },
  {
    icon: Sparkles,
    title: "Style & quality",
    description:
      "Run focused quality checks to catch readability and style friction before you share or export.",
    detail:
      "Ideal for revision passes where you want clear signals without losing your voice.",
    availability: "Standard",
  },
  {
    icon: WandSparkles,
    title: "Smart recommendations",
    description:
      "Coach cards suggest the next helpful move, from creating the first scene to reviewing progress or opening the outline.",
    detail:
      "Project-level and scene-level prompts keep momentum when you are unsure what to tackle next.",
    availability: "Workspace guidance",
  },
]

const coachingAreas = [
  "Synopsis",
  "Scenes",
  "Story structure",
  "Continuity",
  "Style",
  "Quality",
]

export function CoachingSection() {
  return (
    <section className="border-t border-border bg-secondary/30 py-20 md:py-28">
      <div className="mx-auto max-w-6xl px-6">
        <div className="mx-auto mb-14 max-w-3xl text-center">
          <p className="mb-2 text-sm font-medium uppercase tracking-wider text-accent">
            Coaching
          </p>
          <h2 className="font-serif text-3xl font-bold tracking-tight text-foreground md:text-4xl">
            Structured guidance for synopsis, scenes, story logic, and revision
          </h2>
          <p className="mt-4 text-base leading-relaxed text-muted-foreground md:text-lg">
            Prosa is built to coach the manuscript you already have. Get
            focused feedback inside the synopsis, scene editor, and continuity
            workflow so you can think through the work without handing over the
            writing.
          </p>
        </div>

        <div className="mb-10 flex flex-wrap justify-center gap-2">
          {coachingAreas.map((area) => (
            <Badge
              key={area}
              variant="outline"
              className="rounded-full border-border bg-card px-3 py-1 text-xs font-medium text-secondary-foreground"
            >
              {area}
            </Badge>
          ))}
        </div>

        <div className="grid gap-6 md:grid-cols-2 xl:grid-cols-3">
          {coachingCards.map((card) => (
            <div
              key={card.title}
              className="flex h-full flex-col gap-4 rounded-xl border border-border bg-card p-6 transition-shadow hover:shadow-md"
            >
              <div className="flex items-start justify-between gap-4">
                <div className="flex size-10 items-center justify-center rounded-lg bg-accent/15">
                  <card.icon className="size-5 text-accent" />
                </div>
                <span className="rounded-full bg-secondary px-3 py-1 text-[10px] font-medium uppercase tracking-wider text-muted-foreground">
                  {card.availability}
                </span>
              </div>
              <div className="space-y-2">
                <h3 className="font-serif text-xl font-semibold text-foreground">
                  {card.title}
                </h3>
                <p className="text-sm leading-relaxed text-muted-foreground">
                  {card.description}
                </p>
              </div>
              <p className="mt-auto border-t border-border pt-4 text-sm leading-relaxed text-muted-foreground">
                {card.detail}
              </p>
            </div>
          ))}
        </div>
      </div>
    </section>
  )
}
