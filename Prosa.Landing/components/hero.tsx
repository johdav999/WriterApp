import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import { ProductMock } from "@/components/product-mock"
import { APP_LINKS } from "@/lib/app-links"

const benefitChips = [
  "Synopsis coaching",
  "Scene guidance",
  "Continuity-aware",
]

export function Hero() {
  return (
    <section className="relative overflow-hidden">
      <div className="mx-auto max-w-6xl px-6 pb-16 pt-16 md:pb-24 md:pt-24">
        <div className="grid items-center gap-12 lg:grid-cols-2 lg:gap-16">
          {/* Text */}
          <div className="flex flex-col gap-6">
            <div className="flex flex-wrap gap-2">
              {benefitChips.map((chip) => (
                <Badge
                  key={chip}
                  variant="outline"
                  className="rounded-full border-border bg-secondary px-3 py-1 text-xs font-medium text-secondary-foreground"
                >
                  {chip}
                </Badge>
              ))}
            </div>
            <h1 className="font-serif text-4xl font-bold leading-tight tracking-tight text-foreground text-balance md:text-5xl lg:text-6xl">
              Write faster. Stay consistent. Finish your book with structured coaching.
            </h1>
            <p className="max-w-lg text-base leading-relaxed text-muted-foreground md:text-lg">
              Prosa is a modern writing workspace for long-form projects with
              built-in coaching for synopsis, scenes, story development,
              continuity, and revision quality. Draft in flow, organize your
              structure, and get clear feedback where the manuscript actually
              needs it.
            </p>
            <div className="flex flex-wrap gap-3 pt-2">
              <Button size="lg" asChild>
                <a href={APP_LINKS.startFree}>Start free</a>
              </Button>
              <Button variant="outline" size="lg" asChild>
                <a href="#pricing">Explore pricing</a>
              </Button>
            </div>
            <div className="grid gap-2 pt-1 text-sm text-muted-foreground sm:grid-cols-2">
              <div>Evaluate your synopsis before you draft deeper.</div>
              <div>Refine scenes and catch continuity issues in context.</div>
            </div>
          </div>

          {/* Product mock */}
          <div className="relative">
            <ProductMock />
          </div>
        </div>
      </div>
    </section>
  )
}
