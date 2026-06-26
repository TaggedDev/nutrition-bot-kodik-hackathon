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

    case 'SET_ERROR':
      return { ...state, error: action.error }

    case 'RESET_INPUT':
      return { ...state, inputText: '', attachments: [], recordingState: 'idle' }

    default:
      return state
  }
}
