import { Button } from "@/components/ui/button"
import { APP_LINKS } from "@/lib/app-links"

export function FinalCta() {
  return (
    <section className="py-20 md:py-28">
      <div className="mx-auto max-w-3xl px-6 text-center">
        <h2 className="font-serif text-3xl font-bold tracking-tight text-foreground md:text-4xl text-balance">
          Start writing in minutes.
        </h2>
        <p className="mx-auto mt-4 max-w-lg text-muted-foreground">
          Create your first project, draft a scene, and keep your story
          consistent from page one.
        </p>
        <p className="mx-auto mt-2 max-w-lg text-sm text-muted-foreground">
          Create account or sign in with Google.
        </p>
        <div className="mt-8 flex flex-wrap justify-center gap-3">
          <Button size="lg" asChild>
            <a href={APP_LINKS.startFree}>Start free</a>
          </Button>
          <Button variant="outline" size="lg" asChild>
            <a href="#pricing">See pricing</a>
          </Button>
        </div>
      </div>
    </section>
  )
}
