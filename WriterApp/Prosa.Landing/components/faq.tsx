"use client"

import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from "@/components/ui/accordion"

const faqs = [
  {
    q: "Is Prosa an AI writer?",
    a: "No. Prosa is a writing workspace with optional AI tools. The AI assists\u2014it rewrites, tightens, expands, and suggests\u2014but every change is presented for your review. You stay in control of every word.",
  },
  {
    q: "Can I use Prosa without AI?",
    a: "Absolutely. The Free plan includes no AI features at all and gives you a full rich text editor with basic organization. AI tools are available on the Standard and Professional plans, but you never have to use them.",
  },
  {
    q: "What\u2019s the difference between Standard and Professional?",
    a: "Standard adds AI writing tools, project organization, versioning, diff, and import capabilities. Professional includes everything in Standard plus higher AI token limits, continuity bibles, cover image generation, DOCX and EPUB export with templates, and writing session goals.",
  },
  {
    q: "Do you support DOCX and EPUB export?",
    a: "Yes. Publish-ready DOCX and EPUB export with formatting presets and templates is available on the Professional plan.",
  },
  {
    q: "Can I organize by scenes and chapters?",
    a: "Yes. You can structure your project by Part, Chapter, and Scene. Reorder and navigate your manuscript easily from the sidebar.",
  },
  {
    q: "Can I undo AI changes?",
    a: "Yes. Every AI suggestion is shown as a reviewable change. You can accept, reject, or modify it before applying. With versioning and diff, you can also roll back to any previous snapshot of your text.",
  },
]

export function Faq() {
  return (
    <section
      id="faq"
      className="scroll-mt-20 border-t border-border bg-secondary/30 py-20 md:py-28"
    >
      <div className="mx-auto max-w-3xl px-6">
        <div className="mb-14 text-center">
          <p className="mb-2 text-sm font-medium uppercase tracking-wider text-accent">
            FAQ
          </p>
          <h2 className="font-serif text-3xl font-bold tracking-tight text-foreground md:text-4xl">
            Frequently asked questions
          </h2>
        </div>

        <Accordion type="single" collapsible className="w-full">
          {faqs.map((faq, i) => (
            <AccordionItem key={i} value={`faq-${i}`}>
              <AccordionTrigger className="text-left font-serif text-base font-medium text-foreground">
                {faq.q}
              </AccordionTrigger>
              <AccordionContent className="leading-relaxed text-muted-foreground">
                {faq.a}
              </AccordionContent>
            </AccordionItem>
          ))}
        </Accordion>
      </div>
    </section>
  )
}
