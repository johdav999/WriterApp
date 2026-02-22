const DEFAULT_PROD_APP_URL =
  "https://prosa-fgg2cxhbdja2hwee.swedencentral-01.azurewebsites.net"
const DEFAULT_DEV_APP_URL = "http://localhost:5387"
const DEFAULT_LOCAL_APP_PORT = "5387"
const APP_URL_LOG_KEY = "__prosaLandingAppUrlLogged"

function normalizeAppUrl(value: string): string {
  return value.replace(/\/+$/, "")
}

function normalizeHostForLocalCheck(hostname: string): string {
  return hostname.toLowerCase().replace(/\.$/, "").replace(/^\[(.*)\]$/, "$1")
}

function isLocalHostname(hostname: string): boolean {
  const normalized = normalizeHostForLocalCheck(hostname)
  return (
    normalized === "localhost"
    || normalized === "127.0.0.1"
    || normalized === "::1"
  )
}

const configuredAppUrl = process.env.NEXT_PUBLIC_APP_URL?.trim()
const defaultAppUrl =
  process.env.NODE_ENV === "development"
    ? DEFAULT_DEV_APP_URL
    : DEFAULT_PROD_APP_URL

type AppUrlResolution = {
  appUrl: string
  diagnostics: Record<string, string | boolean>
}

function resolveAppUrl(): AppUrlResolution {
  const rawValue =
    configuredAppUrl && configuredAppUrl.length > 0
      ? configuredAppUrl
      : defaultAppUrl

  const withScheme = /^[a-zA-Z][a-zA-Z\d+\-.]*:\/\//.test(rawValue)
    ? rawValue
    : `http://${rawValue}`

  const normalizedDefault = normalizeAppUrl(defaultAppUrl)
  const diagnostics: Record<string, string | boolean> = {
    nodeEnv: process.env.NODE_ENV ?? "",
    rawEnvValue: process.env.NEXT_PUBLIC_APP_URL ?? "",
    configuredAppUrl: configuredAppUrl ?? "",
    defaultAppUrl,
    rawValue,
    withScheme,
  }

  try {
    const parsed = new URL(withScheme)
    if (!parsed.hostname || parsed.hostname.trim().length === 0) {
      diagnostics.fallbackReason = "missing_hostname"
      diagnostics.finalAppUrl = normalizedDefault
      return { appUrl: normalizedDefault, diagnostics }
    }

    const normalizedHost = normalizeHostForLocalCheck(parsed.hostname)
    const isLocal = isLocalHostname(parsed.hostname)
    const portBefore = parsed.port
    let injectedPort = false
    if (isLocal && parsed.port.length === 0) {
      parsed.port = DEFAULT_LOCAL_APP_PORT
      injectedPort = true
    }

    const baseUrl = `${parsed.protocol}//${parsed.host}`
    const finalAppUrl = normalizeAppUrl(baseUrl)
    diagnostics.parsedProtocol = parsed.protocol
    diagnostics.parsedHostnameRaw = parsed.hostname
    diagnostics.parsedHostnameNormalized = normalizedHost
    diagnostics.isLocalHostname = isLocal
    diagnostics.parsedPortBefore = portBefore
    diagnostics.parsedPortAfter = parsed.port
    diagnostics.injectedDefaultLocalPort = injectedPort
    diagnostics.finalAppUrl = finalAppUrl
    return { appUrl: finalAppUrl, diagnostics }
  } catch {
    diagnostics.fallbackReason = "invalid_url_parse"
    diagnostics.finalAppUrl = normalizedDefault
    return { appUrl: normalizedDefault, diagnostics }
  }
}

const appUrlResolution = resolveAppUrl()
export const APP_URL = appUrlResolution.appUrl

function logResolvedAppUrlOnce(): void {
  if (process.env.NODE_ENV !== "development") {
    return
  }

  const globalState = globalThis as typeof globalThis & Record<string, boolean | undefined>
  if (globalState[APP_URL_LOG_KEY]) {
    return
  }

  globalState[APP_URL_LOG_KEY] = true
  console.info(
    "[prosa-landing] APP_URL=",
    APP_URL,
    "NEXT_PUBLIC_APP_URL=",
    process.env.NEXT_PUBLIC_APP_URL,
  )
  console.info("[prosa-landing] APP_URL diagnostics", appUrlResolution.diagnostics)
  console.info("[prosa-landing] APP_LINKS preview", {
    login: `${APP_URL}/login?returnUrl=/projects`,
    startFree: `${APP_URL}/login?returnUrl=/start?plan=free`,
    startStandard: `${APP_URL}/start?plan=standard`,
    startPro: `${APP_URL}/start?plan=pro`,
  })
}

logResolvedAppUrlOnce()

export const APP_LINKS = {
  login: `${APP_URL}/login?returnUrl=/projects`,
  startFree: `${APP_URL}/login?returnUrl=/start?plan=free`,
  startStandard: `${APP_URL}/start?plan=standard`,
  startPro: `${APP_URL}/start?plan=pro`,
} as const
