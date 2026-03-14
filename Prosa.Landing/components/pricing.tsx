import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import { APP_LINKS } from "@/lib/app-links"
import { Check } from "lucide-react"

interface PricingTier {
  name: string
  price: string
  period: string
  description: string
  features: string[]
  cta: string
  ctaHref: string
  highlighted: boolean
  badge?: string
}

const tiers: PricingTier[] = [
  {
    name: "Free",
    price: "0",
    period: "SEK",
    description: "Start writing with core tools",
    features: [
      "Full rich text formatting",
      "Core document writing",
      "Basic sections/pages",
      "Local-first feel (light server usage)",
      "No AI tools",
    ],
    cta: "Start free",
    ctaHref: APP_LINKS.startFree,
    highlighted: false,
  },
  {
    name: "Standard",
    price: "9",
    period: "USD / month",
    description: "Add AI tools and deeper organization",
    features: [
      "Everything in Free",
      "AI writing tools (rewrite, tighten, expand, tone)",
      "Synopsis evaluation + quality checks",
      "Outline generation from synopsis",
      "Search across project content",
      "Versioning + diff",
      "Import from .docx/.rtf/.txt",
    ],
    cta: "Start Standard",
    ctaHref: APP_LINKS.startStandard,
    highlighted: false,
  },
  {
    name: "Professional",
    price: "18",
    period: "USD / month",
    description: "Full power for serious authors",
    features: [
      "Everything in Standard",
      "Higher AI token limits",
      "Full coaching suite for scenes and story",
      "Continuity bibles + apply fixes",
      "Cover image generation",
      "DOCX + EPUB export",
      "Export templates & presets",
      "Goals & writing sessions",
    ],
    cta: "Go Professional",
    ctaHref: APP_LINKS.startPro,
    highlighted: true,
    badge: "Best for power users",
  },
]

export function Pricing() {
  return (
    <section
      id="pricing"
      className="scroll-mt-20 border-t border-border bg-secondary/30 py-20 md:py-28"
    >
      <div className="mx-auto max-w-6xl px-6">
        <div className="mb-14 text-center">
          <p className="mb-2 text-sm font-medium uppercase tracking-wider text-accent">
            Pricing
          </p>
          <h2 className="font-serif text-3xl font-bold tracking-tight text-foreground md:text-4xl">
            Simple, transparent plans
          </h2>
          <p className="mt-3 text-muted-foreground">
            Billed monthly. No hidden fees.
          </p>
        </div>

        <div className="grid gap-6 md:grid-cols-3">
          {tiers.map((tier) => (
            <div
              key={tier.name}
              className={`relative flex flex-col rounded-xl border p-6 transition-shadow ${
                tier.highlighted
                  ? "border-accent bg-card shadow-lg ring-1 ring-accent/20"
                  : "border-border bg-card hover:shadow-md"
              }`}
            >
              {tier.badge && (
                <Badge className="absolute -top-3 left-1/2 -translate-x-1/2 rounded-full bg-accent px-3 py-1 text-xs font-medium text-accent-foreground">
                  {tier.badge}
                </Badge>
              )}
              <div className="mb-6">
                <h3 className="font-serif text-lg font-semibold text-foreground">
                  {tier.name}
                </h3>
                <p className="mt-1 text-sm text-muted-foreground">
                  {tier.description}
                </p>
              </div>

              <div className="mb-6 flex items-baseline gap-1">
                <span className="font-serif text-4xl font-bold text-foreground">
                  {tier.price}
                </span>
                <span className="text-sm text-muted-foreground">
                  {tier.period}
                </span>
              </div>

              <ul className="mb-8 flex flex-1 flex-col gap-3" role="list">
                {tier.features.map((feature) => (
                  <li key={feature} className="flex items-start gap-2.5">
                    <Check className="mt-0.5 size-4 shrink-0 text-accent" />
                    <span className="text-sm text-muted-foreground">
                      {feature}
                    </span>
                  </li>
                ))}
              </ul>

              <Button
                variant={tier.highlighted ? "default" : "outline"}
                className="w-full"
                asChild
              >
                <a href={tier.ctaHref}>{tier.cta}</a>
              </Button>
            </div>
          ))}
        </div>

        <p className="mt-8 text-center text-xs text-muted-foreground">
          AI usage limits apply. Professional includes higher monthly AI
          capacity.
        </p>

        <div className="mt-8 grid gap-4 md:grid-cols-2">
          <div className="rounded-xl border border-border bg-card p-5">
            <div className="mb-2 text-sm font-medium uppercase tracking-wider text-accent">
              Standard coaching
            </div>
            <p className="text-sm leading-relaxed text-muted-foreground">
              Start with core coaching for active draft work: synopsis
              evaluation and scene quality checks.
            </p>
          </div>
          <div className="rounded-xl border border-border bg-card p-5">
            <div className="mb-2 text-sm font-medium uppercase tracking-wider text-accent">
              Professional coaching
            </div>
            <p className="text-sm leading-relaxed text-muted-foreground">
              Unlock the full suite with guiding questions, synopsis
              alternatives, scene coaching, story coach, continuity checks, and
              canon refresh.
            </p>
          </div>
        </div>
      </div>
    </section>
  )
}
