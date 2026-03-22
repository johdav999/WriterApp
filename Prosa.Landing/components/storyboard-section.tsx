import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { APP_LINKS } from "@/lib/app-links"
import {
  ArrowRight,
  BrainCircuit,
  Clapperboard,
  GripVertical,
  PanelsTopLeft,
  Sparkles,
  Tags,
} from "lucide-react"

const storyboardBullets = [
  "Visual chapter-and-scene board with drag-and-drop restructuring",
  "Inline scene editor for summary, POV, role, intent, and notes",
  "Filters and color modes for POV, subplot, and status",
  "Bulk updates across multiple scenes",
  "AI suggestions for next scenes, missing beats, and structure issues",
]

const storyboardSignals = [
  "POV balance",
  "Missing scenes",
  "Subplot continuity",
]

export function StoryboardSection() {
  return (
    <section
      id="storyboard"
      className="scroll-mt-20 border-t border-border bg-card py-20 md:py-28"
    >
      <div className="mx-auto max-w-6xl px-6">
        <div className="grid items-center gap-12 lg:grid-cols-[.95fr_1.05fr] lg:gap-16">
          <div className="flex flex-col gap-6">
            <div className="flex flex-wrap items-center gap-3">
              <div className="flex items-center gap-2">
                <Clapperboard className="size-5 text-accent" />
                <Badge
                  variant="outline"
                  className="rounded-full border-accent/30 bg-accent/5 text-xs text-accent-foreground"
                >
                  Professional Planning
                </Badge>
              </div>
              <span className="text-xs font-medium uppercase tracking-[0.24em] text-muted-foreground">
                Storyboard
              </span>
            </div>

            <div className="space-y-4">
              <h2 className="font-serif text-3xl font-bold tracking-tight text-foreground md:text-4xl">
                Storyboard your manuscript like a working story system
              </h2>
              <p className="max-w-xl text-base leading-relaxed text-muted-foreground md:text-lg">
                Plan scenes visually, edit structure inline, and get AI guidance
                on pacing, POV balance, and missing story beats.
              </p>
              <p className="max-w-xl leading-relaxed text-muted-foreground">
                Prosa&apos;s storyboard turns your manuscript into a visual scene
                board. Organize chapters as columns, move scenes with
                drag-and-drop, and update story structure directly from the
                board. With built-in insights and AI suggestions, you can spot
                weak pacing, missing scenes, and structural imbalances before
                they become problems.
              </p>
            </div>

            <ul className="grid gap-3 text-sm text-foreground sm:grid-cols-2">
              {storyboardBullets.map((bullet) => (
                <li
                  key={bullet}
                  className="flex items-start gap-3 rounded-xl border border-border bg-secondary/35 px-4 py-3"
                >
                  <span className="mt-1 size-2 rounded-full bg-accent" />
                  <span className="leading-relaxed">{bullet}</span>
                </li>
              ))}
            </ul>

            <div className="rounded-2xl border border-accent/20 bg-accent/5 px-5 py-4">
              <p className="text-sm font-medium text-foreground">
                From outline to execution
              </p>
              <p className="mt-1 text-sm leading-relaxed text-muted-foreground">
                Plan, analyze, and refine your story in one place.
              </p>
            </div>

            <div className="flex flex-wrap gap-3 pt-1">
              <Button size="lg" asChild>
                <a href={APP_LINKS.startPro}>
                  Try the Storyboard
                  <ArrowRight className="size-4" />
                </a>
              </Button>
              <Button variant="outline" size="lg" asChild>
                <a href="#pricing">See plans</a>
              </Button>
            </div>
          </div>

          <div className="relative">
            <div className="absolute inset-x-8 top-6 h-40 rounded-full bg-accent/10 blur-3xl" />
            <div className="relative overflow-hidden rounded-[28px] border border-border bg-secondary/25 p-3 shadow-[0_28px_80px_-32px_rgba(0,0,0,0.35)]">
              <div className="rounded-[22px] border border-border bg-card">
                <div className="flex items-center justify-between border-b border-border px-4 py-3">
                  <div>
                    <p className="text-[11px] font-semibold uppercase tracking-[0.28em] text-muted-foreground">
                      Storyboard
                    </p>
                    <p className="mt-1 font-serif text-lg text-foreground">
                      The Glass Orchard
                    </p>
                  </div>
                  <Badge className="rounded-full bg-primary/10 text-foreground hover:bg-primary/10">
                    Pro
                  </Badge>
                </div>

                <div className="grid gap-3 p-3 md:grid-cols-[1.55fr_.45fr] lg:grid-cols-[1.7fr_.3fr]">
                  <div className="rounded-2xl border border-border bg-secondary/35 p-3">
                    <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
                      <div className="flex min-w-0 items-center gap-2">
                        <PanelsTopLeft className="size-4 text-accent" />
                        <span className="text-sm font-medium text-foreground">
                          Chapter board
                        </span>
                      </div>
                      <span className="text-xs text-muted-foreground">
                        Drag to restructure
                      </span>
                    </div>

                    <div className="grid gap-3 grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
                      {[
                        {
                          chapter: "Chapter 4",
                          summary: "2 draft scenes · POV: Mara",
                          cards: [
                            "Break into the greenhouse",
                            "Find ledger beneath the bench",
                          ],
                        },
                        {
                          chapter: "Chapter 5",
                          summary: "3 scenes · POV: Mixed",
                          cards: [
                            "Confrontation at the river",
                            "A missing witness surfaces",
                          ],
                        },
                      ].map((column) => (
                        <div
                          key={column.chapter}
                          className="min-w-0 rounded-xl border border-border bg-card p-3"
                        >
                          <div className="mb-3 min-w-0">
                            <p className="break-words font-serif text-base text-foreground">
                              {column.chapter}
                            </p>
                            <p className="break-words text-xs text-muted-foreground">
                              {column.summary}
                            </p>
                          </div>
                          <div className="space-y-2">
                            {column.cards.map((card, index) => (
                              <div
                                key={card}
                                className={`rounded-lg border px-3 py-2 ${
                                  index === 0
                                    ? "border-accent/30 bg-accent/10"
                                    : "border-border bg-secondary/40"
                                }`}
                              >
                                <div className="flex min-w-0 items-start gap-2 overflow-hidden">
                                  <GripVertical className="mt-0.5 size-3.5 shrink-0 text-muted-foreground" />
                                  <div className="min-w-0 flex-1 space-y-1">
                                    <p className="whitespace-normal text-sm leading-5 font-medium text-foreground [overflow-wrap:break-word]">
                                      {card}
                                    </p>
                                    <div className="flex min-w-0 flex-wrap gap-1.5 text-[10px] uppercase tracking-wide text-muted-foreground">
                                      <span className="rounded-full bg-card px-2 py-1">
                                        Draft
                                      </span>
                                      <span className="rounded-full bg-card px-2 py-1">
                                        POV
                                      </span>
                                      <span className="rounded-full bg-card px-2 py-1">
                                        Subplot
                                      </span>
                                    </div>
                                  </div>
                                </div>
                              </div>
                            ))}
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>

                  <div className="space-y-3">
                    <div className="rounded-2xl border border-border bg-card p-4">
                      <div className="flex items-center gap-2">
                        <Tags className="size-4 text-accent" />
                        <span className="text-sm font-medium text-foreground">
                          Scene inspector
                        </span>
                      </div>
                      <div className="mt-3 space-y-2">
                        {["Summary", "POV", "Narrative role", "Intent", "Notes"].map(
                          (field) => (
                            <div key={field}>
                              <p className="text-[11px] uppercase tracking-wide text-muted-foreground">
                                {field}
                              </p>
                              <div className="mt-1 h-8 rounded-md bg-secondary/55" />
                            </div>
                          ),
                        )}
                      </div>
                    </div>

                    <div className="rounded-2xl border border-accent/20 bg-accent/6 p-4">
                      <div className="flex items-center gap-2">
                        <BrainCircuit className="size-4 text-accent" />
                        <span className="text-sm font-medium text-foreground">
                          AI-powered story structure
                        </span>
                      </div>
                      <p className="mt-2 text-sm leading-relaxed text-muted-foreground">
                        Get insights on pacing, subplot continuity, and POV
                        balance directly from your storyboard.
                      </p>
                      <div className="mt-3 flex flex-wrap gap-2">
                        {storyboardSignals.map((signal) => (
                          <div
                            key={signal}
                            className="inline-flex items-center gap-1.5 rounded-full border border-border bg-card px-3 py-1.5 text-xs text-foreground"
                          >
                            <Sparkles className="size-3 text-accent" />
                            {signal}
                          </div>
                        ))}
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  )
}
