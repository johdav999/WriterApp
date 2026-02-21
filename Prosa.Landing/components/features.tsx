import { Badge } from "@/components/ui/badge"
import {
  Type,
  Clapperboard,
  Upload,
  FolderKanban,
  Search,
  LayoutTemplate,
  GitCompare,
  Download,
} from "lucide-react"

type Tier = "Free" | "Standard" | "Pro"

interface Feature {
  icon: React.ComponentType<{ className?: string }>
  title: string
  description: string
  tier: Tier
  category: string
}

const features: Feature[] = [
  {
    icon: Type,
    title: "Rich text editor",
    description:
      "A full-featured editor for long-form writing: headings, lists, tables, links, images.",
    tier: "Free",
    category: "Writing",
  },
  {
    icon: Clapperboard,
    title: "Scene-based workflow",
    description:
      "Write and manage content by scene. Keep scene notes and metadata alongside the draft.",
    tier: "Free",
    category: "Writing",
  },
  {
    icon: Upload,
    title: "Import",
    description:
      "Bring in .txt, .docx, or .rtf and append or replace sections cleanly.",
    tier: "Standard",
    category: "Writing",
  },
  {
    icon: FolderKanban,
    title: "Projects + structure",
    description:
      "Organize by Part / Chapter / Scene with fast reordering and navigation.",
    tier: "Standard",
    category: "Organize",
  },
  {
    icon: Search,
    title: "Search everywhere",
    description:
      "Search across draft text, notes, scene cards, and outlines in one place.",
    tier: "Standard",
    category: "Organize",
  },
  {
    icon: LayoutTemplate,
    title: "Outlines + templates",
    description:
      "Create and apply outlines and templates to jump-start a new project.",
    tier: "Standard",
    category: "Organize",
  },
  {
    icon: GitCompare,
    title: "Versioning + diff",
    description:
      "Compare snapshots, see what changed, and restore earlier versions when needed.",
    tier: "Standard",
    category: "Revise & Publish",
  },
  {
    icon: Download,
    title: "Export DOCX / EPUB",
    description:
      "Publish-ready exports with presets and templates for clean formatting.",
    tier: "Pro",
    category: "Revise & Publish",
  },
]

const tierColors: Record<Tier, string> = {
  Free: "bg-secondary text-secondary-foreground",
  Standard: "bg-accent/15 text-accent-foreground",
  Pro: "bg-primary/10 text-foreground",
}

export function Features() {
  return (
    <section id="features" className="scroll-mt-20 border-t border-border bg-secondary/30 py-20 md:py-28">
      <div className="mx-auto max-w-6xl px-6">
        <div className="mb-14 text-center">
          <p className="mb-2 text-sm font-medium uppercase tracking-wider text-accent">
            Features
          </p>
          <h2 className="font-serif text-3xl font-bold tracking-tight text-foreground md:text-4xl">
            Everything you need to write, organize, and publish
          </h2>
        </div>

        <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
          {features.map((f) => (
            <div
              key={f.title}
              className="flex flex-col gap-3 rounded-lg border border-border bg-card p-5 transition-shadow hover:shadow-md"
            >
              <div className="flex items-center justify-between">
                <div className="flex size-9 items-center justify-center rounded-md bg-accent/10">
                  <f.icon className="size-4 text-accent" />
                </div>
                <Badge
                  variant="secondary"
                  className={`rounded-full text-[10px] font-medium uppercase tracking-wider ${tierColors[f.tier]}`}
                >
                  {f.tier}
                </Badge>
              </div>
              <h3 className="font-serif text-base font-semibold text-foreground">
                {f.title}
              </h3>
              <p className="text-sm leading-relaxed text-muted-foreground">
                {f.description}
              </p>
            </div>
          ))}
        </div>
      </div>
    </section>
  )
}
