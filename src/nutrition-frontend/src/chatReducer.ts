import type { AppState, AppAction } from './types'

export function chatReducer(state: AppState, action: AppAction): AppState {
  switch (action.type) {
    case 'SET_INPUT_TEXT':
      return { ...state, inputText: action.text }

    case 'ADD_ATTACHMENTS': {
      const available = 10 - state.attachments.length
      if (available <= 0) return state
      const toAdd = action.attachments.slice(0, available)
      return { ...state, attachments: [...state.attachments, ...toAdd] }
    }

    case 'REMOVE_ATTACHMENT':
      return {
        ...state,
        attachments: state.attachments.filter((a) => a.id !== action.id),
      }

    case 'CLEAR_ATTACHMENTS':
      return { ...state, attachments: [] }

    case 'SET_RECORDING_STATE':
      return { ...state, recordingState: action.state }

    case 'SET_LOADING':
      return { ...state, loading: action.loading }

    case 'ADD_MESSAGE':
      return { ...state, messages: [...state.messages, action.message] }

    case 'RESOLVE_CLARIFICATION':
      return {
        ...state,
        messages: state.messages.map((message) => {
          if (message.kind !== 'assistant-result' || message.id !== action.messageId) {
            return message
          }

          const clarifications = message.clarifications.map((clarification) =>
            clarification.id === action.clarificationId
              ? { ...clarification, status: 'answered' as const, selectedProduct: action.product }
              : clarification,
          )
          const nextPendingIndex = clarifications.findIndex((item) => item.status === 'pending')

          return {
            ...message,
            clarifications,
            activeClarificationIndex:
              nextPendingIndex >= 0 ? nextPendingIndex : message.activeClarificationIndex,
          }
        }),
      }

    case 'CANCEL_CLARIFICATION':
      return {
        ...state,
        messages: state.messages.map((message) => {
          if (message.kind !== 'assistant-result' || message.id !== action.messageId) {
            return message
          }

          const clarifications = message.clarifications.map((clarification) =>
            clarification.id === action.clarificationId
              ? { ...clarification, status: 'cancelled' as const, selectedProduct: null }
              : clarification,
          )
          const nextPendingIndex = clarifications.findIndex((item) => item.status === 'pending')

          return {
            ...message,
            clarifications,
            activeClarificationIndex:
              nextPendingIndex >= 0 ? nextPendingIndex : message.activeClarificationIndex,
          }
        }),
      }

    case 'START_MANUAL_CLARIFICATION':
      return {
        ...state,
        messages: state.messages.map((message) => {
          if (message.kind !== 'assistant-result' || message.id !== action.messageId) {
            return message
          }

          return {
            ...message,
            clarifications: message.clarifications.map((clarification) =>
              clarification.id === action.clarificationId
                ? { ...clarification, status: 'refining' as const, selectedProduct: null }
                : clarification,
            ),
          }
        }),
      }

    case 'REPLACE_CLARIFICATION_CANDIDATES':
      return {
        ...state,
        messages: state.messages.map((message) => {
          if (message.kind !== 'assistant-result' || message.id !== action.messageId) {
            return message
          }

          return {
            ...message,
            clarifications: message.clarifications.map((clarification) =>
              clarification.id === action.clarificationId
                ? {
                    ...clarification,
                    parsedProductName: action.parsedProductName,
                    question: action.question,
                    candidates: action.candidates,
                    status: 'pending' as const,
                    selectedProduct: null,
                  }
                : clarification,
            ),
          }
        }),
      }

    case 'SET_ACTIVE_CLARIFICATION':
      return {
        ...state,
        messages: state.messages.map((message) =>
          message.kind === 'assistant-result' && message.id === action.messageId
            ? { ...message, activeClarificationIndex: action.index }
            : message,
        ),
      }

    case 'SET_ERROR':
      return { ...state, error: action.error }

    case 'RESET_INPUT':
      return { ...state, inputText: '', attachments: [], recordingState: 'idle' }

    default:
      return state
  }
}
