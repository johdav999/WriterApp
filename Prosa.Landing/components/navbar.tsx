"use client"

import { useState } from "react"
import { Button } from "@/components/ui/button"
import { APP_LINKS } from "@/lib/app-links"
import { Menu, X } from "lucide-react"

const navLinks = [
  { label: "Features", href: "#features" },
  { label: "How it works", href: "#how-it-works" },
  { label: "Pricing", href: "#pricing" },
  { label: "FAQ", href: "#faq" },
]

const supportHref = "mailto:hello@prosa-app.com"

export function Navbar() {
  const [mobileOpen, setMobileOpen] = useState(false)

  return (
    <header className="sticky top-0 z-50 border-b border-border/60 bg-background/90 backdrop-blur-md">
      <div className="mx-auto max-w-6xl px-6">
        <div className="grid min-h-[92px] grid-cols-[1fr_auto] items-center gap-4 py-3 md:min-h-0 md:grid-cols-[auto_minmax(0,1fr)_auto] md:grid-rows-[minmax(92px,auto)_44px] md:py-0">
          <a
            href="/"
            className="inline-flex items-center rounded-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 md:row-span-2 md:pl-7"
            aria-label="Prosa home"
          >
            <img
              src="/logo.png"
              alt="Prosa"
              className="block h-10 w-auto max-w-[192px] object-contain md:h-[50px] md:max-w-[240px]"
            />
          </a>

          <div className="hidden items-center justify-end gap-3 md:col-start-3 md:row-span-2 md:row-start-1 md:flex md:self-center">
            <Button variant="outline" size="sm" asChild>
              <a href={supportHref}>Support</a>
            </Button>
            <Button variant="ghost" size="sm" asChild>
              <a href={APP_LINKS.login}>Sign in</a>
            </Button>
            <Button size="sm" asChild>
              <a href={APP_LINKS.startFree}>Start free</a>
            </Button>
          </div>

          <button
            className="justify-self-end text-foreground md:hidden"
            onClick={() => setMobileOpen(!mobileOpen)}
            aria-label={mobileOpen ? "Close menu" : "Open menu"}
          >
            {mobileOpen ? <X className="size-5" /> : <Menu className="size-5" />}
          </button>

          <nav
            className="hidden min-w-0 items-center md:col-start-2 md:row-span-2 md:row-start-1 md:flex md:self-center"
            aria-label="Primary"
          >
            <ul className="flex items-center gap-2 md:pl-4" role="list">
              {navLinks.map((link) => (
                <li key={link.href}>
                  <a
                    href={link.href}
                    className="inline-flex min-h-8 items-center rounded-md px-3 text-sm font-medium text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground"
                  >
                    {link.label}
                  </a>
                </li>
              ))}
            </ul>
          </nav>
        </div>
      </div>

      {/* Mobile menu */}
      {mobileOpen && (
        <div className="border-t border-border bg-background px-6 pb-6 md:hidden">
          <ul className="flex flex-col gap-4 pt-4" role="list">
            {navLinks.map((link) => (
              <li key={link.href}>
                <a
                  href={link.href}
                  className="text-sm text-muted-foreground transition-colors hover:text-foreground"
                  onClick={() => setMobileOpen(false)}
                >
                  {link.label}
                </a>
              </li>
            ))}
          </ul>
          <div className="mt-4 flex flex-col gap-2">
            <Button variant="outline" size="sm" asChild>
              <a href={supportHref}>Support</a>
            </Button>
            <Button variant="outline" size="sm" asChild>
              <a href={APP_LINKS.login}>Sign in</a>
            </Button>
            <Button size="sm" asChild>
              <a href={APP_LINKS.startFree}>Start free</a>
            </Button>
          </div>
        </div>
      )}
    </header>
  )
}
