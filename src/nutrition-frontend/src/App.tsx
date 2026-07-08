import { useCallback, useEffect, useReducer, useState } from 'react'
import type {
  AppState,
  Attachment,
  ChatMessage,
  CurrentUser,
  MealEditContext,
  MealEntryItem,
  NutritionChatSearchResponse,
  NutritionClarification,
  ProductNutrition,
} from './types'
import { chatReducer } from './chatReducer'
import { AuthView } from './AuthView'
import { ChatView } from './ChatView'
import { ChatInputFooter } from './ChatInputFooter'
import { ProfileView } from './ProfileView'
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
  const [route, setRoute] = useState(window.location.pathname === '/profile' ? 'profile' : 'chat')
  const [mealEditContext, setMealEditContext] = useState<MealEditContext | null>(readMealEditContext())

  const hasPendingClarification = state.messages.some(
    (message) =>
      message.kind === 'assistant-result' &&
      message.clarifications.some((clarification) => clarification.status === 'pending' || clarification.status === 'refining'),
  )

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

  useEffect(() => {
    const handlePopState = () => {
      setRoute(window.location.pathname === '/profile' ? 'profile' : 'chat')
      setMealEditContext(readMealEditContext())
    }

    window.addEventListener('popstate', handlePopState)
    return () => window.removeEventListener('popstate', handlePopState)
  }, [])

  const navigate = useCallback((nextRoute: 'chat' | 'profile') => {
    const path = nextRoute === 'profile' ? '/profile' : '/'
    window.history.pushState(null, '', path)
    setRoute(nextRoute)
    if (nextRoute === 'chat') {
      setMealEditContext(null)
    }
  }, [])

  const handleLogout = useCallback(async () => {
    await fetch('/api/v1/auth/logout', {
      method: 'POST',
      credentials: 'include',
    })
    setCurrentUser(null)
    navigate('chat')
  }, [navigate])

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
      if (!trimmed && attachments.length > 0) {
        const assistantMsg: ChatMessage = {
          kind: 'assistant-result',
          id: crypto.randomUUID(),
          query: 'Вложение',
          items: [],
          clarifications: [],
          activeClarificationIndex: 0,
          error: 'Файл выбран, но backend для отправки вложений пока не подключен.',
        }
        dispatch({ type: 'ADD_MESSAGE', message: assistantMsg })
        return
      }

      const response = await fetch(
        `/api/v1/nutrition/search?query=${encodeURIComponent(trimmed)}`,
        { credentials: 'include' },
      )

      if (!response.ok) {
        throw new Error(`API error: ${response.status}`)
      }

      const payload = normalizeSearchResponse(await response.json(), trimmed)
      const assistantMsg: ChatMessage = {
        kind: 'assistant-result',
        id: crypto.randomUUID(),
        query: trimmed,
        items: payload.items,
        clarifications: payload.clarifications,
        activeClarificationIndex: 0,
        error:
          payload.items.length === 0 && payload.clarifications.length === 0
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
        clarifications: [],
        activeClarificationIndex: 0,
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

  const handleResolveClarification = useCallback(
    async (messageId: string, clarificationId: string, product: ProductNutrition) => {
      await saveProductToProfile(product, mealEditContext)
      dispatch({ type: 'RESOLVE_CLARIFICATION', messageId, clarificationId, product })
    },
    [mealEditContext],
  )

  const handleSaveProduct = useCallback(
    async (product: ProductNutrition) => {
      await saveProductToProfile(product, mealEditContext)
      if (mealEditContext) {
        setMealEditContext(null)
        window.history.replaceState(null, '', '/')
      }
    },
    [mealEditContext],
  )

  const handleEditMeal = useCallback((entry: MealEntryItem) => {
    const params = new URLSearchParams({
      mealEntryId: entry.id,
      mealType: entry.mealType,
    })
    window.history.pushState(null, '', `/?${params.toString()}`)
    setMealEditContext({ mealEntryId: entry.id, mealType: entry.mealType })
    setRoute('chat')
  }, [])

  const handleCancelClarification = useCallback((messageId: string, clarificationId: string) => {
    dispatch({ type: 'CANCEL_CLARIFICATION', messageId, clarificationId })
  }, [])

  const handleManualClarification = useCallback(
    async (messageId: string, clarificationId: string, query: string) => {
      const trimmed = query.trim()
      if (!trimmed) return

      dispatch({ type: 'START_MANUAL_CLARIFICATION', messageId, clarificationId })
      try {
        const response = await fetch(
          `/api/v1/nutrition/search?query=${encodeURIComponent(trimmed)}`,
          { credentials: 'include' },
        )

        if (!response.ok) {
          throw new Error(`API error: ${response.status}`)
        }

        const payload = normalizeSearchResponse(await response.json(), trimmed)
        const nextClarification = payload.clarifications[0]
        const candidates = nextClarification?.candidates.length
          ? nextClarification.candidates
          : payload.items.slice(0, 3)

        dispatch({
          type: 'REPLACE_CLARIFICATION_CANDIDATES',
          messageId,
          clarificationId,
          parsedProductName: nextClarification?.parsedProductName ?? trimmed,
          question: nextClarification?.question ?? `Выберите подходящий вариант для "${trimmed}"`,
          candidates,
        })
      } catch {
        dispatch({
          type: 'REPLACE_CLARIFICATION_CANDIDATES',
          messageId,
          clarificationId,
          parsedProductName: trimmed,
          question: `Не удалось уточнить "${trimmed}". Попробуйте другой запрос.`,
          candidates: [],
        })
      }
    },
    [],
  )

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

  if (route === 'profile') {
    return (
      <div className="app-shell">
        <ProfileView
          onBackToChat={() => navigate('chat')}
          onUnauthorized={() => setCurrentUser(null)}
          onEditMeal={handleEditMeal}
        />
      </div>
    )
  }

  const initials = `${currentUser.firstName.charAt(0)}${currentUser.secondName.charAt(0)}`.toUpperCase()

  return (
    <div className="app-shell">
      <header className="app-header">
        <button type="button" className="profile-initials-btn" onClick={() => navigate('profile')} aria-label="Open profile">
          {initials}
        </button>
        <div className="app-header-user">
          <strong>{currentUser.firstName} {currentUser.secondName}</strong>
          <span>{currentUser.email}</span>
        </div>
        <button type="button" onClick={handleLogout}>
          Logout
        </button>
      </header>
      <ChatView
        messages={state.messages}
        loading={state.loading}
        onResolveClarification={handleResolveClarification}
        onCancelClarification={handleCancelClarification}
        onManualClarification={handleManualClarification}
        onSaveProduct={handleSaveProduct}
        onSetActiveClarification={(messageId, index) =>
          dispatch({ type: 'SET_ACTIVE_CLARIFICATION', messageId, index })
        }
        mealEditContext={mealEditContext}
      />
      <ChatInputFooter
        inputText={state.inputText}
        attachments={state.attachments}
        recordingState={state.recordingState}
        loading={state.loading}
        locked={hasPendingClarification}
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

async function saveProductToProfile(product: ProductNutrition, editContext: MealEditContext | null) {
  const facts = normalizeCalories(product.nutritionFacts)
  const payload = {
    productName: product.productName,
    brand: product.brand ?? '',
    calories: facts.calories,
    protein: facts.protein,
    fat: facts.fat,
    carbs: facts.carbs,
    mealType: editContext?.mealType ?? 'Snack',
  }

  const response = await fetch(
    editContext ? `/api/v1/profile/entry/${editContext.mealEntryId}` : '/api/v1/profile/entry',
    {
      method: editContext ? 'PUT' : 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    },
  )

  if (!response.ok) {
    throw new Error(`Profile entry API error: ${response.status}`)
  }
}

function normalizeCalories(facts: ProductNutrition['nutritionFacts']) {
  const protein = facts?.protein ?? 0
  const fat = facts?.fat ?? 0
  const carbs = facts?.carbs ?? 0
  const calculatedCalories = protein * 4 + fat * 9 + carbs * 4
  const calories = facts?.calories ?? calculatedCalories

  return {
    protein,
    fat,
    carbs,
    calories: Math.abs(calories - calculatedCalories) > 1 ? Number(calculatedCalories.toFixed(1)) : calories,
  }
}

function readMealEditContext(): MealEditContext | null {
  const params = new URLSearchParams(window.location.search)
  const mealEntryId = params.get('mealEntryId')
  const mealType = params.get('mealType')
  return mealEntryId && mealType ? { mealEntryId, mealType } : null
}

function normalizeSearchResponse(raw: unknown, fallbackQuery: string): {
  items: ProductNutrition[]
  clarifications: NutritionClarification[]
} {
  if (Array.isArray(raw)) {
    return { items: raw as ProductNutrition[], clarifications: [] }
  }

  const payload = raw as Partial<NutritionChatSearchResponse> | null
  const items = Array.isArray(payload?.items) ? payload.items : []
  const clarifications = Array.isArray(payload?.clarifications)
    ? payload.clarifications.map((clarification, index) => ({
        id: clarification.id || `${fallbackQuery}-${index}`,
        originalInput: clarification.originalInput || fallbackQuery,
        parsedProductName: clarification.parsedProductName || fallbackQuery,
        question:
          clarification.question ||
          `Выберите подходящий вариант для "${clarification.parsedProductName || fallbackQuery}"`,
        candidates: Array.isArray(clarification.candidates) ? clarification.candidates : [],
        status: 'pending' as const,
        selectedProduct: null,
      }))
    : []

  return { items, clarifications }
}

export default App
