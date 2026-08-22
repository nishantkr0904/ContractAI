import axios from 'axios'
import type { AxiosError } from 'axios'

const TOKEN_STORAGE_KEY = 'contractai.token'

// The token lives in a module variable (mirrored to localStorage) rather than being
// read from React state, so the request interceptor always sees the current value
// without a stale closure. localStorage access is wrapped because it throws in some
// private-browsing modes; there we degrade to an in-memory-only token.
let authToken: string | null = readStoredToken()
let unauthorizedHandler: (() => void) | null = null

function readStoredToken(): string | null {
  try {
    return localStorage.getItem(TOKEN_STORAGE_KEY)
  } catch {
    return null
  }
}

export function getAuthToken(): string | null {
  return authToken
}

export function setAuthToken(token: string | null): void {
  authToken = token
  try {
    if (token) {
      localStorage.setItem(TOKEN_STORAGE_KEY, token)
    } else {
      localStorage.removeItem(TOKEN_STORAGE_KEY)
    }
  } catch {
    // storage unavailable — keep the in-memory token for this session only
  }
}

// The auth provider registers a callback here so a 401 from anywhere can force a
// logout without this module having to import React.
export function setUnauthorizedHandler(handler: (() => void) | null): void {
  unauthorizedHandler = handler
}

// baseURL is the versioned prefix only. Vite proxies /api to the backend in dev, and
// the built assets are served behind the same origin in prod, so this relative path
// resolves correctly in both.
export const api = axios.create({
  baseURL: '/api/v1',
})

api.interceptors.request.use((config) => {
  if (authToken) {
    config.headers.Authorization = `Bearer ${authToken}`
  }
  return config
})

api.interceptors.response.use(
  (response) => response,
  (error: AxiosError) => {
    if (error.response?.status === 401) {
      setAuthToken(null)
      unauthorizedHandler?.()
    }
    return Promise.reject(error)
  },
)
