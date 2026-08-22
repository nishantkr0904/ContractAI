import { createContext, useContext } from 'react'

export type DevRole = 'reader' | 'writer'

export interface AuthContextValue {
  token: string | null
  scope: string | null
  isAuthenticated: boolean
  login: (role: DevRole) => Promise<void>
  logout: () => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth(): AuthContextValue {
  const value = useContext(AuthContext)
  if (!value) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return value
}
