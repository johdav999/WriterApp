import { PenLine, FolderTree, CheckCircle2 } from "lucide-react"

const steps = [
  {
    icon: PenLine,
    title: "Draft in scenes",
    text: "Write in manageable chunks. Keep notes, versions, and diffs close so you never lose a good idea.",
    step: "01",
  },
  {
    icon: FolderTree,
    title: "Organize your story",
    text: "Build a project structure with parts, chapters, and scenes. Outline first\u2014or discover the outline as you draft.",
    step: "02",
  },
  {
    icon: CheckCircle2,
    title: "Revise with confidence",
    text: "Compare changes, restore versions, and run focused improvements. Export when you\u2019re ready.",
    step: "03",
  },
]

export function HowItWorks() {
  return (
    <section id="how-it-works" className="scroll-mt-20 py-20 md:py-28">
      <div className="mx-auto max-w-6xl px-6">
        <div className="mb-14 text-center">
          <p className="mb-2 text-sm font-medium uppercase tracking-wider text-accent">
            How it works
          </p>
          <h2 className="font-serif text-3xl font-bold tracking-tight text-foreground md:text-4xl">
            From first draft to finished manuscript
          </h2>
        </div>

        <div className="grid gap-10 md:grid-cols-3 md:gap-8">
          {steps.map((s) => (
            <div key={s.step} className="flex flex-col gap-4">
              <div className="flex items-center gap-3">
                <div className="flex size-10 items-center justify-center rounded-lg bg-accent/15">
                  <s.icon className="size-5 text-accent" />
                </div>
                <span className="font-serif text-xs font-semibold tracking-wider text-muted-foreground">
                  Step {s.step}
                </span>
              </div>
              <h3 className="font-serif text-xl font-semibold text-foreground">
                {s.title}
              </h3>
              <p className="leading-relaxed text-muted-foreground">{s.text}</p>
            </div>
          ))}
        </div>
      </div>
    </section>
  )
}
