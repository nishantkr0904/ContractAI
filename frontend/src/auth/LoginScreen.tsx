import { useState } from 'react'
import type { FormEvent } from 'react'
import { useAuth } from './authContext'
import type { DevRole } from './authContext'

// Development sign-in: the backend mints a JWT from POST /auth/dev-token (only when
// running in the Development environment). The role picker maps to reader/writer
// scopes so the same screen can exercise both authorization policies.
export function LoginScreen() {
  const { login } = useAuth()
  const [role, setRole] = useState<DevRole>('writer')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      await login(role)
    } catch {
      setError('Sign-in failed. Confirm the API is running in the Development environment.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="grid min-h-screen place-items-center px-4">
      <form
        onSubmit={handleSubmit}
        className="w-full max-w-sm rounded-xl border border-slate-200 bg-white p-8 shadow-sm"
      >
        <h1 className="text-2xl font-semibold text-slate-800">ContractAI</h1>
        <p className="mt-1 text-sm text-slate-500">Sign in to review contracts.</p>

        <fieldset className="mt-6">
          <legend className="text-sm font-medium text-slate-700">Role</legend>
          <div className="mt-2 grid grid-cols-2 gap-2">
            {(['reader', 'writer'] as const).map((option) => (
              <label
                key={option}
                className={`cursor-pointer rounded-lg border px-3 py-2 text-center text-sm capitalize ${
                  role === option
                    ? 'border-slate-800 bg-slate-800 text-white'
                    : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300'
                }`}
              >
                <input
                  type="radio"
                  name="role"
                  value={option}
                  checked={role === option}
                  onChange={() => setRole(option)}
                  className="sr-only"
                />
                {option}
              </label>
            ))}
          </div>
        </fieldset>

        {error && <p className="mt-4 text-sm text-risk-critical">{error}</p>}

        <button
          type="submit"
          disabled={submitting}
          className="mt-6 w-full rounded-lg bg-slate-800 px-4 py-2 text-sm font-medium text-white hover:bg-slate-700 disabled:opacity-60"
        >
          {submitting ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </div>
  )
}
