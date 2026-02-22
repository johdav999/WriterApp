import { Badge } from "@/components/ui/badge"
import { Sparkles } from "lucide-react"

const actions = [
  "Tighten selection",
  "Expand scene",
  "Change tone",
  "Show, don\u2019t tell",
  "Propose next paragraph",
  "Continuity check",
]

export function AiSection() {
  return (
    <section className="py-20 md:py-28">
      <div className="mx-auto max-w-6xl px-6">
        <div className="grid items-center gap-12 lg:grid-cols-2">
          {/* Text */}
          <div className="flex flex-col gap-5">
            <div className="flex items-center gap-2">
              <Sparkles className="size-5 text-accent" />
              <Badge
                variant="outline"
                className="rounded-full border-accent/30 text-xs text-accent-foreground"
              >
                Standard & Professional
              </Badge>
            </div>
            <h2 className="font-serif text-3xl font-bold tracking-tight text-foreground md:text-4xl">
              AI coaching when you want it&mdash;never when you don&rsquo;t.
            </h2>
            <p className="max-w-lg leading-relaxed text-muted-foreground">
              Use AI to tighten prose, expand a scene, change tone, propose the
              next paragraph, generate outlines, and keep continuity bibles up
              to date. Every AI change is reviewable before you apply it.
            </p>
          </div>

          {/* Action pills */}
          <div className="flex flex-wrap gap-3">
            {actions.map((action) => (
              <div
                key={action}
                className="flex items-center gap-2 rounded-full border border-border bg-card px-4 py-2.5 text-sm text-foreground shadow-sm transition-colors hover:border-accent/40 hover:bg-accent/5"
              >
                <Sparkles className="size-3.5 text-accent" />
                {action}
              </div>
            ))}
          </div>
        </div>
      </div>
    </section>
  )
}
