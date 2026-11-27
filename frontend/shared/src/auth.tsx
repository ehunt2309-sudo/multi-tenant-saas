import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api } from './api'
import { getStoredRefreshToken, hasStoredSession, onSessionChange, refreshSession, setSession } from './http'
import type { RegisterInput, Role, UserInfo } from './types'

interface AuthState {
  user: UserInfo | null
  /** true while restoring a session after a page reload */
  loading: boolean
  login: (email: string, password: string) => Promise<UserInfo>
  register: (input: RegisterInput) => Promise<UserInfo>
  logout: () => Promise<void>
  hasRole: (...roles: Role[]) => boolean
}

const AuthContext = createContext<AuthState | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserInfo | null>(null)
  const [loading, setLoading] = useState(hasStoredSession())

  useEffect(() => onSessionChange(auth => setUser(auth?.user ?? null)), [])

  // Restore the session after a reload using the stored refresh token.
  useEffect(() => {
    if (!hasStoredSession()) return
    refreshSession().finally(() => setLoading(false))
  }, [])

  const login = useCallback(async (email: string, password: string) => {
    const auth = await api.auth.login(email, password)
    setSession(auth)
    return auth.user
  }, [])

  const register = useCallback(async (input: RegisterInput) => {
    const auth = await api.auth.register(input)
    setSession(auth)
    return auth.user
  }, [])

  const logout = useCallback(async () => {
    const token = getStoredRefreshToken()
    setSession(null)                                  // sign out locally first, never block on the network
    if (token) await api.auth.logout(token).catch(() => undefined)
  }, [])

  const value = useMemo<AuthState>(() => ({
    user, loading, login, register, logout,
    hasRole: (...roles) => !!user && roles.includes(user.role),
  }), [user, loading, login, register, logout])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside <AuthProvider>')
  return ctx
}
