(function () {
  const KEY = "onboarding-spotlight-target";

  function clear() {
    const previous = document.querySelector("." + KEY);
    if (previous) {
      previous.classList.remove(KEY);
    }
  }

  function apply(selector) {
    clear();
    if (!selector || typeof selector !== "string") {
      return;
    }

    const target = document.querySelector(selector);
    if (!target) {
      return;
    }

    target.classList.add(KEY);
    if (typeof target.scrollIntoView === "function") {
      target.scrollIntoView({ behavior: "smooth", block: "center", inline: "nearest" });
    }
  }

  window.writerAppOnboardingHighlight = {
    apply,
    clear
  };
})();
