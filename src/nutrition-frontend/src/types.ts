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

export type NutritionClarification = {
  id: string
  originalInput: string
  parsedProductName: string
  question: string
  candidates: ProductNutrition[]
  status: 'pending' | 'answered' | 'cancelled' | 'refining'
  selectedProduct: ProductNutrition | null
}

export type NutritionChatSearchResponse = {
  query: string
  items: ProductNutrition[]
  clarifications: Omit<NutritionClarification, 'status' | 'selectedProduct'>[]
  requiresClarification: boolean
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
      clarifications: NutritionClarification[]
      activeClarificationIndex: number
      error?: string
    }

export type RecordingState = 'idle' | 'recording' | 'locked'

export type CurrentUser = {
  userId: string
  email: string
  firstName: string
  secondName: string
}

export type ProfileData = {
  userId: string
  email: string
  firstName: string
  secondName: string
}

export type MealEntryItem = {
  id: string
  productName: string
  brand: string
  calories: number
  protein: number
  fat: number
  carbs: number
  mealType: string
  servingGrams: number
  portionLabel: string
  sourceType: string
  sourceReference: string
  loggedAtUtc: string
  createdAtUtc: string
}

export type NutritionSummary = {
  calories: number
  protein: number
  fat: number
  carbs: number
}

export type MealEntrySummaryByType = {
  mealType: string
  calories: number
  protein: number
  fat: number
  carbs: number
  count: number
}

export type DailyGoal = {
  targetCalories: number
  targetProtein: number
  targetFat: number
  targetCarbs: number
}

export type ProfileHistory = {
  entries: MealEntryItem[]
  totalSummary: NutritionSummary
}

export type ProfileSummaryByTypeResponse = {
  summaryByType: MealEntrySummaryByType[]
  totalSummary: NutritionSummary
}

export type MealEntriesByType = {
  mealType: string
  entries: MealEntryItem[]
  summary: NutritionSummary
}

export type ProfileDay = {
  date: string
  goal: DailyGoal | null
  meals: MealEntriesByType[]
  totalSummary: NutritionSummary
}

export type MealEditContext = {
  mealEntryId: string
  mealType: string
}

export type AppAction =
  | { type: 'SET_INPUT_TEXT'; text: string }
  | { type: 'ADD_ATTACHMENTS'; attachments: Attachment[] }
  | { type: 'REMOVE_ATTACHMENT'; id: string }
  | { type: 'CLEAR_ATTACHMENTS' }
  | { type: 'SET_RECORDING_STATE'; state: RecordingState }
  | { type: 'SET_LOADING'; loading: boolean }
  | { type: 'ADD_MESSAGE'; message: ChatMessage }
  | {
      type: 'RESOLVE_CLARIFICATION'
      messageId: string
      clarificationId: string
      product: ProductNutrition
    }
  | { type: 'CANCEL_CLARIFICATION'; messageId: string; clarificationId: string }
  | { type: 'START_MANUAL_CLARIFICATION'; messageId: string; clarificationId: string }
  | {
      type: 'REPLACE_CLARIFICATION_CANDIDATES'
      messageId: string
      clarificationId: string
      parsedProductName: string
      question: string
      candidates: ProductNutrition[]
    }
  | { type: 'SET_ACTIVE_CLARIFICATION'; messageId: string; index: number }
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
