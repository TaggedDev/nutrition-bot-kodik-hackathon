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
  const [rememberMe, setRememberMe] = useState(true)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const title = mode === 'login' ? 'Вход' : 'Создать аккаунт'
  const submitLabel = mode === 'login' ? 'Войти' : 'Зарегистрироваться'
  const passwordHint = useMemo(
    () => (mode === 'register' ? 'Минимум 8 символов, одна цифра и одна строчная буква.' : null),
    [mode],
  )

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setLoading(true)
    setError(null)

    const endpoint = mode === 'login' ? '/api/v1/auth/login' : '/api/v1/auth/register'
    const payload =
      mode === 'login'
        ? { email, password, rememberMe }
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
          throw new Error('Неверный email или пароль.')
        }

        const body = (await response.json().catch(() => null)) as AuthErrorResponse | null
        throw new Error(body?.errors?.join(' ') || `Ошибка входа: ${response.status}`)
      }

      const user = (await response.json()) as CurrentUser
      onAuthenticated(user)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Не удалось войти.')
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
          <div className="auth-logo" aria-hidden="true">
            <AuthUtensilsIcon />
          </div>
          <div>
            <h1 id="auth-title">{title}</h1>
            <p>Войдите в аккаунт Nutrition, чтобы открыть чат.</p>
          </div>
        </div>

        <div className="auth-tabs" role="tablist" aria-label="Режим авторизации">
          <button
            type="button"
            className={mode === 'login' ? 'active' : ''}
            onClick={() => switchMode('login')}
          >
            Вход
          </button>
          <button
            type="button"
            className={mode === 'register' ? 'active' : ''}
            onClick={() => switchMode('register')}
          >
            Регистрация
          </button>
        </div>

        <form className="auth-form" onSubmit={handleSubmit}>
          {mode === 'register' && (
            <div className="auth-name-grid">
              <label>
                Имя
                <input
                  value={firstName}
                  onChange={(event) => setFirstName(event.target.value)}
                  autoComplete="given-name"
                  required
                />
              </label>

              <label>
                Фамилия
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
            Пароль
            <input
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
              required
            />
          </label>

          {mode === 'login' && (
            <label className="remember-row">
              <input
                type="checkbox"
                checked={rememberMe}
                onChange={(event) => setRememberMe(event.target.checked)}
              />
              <span>Запомнить меня на этом устройстве</span>
            </label>
          )}

          {passwordHint && <p className="auth-hint">{passwordHint}</p>}
          {error && <div className="auth-error">{error}</div>}

          <button type="submit" className="auth-submit" disabled={loading}>
            {loading ? 'Подождите...' : submitLabel}
          </button>
        </form>
      </section>
    </main>
  )
}

function AuthUtensilsIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
      <path d="M7 3v7.2a3 3 0 0 1-1.9 2.8V21H3.7v-8A3 3 0 0 1 2 10.2V3h1.4v6h1V3h1.3v6h1V3H7Zm6.8 0c2.9.8 4.7 3.2 4.7 6v3.4h-2.1V21H15V3h-1.2Z" />
    </svg>
  )
}
