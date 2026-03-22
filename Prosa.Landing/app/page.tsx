import { Navbar } from "@/components/navbar"
import { Hero } from "@/components/hero"
import { SocialProof } from "@/components/social-proof"
import { CoachingSection } from "@/components/coaching-section"
import { HowItWorks } from "@/components/how-it-works"
import { Features } from "@/components/features"
import { StoryboardSection } from "@/components/storyboard-section"
import { AiSection } from "@/components/ai-section"
import { Pricing } from "@/components/pricing"
import { UseCases } from "@/components/use-cases"
import { Faq } from "@/components/faq"
import { FinalCta } from "@/components/final-cta"
import { Footer } from "@/components/footer"

export default function Home() {
  return (
    <div className="min-h-screen">
      <Navbar />
      <main>
        <Hero />
        <SocialProof />
        <CoachingSection />
        <HowItWorks />
        <Features />
        <StoryboardSection />
        <AiSection />
        <Pricing />
        <UseCases />
        <Faq />
        <FinalCta />
      </main>
      <Footer />
    </div>
  )
}
