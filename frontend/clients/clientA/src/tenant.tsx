import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react'
import { api, type PublicTenant, type SiteContent } from '@platform/shared'

export const DEFAULT_CONTENT: Required<SiteContent> = {
  tagline: 'A sanctuary for the senses',
  about: 'Step out of the everyday. Every treatment is tailored to you, every detail considered, every visit unhurried.',
  address: '',
  phone: '',
  email: '',
  hours: [],
}

interface TenantState {
  tenant: PublicTenant | null
  content: Required<SiteContent>
  error: string | null
  /** Re-fetch branding + site content (after the manager edits them). */
  reload: () => void
}
const TenantContext = createContext<TenantState>({ tenant: null, content: DEFAULT_CONTENT, error: null, reload: () => {} })

/** Loads this venue's branding and editable site content. The API knows who we are from our domain. */
export function TenantProvider({ children }: { children: ReactNode }) {
  const [tenant, setTenant] = useState<PublicTenant | null>(null)
  const [content, setContent] = useState<Required<SiteContent>>(DEFAULT_CONTENT)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(() => {
    Promise.all([
      api.tenant.public(),
      api.pages.getPublic('home').then(p => p.content).catch(() => ({}) as SiteContent),
    ])
      .then(([t, c]) => {
        document.title = t.name
        if (t.primaryColor) document.documentElement.style.setProperty('--accent', t.primaryColor)
        else document.documentElement.style.removeProperty('--accent')
        setTenant(t)
        setContent({ ...DEFAULT_CONTENT, ...c })
        setError(null)
      })
      .catch(e => setError(e instanceof Error ? e.message : 'Could not reach the server.'))
  }, [])

  useEffect(load, [load])

  return <TenantContext.Provider value={{ tenant, content, error, reload: load }}>{children}</TenantContext.Provider>
}

export const useTenant = () => useContext(TenantContext)
