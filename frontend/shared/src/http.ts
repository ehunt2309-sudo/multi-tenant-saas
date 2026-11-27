import type { AuthResponse } from './types'

/**
 * Tiny fetch wrapper for the platform API.
 *  - Access token lives in memory only.
 *  - Refresh token lives in sessionStorage (survives reloads, dies with the tab). The backend rotates it on
 *    every use, so a stolen old token is useless and replaying one revokes the whole session.
 *  - Parallel 401s share ONE refresh call. This matters: two simultaneous refreshes would look like token
 *    theft to the backend and sign the user out.
 */

const REFRESH_KEY = 'platform.refresh'

let baseUrl = ''
let accessToken: string | null = null
let refreshInFlight: Promise<boolean> | null = null
let sessionListeners: Array<(auth: AuthResponse | null) => void> = []

export function configureApi(opts: { baseUrl: string }) {
  baseUrl = opts.baseUrl.replace(/\/$/, '')
}

export function onSessionChange(fn: (auth: AuthResponse | null) => void) {
  sessionListeners.push(fn)
  return () => { sessionListeners = sessionListeners.filter(l => l !== fn) }
}

export function hasStoredSession() {
  return !!sessionStorage.getItem(REFRESH_KEY)
}

export function setSession(auth: AuthResponse | null) {
  accessToken = auth?.accessToken ?? null
  if (auth) sessionStorage.setItem(REFRESH_KEY, auth.refreshToken)
  else sessionStorage.removeItem(REFRESH_KEY)
  sessionListeners.forEach(l => l(auth))
}

export function getStoredRefreshToken() {
  return sessionStorage.getItem(REFRESH_KEY)
}

export class ApiError extends Error {
  constructor(public status: number, public title: string, message: string) {
    super(message)
  }
}

type Query = Record<string, string | number | boolean | null | undefined>

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'
  body?: unknown
  query?: Query
  /** false for public endpoints / login so no token is sent and no refresh is attempted */
  auth?: boolean
}

function buildUrl(path: string, query?: Query) {
  const url = new URL(baseUrl + path)
  if (query) for (const [k, v] of Object.entries(query)) if (v !== undefined && v !== null && v !== '') url.searchParams.set(k, String(v))
  return url.toString()
}

async function send(path: string, opts: RequestOptions): Promise<Response> {
  const headers: Record<string, string> = {}
  if (opts.body !== undefined) headers['Content-Type'] = 'application/json'
  if (opts.auth !== false && accessToken) headers['Authorization'] = `Bearer ${accessToken}`
  return fetch(buildUrl(path, opts.query), {
    method: opts.method ?? 'GET',
    headers,
    body: opts.body !== undefined ? JSON.stringify(opts.body) : undefined,
  })
}

/** Returns true if a fresh access token is now available. */
export function refreshSession(): Promise<boolean> {
  if (refreshInFlight) return refreshInFlight
  const refreshToken = getStoredRefreshToken()
  if (!refreshToken) return Promise.resolve(false)

  refreshInFlight = (async () => {
    try {
      const res = await fetch(baseUrl + '/api/auth/refresh', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      })
      if (!res.ok) { setSession(null); return false }
      setSession((await res.json()) as AuthResponse)
      return true
    } catch {
      return false // network blip: keep the session, let the caller retry later
    } finally {
      refreshInFlight = null
    }
  })()
  return refreshInFlight
}

async function parseError(res: Response): Promise<ApiError> {
  let title = res.statusText, detail = ''
  try {
    const data = await res.json()
    title = data.title ?? title
    detail = data.detail ?? ''
    if (data.errors) detail = Object.values<string[]>(data.errors).flat()[0] ?? detail   // validation errors
  } catch { /* non-JSON body */ }
  if (res.status === 429) detail = 'Too many attempts. Please wait a moment and try again.'
  return new ApiError(res.status, title, detail || title || 'Something went wrong.')
}

export async function request<T>(path: string, opts: RequestOptions = {}): Promise<T> {
  let res = await send(path, opts)
  if (res.status === 401 && opts.auth !== false && (await refreshSession())) res = await send(path, opts)
  if (!res.ok) throw await parseError(res)
  if (res.status === 204) return undefined as T
  return (await res.json()) as T
}
