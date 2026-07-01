import { useMemo, useState } from 'react'
import type { CurrentUser } from './types'
import './AuthView.css'

type AuthMode = 'login' | 'register'

type Props = {
  onAuthenticated: (user: CurrentUser) => void
}

type AuthErrorResponse = {
  errors?: string[]
}

const emptyRegisterForm = {
  email: '',
  firstName: '',
  secondName: '',
  password: '',
}

export function AuthView({ onAuthenticated }: Props) {
  const [mode, setMode] = useState<AuthMode>('login')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [firstName, setFirstName] = useState('')
  const [secondName, setSecondName] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const title = mode === 'login' ? 'Sign in' : 'Create account'
  const submitLabel = mode === 'login' ? 'Sign in' : 'Register'
  const passwordHint = useMemo(
    () => (mode === 'register' ? 'At least 8 chars, one digit, one lowercase letter.' : null),
    [mode],
  )

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setLoading(true)
    setError(null)

    const endpoint = mode === 'login' ? '/api/v1/auth/login' : '/api/v1/auth/register'
    const payload =
      mode === 'login'
        ? { email, password }
        : { ...emptyRegisterForm, email, firstName, secondName, password }

    try {
      const response = await fetch(endpoint, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        credentials: 'include',
        body: JSON.stringify(payload),
      })

      if (!response.ok) {
        if (response.status === 401) {
          throw new Error('Invalid email or password.')
        }

        const body = (await response.json().catch(() => null)) as AuthErrorResponse | null
        throw new Error(body?.errors?.join(' ') || `Auth failed: ${response.status}`)
      }

      const user = (await response.json()) as CurrentUser
      onAuthenticated(user)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Authentication failed.')
    } finally {
      setLoading(false)
    }
  }

  function switchMode(nextMode: AuthMode) {
    setMode(nextMode)
    setError(null)
  }

  return (
    <main className="auth-shell">
      <section className="auth-panel" aria-labelledby="auth-title">
        <div className="auth-brand">
          <div className="auth-logo">N</div>
          <div>
            <h1 id="auth-title">{title}</h1>
            <p>Use your Nutrition account to open the chat.</p>
          </div>
        </div>

        <div className="auth-tabs" role="tablist" aria-label="Authentication mode">
          <button
            type="button"
            className={mode === 'login' ? 'active' : ''}
            onClick={() => switchMode('login')}
          >
            Login
          </button>
          <button
            type="button"
            className={mode === 'register' ? 'active' : ''}
            onClick={() => switchMode('register')}
          >
            Register
          </button>
        </div>

        <form className="auth-form" onSubmit={handleSubmit}>
          {mode === 'register' && (
            <div className="auth-name-grid">
              <label>
                First name
                <input
                  value={firstName}
                  onChange={(event) => setFirstName(event.target.value)}
                  autoComplete="given-name"
                  required
                />
              </label>

              <label>
                Second name
                <input
                  value={secondName}
                  onChange={(event) => setSecondName(event.target.value)}
                  autoComplete="family-name"
                  required
                />
              </label>
            </div>
          )}

          <label>
            Email
            <input
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              autoComplete="email"
              required
            />
          </label>

          <label>
            Password
            <input
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
              required
            />
          </label>

          {passwordHint && <p className="auth-hint">{passwordHint}</p>}
          {error && <div className="auth-error">{error}</div>}

          <button type="submit" className="auth-submit" disabled={loading}>
            {loading ? 'Please wait...' : submitLabel}
          </button>
        </form>
      </section>
    </main>
  )
}
