import { BookOpen, RefreshCw, ShieldCheck } from "lucide-react"

const items = [
  {
    icon: BookOpen,
    text: "Designed for novels, series, and long manuscripts",
  },
  {
    icon: RefreshCw,
    text: "Built for revision-heavy workflows",
  },
  {
    icon: ShieldCheck,
    text: "AI is optional\u2014your voice stays in control",
  },
]

export function SocialProof() {
  return (
    <section className="border-y border-border bg-secondary/50">
      <div className="mx-auto flex max-w-6xl flex-col items-center justify-center gap-6 px-6 py-10 md:flex-row md:gap-12 md:py-8">
        {items.map((item) => (
          <div key={item.text} className="flex items-center gap-3">
            <item.icon className="size-4 shrink-0 text-accent" />
            <span className="text-sm text-muted-foreground">{item.text}</span>
          </div>
        ))}
      </div>
    </section>
  )
}
