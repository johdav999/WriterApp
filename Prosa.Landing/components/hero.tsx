import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import { ProductMock } from "@/components/product-mock"

const benefitChips = [
  "Scene-based writing",
  "Continuity-aware",
  "Publish-ready exports",
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
              Write faster. Stay consistent. Finish your book.
            </h1>
            <p className="max-w-lg text-base leading-relaxed text-muted-foreground md:text-lg">
              Prosa is a modern writing workspace built for long-form
              projects&mdash;scenes, versions, exports, and (when you want it)
              AI coaching. Draft in flow, organize your story structure, and
              revise with confidence.
            </p>
            <div className="flex flex-wrap gap-3 pt-2">
              <Button size="lg" asChild>
                <a href="#start-free">Start free</a>
              </Button>
              <Button variant="outline" size="lg" asChild>
                <a href="#pricing">Explore pricing</a>
              </Button>
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
