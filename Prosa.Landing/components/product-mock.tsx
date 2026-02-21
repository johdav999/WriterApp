import {
  FileText,
  Sparkles,
  GitCompare,
  Download,
  Layers,
  Pen,
} from "lucide-react"

export function ProductMock() {
  return (
    <div className="rounded-xl border border-border bg-card p-1 shadow-lg">
      {/* Title bar */}
      <div className="flex items-center gap-2 border-b border-border px-4 py-2.5">
        <div className="flex gap-1.5">
          <span className="size-2.5 rounded-full bg-border" />
          <span className="size-2.5 rounded-full bg-border" />
          <span className="size-2.5 rounded-full bg-border" />
        </div>
        <span className="ml-2 text-xs text-muted-foreground">
          My Novel &mdash; Chapter 3
        </span>
      </div>

      <div className="flex">
        {/* Sidebar */}
        <div className="hidden w-44 border-r border-border p-3 sm:block">
          <p className="mb-2 text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">
            Structure
          </p>
          <ul className="flex flex-col gap-1.5" role="list">
            {["Part I", "Ch 1: Arrival", "Ch 2: The Letter", "Ch 3: Dusk"].map(
              (item, i) => (
                <li
                  key={item}
                  className={`flex items-center gap-1.5 rounded px-2 py-1 text-xs ${
                    i === 3
                      ? "bg-accent/30 font-medium text-foreground"
                      : "text-muted-foreground"
                  }`}
                >
                  <Layers className="size-3 shrink-0" />
                  {item}
                </li>
              )
            )}
          </ul>
        </div>

        {/* Editor area */}
        <div className="flex-1 p-4">
          {/* Scene header */}
          <div className="mb-3 flex items-center gap-2">
            <FileText className="size-3.5 text-accent" />
            <span className="text-xs font-medium text-foreground">
              Scene 2: The garden
            </span>
          </div>

          {/* Fake text lines */}
          <div className="flex flex-col gap-2">
            <div className="flex items-center gap-1.5">
              <Pen className="size-3 text-muted-foreground/50" />
              <div className="h-2 w-full rounded bg-muted" />
            </div>
            <div className="h-2 w-5/6 rounded bg-muted" />
            <div className="h-2 w-4/6 rounded bg-muted" />
            <div className="h-2 w-full rounded bg-muted" />
            <div className="h-2 w-3/4 rounded bg-muted" />
          </div>

          {/* Feature hints */}
          <div className="mt-5 grid grid-cols-2 gap-2">
            <FeatureHint icon={Sparkles} label="AI suggestions" tier="Std" />
            <FeatureHint icon={GitCompare} label="Diff / restore" tier="Std" />
            <FeatureHint icon={Download} label="DOCX / EPUB" tier="Pro" />
            <FeatureHint icon={Layers} label="Scene notes" tier="Free" />
          </div>
        </div>
      </div>
    </div>
  )
}

function FeatureHint({
  icon: Icon,
  label,
  tier,
}: {
  icon: React.ComponentType<{ className?: string }>
  label: string
  tier: string
}) {
  return (
    <div className="flex items-center gap-2 rounded-md border border-border bg-secondary/50 px-2.5 py-1.5">
      <Icon className="size-3.5 text-accent" />
      <span className="text-[11px] text-foreground">{label}</span>
      <span className="ml-auto text-[9px] font-medium uppercase tracking-wide text-muted-foreground">
        {tier}
      </span>
    </div>
  )
}
