import { useCallback, useEffect, useState } from 'react'
import { AuthView } from './AuthView'
import { ChatPage } from './ChatPage'
import { ProfileView } from './ProfileView'
import type { CurrentUser } from './types'
import './App.css'

function App() {
  const [currentUser, setCurrentUser] = useState<CurrentUser | null>(null)
  const [authChecked, setAuthChecked] = useState(false)
  const [route, setRoute] = useState(window.location.pathname === '/profile' ? 'profile' : 'chat')

  useEffect(() => {
    let active = true

    async function loadCurrentUser() {
      try {
        const response = await fetch('/api/v1/auth/me', { credentials: 'include' })
        if (!active) return

        setCurrentUser(response.ok ? ((await response.json()) as CurrentUser) : null)
      } catch {
        if (active) {
          setCurrentUser(null)
        }
      } finally {
        if (active) {
          setAuthChecked(true)
        }
      }
    }

    loadCurrentUser()

    return () => {
      active = false
    }
  }, [])

  useEffect(() => {
    const handlePopState = () => setRoute(window.location.pathname === '/profile' ? 'profile' : 'chat')
    window.addEventListener('popstate', handlePopState)
    return () => window.removeEventListener('popstate', handlePopState)
  }, [])

  const navigate = useCallback((nextRoute: 'chat' | 'profile') => {
    window.history.pushState(null, '', nextRoute === 'profile' ? '/profile' : '/')
    setRoute(nextRoute)
  }, [])

  const handleUnauthorized = useCallback(() => {
    setCurrentUser(null)
    window.history.replaceState(null, '', '/')
    setRoute('chat')
  }, [])

  const handleEditMeal = useCallback(() => {
    navigate('chat')
  }, [navigate])

  if (!authChecked) {
    return (
      <main className="app-loading">
        <div>Загрузка...</div>
      </main>
    )
  }

  if (!currentUser) {
    return <AuthView onAuthenticated={setCurrentUser} />
  }

  if (route === 'profile') {
    return (
      <ProfileView
        onBackToChat={() => navigate('chat')}
        onUnauthorized={handleUnauthorized}
        onEditMeal={handleEditMeal}
      />
    )
  }

  return (
    <ChatPage
      currentUser={currentUser}
      onOpenProfile={() => navigate('profile')}
      onUnauthorized={handleUnauthorized}
    />
  )
}

export default App
