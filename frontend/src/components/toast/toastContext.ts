import { createContext, useContext } from 'react'

export type ToastTone = 'success' | 'error' | 'info'

export interface Toast {
  id: number
  message: string
  tone: ToastTone
}

export interface ToastContextValue {
  showToast: (message: string, tone?: ToastTone) => void
}

export const ToastContext = createContext<ToastContextValue | null>(null)

export function useToast(): ToastContextValue {
  const value = useContext(ToastContext)
  if (!value) {
    throw new Error('useToast must be used within a ToastProvider')
  }
  return value
}
