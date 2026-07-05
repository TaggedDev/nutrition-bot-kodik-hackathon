import { useCallback, useEffect, useReducer, useState } from 'react'
import type { AppState, Attachment, ChatMessage, CurrentUser } from './types'
import { chatReducer } from './chatReducer'
import { AuthView } from './AuthView'
import { ChatView } from './ChatView'
import { ChatInputFooter } from './ChatInputFooter'
import './App.css'

const initialState: AppState = {
  messages: [],
  inputText: '',
  attachments: [],
  recordingState: 'idle',
  loading: false,
  error: null,
}

function App() {
  const [state, dispatch] = useReducer(chatReducer, initialState)
  const [currentUser, setCurrentUser] = useState<CurrentUser | null>(null)
  const [authChecked, setAuthChecked] = useState(false)

  useEffect(() => {
    let active = true

    async function loadCurrentUser() {
      try {
        const response = await fetch('/api/v1/auth/me', {
          credentials: 'include',
        })

        if (!active) return

        if (response.ok) {
          setCurrentUser((await response.json()) as CurrentUser)
        } else {
          setCurrentUser(null)
        }
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

  const handleLogout = useCallback(async () => {
    await fetch('/api/v1/auth/logout', {
      method: 'POST',
      credentials: 'include',
    })
    setCurrentUser(null)
  }, [])

  const handleSendText = useCallback(async (text: string, attachments: Attachment[]) => {
    const trimmed = text.trim()
    const userMsg: ChatMessage = {
      kind: 'user-text',
      id: crypto.randomUUID(),
      text: trimmed,
      attachments: [...attachments],
    }

    dispatch({ type: 'ADD_MESSAGE', message: userMsg })
    dispatch({ type: 'CLEAR_ATTACHMENTS' })
    dispatch({ type: 'SET_INPUT_TEXT', text: '' })
    dispatch({ type: 'SET_LOADING', loading: true })
    dispatch({ type: 'SET_ERROR', error: null })

    try {
      // TODO: connect real file/audio upload endpoints when backend contracts are added.
      if (!trimmed && attachments.length > 0) {
        const assistantMsg: ChatMessage = {
          kind: 'assistant-result',
          id: crypto.randomUUID(),
          query: 'Вложение',
          items: [],
          error: 'Файл выбран, но backend для отправки вложений пока не подключен.',
        }
        dispatch({ type: 'ADD_MESSAGE', message: assistantMsg })
        return
      }

      const response = await fetch(
        `/api/v1/nutrition/search?query=${encodeURIComponent(trimmed)}`,
        {
          credentials: 'include',
        },
      )

      if (!response.ok) {
        throw new Error(`API error: ${response.status}`)
      }

      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const items = (await response.json()) as any[]

      const assistantMsg: ChatMessage = {
        kind: 'assistant-result',
        id: crypto.randomUUID(),
        query: trimmed,
        items,
        error:
          !Array.isArray(items) || items.length === 0
            ? 'Ничего не найдено. Попробуйте другой запрос.'
            : undefined,
      }
      dispatch({ type: 'ADD_MESSAGE', message: assistantMsg })
    } catch (err) {
      const assistantMsg: ChatMessage = {
        kind: 'assistant-result',
        id: crypto.randomUUID(),
        query: trimmed,
        items: [],
        error:
          err instanceof Error
            ? `Не удалось выполнить поиск: ${err.message}`
            : 'Не удалось выполнить поиск. Проверьте, что backend запущен.',
      }
      dispatch({ type: 'ADD_MESSAGE', message: assistantMsg })
    } finally {
      dispatch({ type: 'SET_LOADING', loading: false })
    }
  }, [])

  const handleSendVoice = useCallback((audioBlob: Blob, duration: number) => {
    const voiceMsg: ChatMessage = {
      kind: 'user-voice',
      id: crypto.randomUUID(),
      audio: {
        blob: audioBlob,
        url: URL.createObjectURL(audioBlob),
        duration,
      },
    }
    dispatch({ type: 'ADD_MESSAGE', message: voiceMsg })
  }, [])

  if (!authChecked) {
    return (
      <main className="app-loading">
        <div>Loading...</div>
      </main>
    )
  }

  if (!currentUser) {
    return <AuthView onAuthenticated={setCurrentUser} />
  }

  return (
    <div className="app-shell">
      <header className="app-header">
        <div>
          <strong>{currentUser.firstName} {currentUser.secondName}</strong>
          <span>{currentUser.email}</span>
        </div>
        <button type="button" onClick={handleLogout}>
          Logout
        </button>
      </header>
      <ChatView messages={state.messages} loading={state.loading} />
      <ChatInputFooter
        inputText={state.inputText}
        attachments={state.attachments}
        recordingState={state.recordingState}
        loading={state.loading}
        onInputTextChange={(text) => dispatch({ type: 'SET_INPUT_TEXT', text })}
        onAddAttachments={(atts) => dispatch({ type: 'ADD_ATTACHMENTS', attachments: atts })}
        onRemoveAttachment={(id) => dispatch({ type: 'REMOVE_ATTACHMENT', id })}
        onClearAttachments={() => dispatch({ type: 'CLEAR_ATTACHMENTS' })}
        onRecordingStateChange={(s) => dispatch({ type: 'SET_RECORDING_STATE', state: s })}
        onSendText={handleSendText}
        onSendVoice={handleSendVoice}
      />
    </div>
  )
}

export default App
