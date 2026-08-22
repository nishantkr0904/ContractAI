import { useCallback, useMemo, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { ToastContext } from './toastContext'
import type { Toast, ToastContextValue, ToastTone } from './toastContext'

const TOAST_TTL_MS = 4000

const toneClasses: Record<ToastTone, string> = {
  success: 'text-risk-low',
  error: 'text-risk-critical',
  info: 'text-slate-700',
}

// Transient notifications stacked bottom-right. Each auto-dismisses after a fixed TTL;
// ids come from a monotonic counter so a burst of toasts never collides on key.
export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([])
  const nextId = useRef(0)

  const showToast = useCallback((message: string, tone: ToastTone = 'info') => {
    const id = nextId.current++
    setToasts((current) => [...current, { id, message, tone }])
    window.setTimeout(() => {
      setToasts((current) => current.filter((toast) => toast.id !== id))
    }, TOAST_TTL_MS)
  }, [])

  const value = useMemo<ToastContextValue>(() => ({ showToast }), [showToast])

  return (
    <ToastContext value={value}>
      {children}
      <div className="fixed bottom-4 right-4 z-50 flex flex-col gap-2">
        {toasts.map((toast) => (
          <div
            key={toast.id}
            role="status"
            className={`rounded-lg border border-slate-200 bg-white px-4 py-2 text-sm shadow-md ${toneClasses[toast.tone]}`}
          >
            {toast.message}
          </div>
        ))}
      </div>
    </ToastContext>
  )
}
