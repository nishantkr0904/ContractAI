import { useCallback, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { api, getAuthToken, setAuthToken, setUnauthorizedHandler } from '../api/client'
import type { DevTokenResponse } from '../types/api'
import { AuthContext } from './authContext'
import type { AuthContextValue, DevRole } from './authContext'

// On reload only the token is restored (the server re-validates it); scope is not
// persisted because it's a client-held claim we shouldn't trust for gating — it's
// tracked here purely for display and starts null until the next login.
export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(() => getAuthToken())
  const [scope, setScope] = useState<string | null>(null)

  const logout = useCallback(() => {
    setAuthToken(null)
    setToken(null)
    setScope(null)
  }, [])

  const login = useCallback(async (role: DevRole) => {
    const { data } = await api.post<DevTokenResponse>('/auth/dev-token', { role })
    setAuthToken(data.token)
    setToken(data.token)
    setScope(data.scope)
  }, [])

  // A 401 from any request (expired or rejected token) drops us back to the login
  // screen. The interceptor has already cleared the stored token by this point.
  useEffect(() => {
    setUnauthorizedHandler(() => {
      setToken(null)
      setScope(null)
    })
    return () => setUnauthorizedHandler(null)
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({
      token,
      scope,
      isAuthenticated: token !== null,
      login,
      logout,
    }),
    [token, scope, login, logout],
  )

  return <AuthContext value={value}>{children}</AuthContext>
}
