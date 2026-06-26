export type NutritionFacts = {
  calories: number
  protein: number
  fat: number
  carbs: number
}

export type ProductNutrition = {
  productId: string
  productName: string
  brand: string | null
  nutritionFacts: NutritionFacts
  sourceType: string
  sourceReference: string
  confidenceScore: number
}

export type Attachment = {
  id: string
  file: File
  previewUrl: string
}

export type VoiceAttachment = {
  blob: Blob
  url: string
  duration: number
}

export type ChatMessage =
  | {
      kind: 'user-text'
      id: string
      text: string
      attachments: Attachment[]
    }
  | {
      kind: 'user-voice'
      id: string
      audio: VoiceAttachment
    }
  | {
      kind: 'assistant-result'
      id: string
      query: string
      items: ProductNutrition[]
      error?: string
    }

export type RecordingState = 'idle' | 'recording' | 'locked'

export type AppAction =
  | { type: 'SET_INPUT_TEXT'; text: string }
  | { type: 'ADD_ATTACHMENTS'; attachments: Attachment[] }
  | { type: 'REMOVE_ATTACHMENT'; id: string }
  | { type: 'CLEAR_ATTACHMENTS' }
  | { type: 'SET_RECORDING_STATE'; state: RecordingState }
  | { type: 'SET_LOADING'; loading: boolean }
  | { type: 'ADD_MESSAGE'; message: ChatMessage }
  | { type: 'SET_ERROR'; error: string | null }
  | { type: 'RESET_INPUT' }

export type AppState = {
  messages: ChatMessage[]
  inputText: string
  attachments: Attachment[]
  recordingState: RecordingState
  loading: boolean
  error: string | null
}
