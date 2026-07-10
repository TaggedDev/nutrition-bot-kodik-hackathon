import type { CurrentUser, DailyGoal, MealEntryItem, ProfileData } from './types'

export type ProfileUpdate = {
  firstName: string
  secondName: string
}

export type ProfileStatisticsDay = {
  date: string
  totalCalories: number
  proteinGrams: number
  fatGrams: number
  carbsGrams: number
  breakfastCalories: number
  lunchCalories: number
  dinnerCalories: number
  snackCalories: number
  hasData: boolean
}

export type ProfileStatistics = {
  dailyCaloriesTarget: number
  items: ProfileStatisticsDay[]
}

export type ChatHistoryItemData = {
  id: string
  title: string
  preview: string
  dateLabel: string
  timeLabel: string
}

async function request<T>(url: string, options: RequestInit = {}): Promise<T> {
  const response = await fetch(url, {
    ...options,
    credentials: 'include',
    headers: {
      ...(options.body ? { 'Content-Type': 'application/json' } : {}),
      ...options.headers,
    },
  })

  if (response.status === 401) {
    throw new Error('unauthorized')
  }

  if (!response.ok) {
    throw new Error(`request_failed:${response.status}`)
  }

  return (await response.json()) as T
}

export const profileApi = {
  getMe: () => request<ProfileData>('/api/v1/profile/me'),
  updateMe: (payload: ProfileUpdate) =>
    request<ProfileData>('/api/v1/profile/me', {
      method: 'PATCH',
      body: JSON.stringify(payload),
    }),
  async getGoals() {
    const response = await fetch('/api/v1/profile/goals', { credentials: 'include' })
    if (response.status === 401) throw new Error('unauthorized')
    if (response.status === 204) return null
    if (!response.ok) throw new Error(`request_failed:${response.status}`)
    return (await response.json()) as DailyGoal
  },
  updateGoals: (payload: DailyGoal) =>
    request<DailyGoal>('/api/v1/profile/goals', {
      method: 'PATCH',
      body: JSON.stringify(payload),
    }),
  getStatistics: (rangeDays: number, endDate: string) =>
    request<ProfileStatistics>(`/api/v1/profile/statistics?rangeDays=${rangeDays}&endDate=${endDate}`),
  async exportDailyCsv() {
    const response = await fetch('/api/v1/profile/export/daily-csv', { credentials: 'include' })
    if (response.status === 401) throw new Error('unauthorized')
    if (!response.ok) throw new Error(`request_failed:${response.status}`)
    return response.blob()
  },
  requestAccountDeletion: () =>
    request<{ accepted: boolean; requestedAtUtc: string }>('/api/v1/profile/delete-request', {
      method: 'POST',
    }),
}

export const authApi = {
  logout: async () => {
    const response = await fetch('/api/v1/auth/logout', {
      method: 'POST',
      credentials: 'include',
    })
    if (response.status === 401) throw new Error('unauthorized')
    if (!response.ok) throw new Error(`request_failed:${response.status}`)
  },
}

export const chatApi = {
  getHistory: async (): Promise<ChatHistoryItemData[]> => [
    {
      id: 'weekly-plan',
      title: 'План питания на неделю',
      preview: 'Подбери план питания на неделю...',
      dateLabel: 'Сегодня',
      timeLabel: '10:45',
    },
    {
      id: 'ration-analysis',
      title: 'Анализ рациона',
      preview: 'Проанализируй мой рацион за...',
      dateLabel: 'Вчера',
      timeLabel: 'Вчера, 18:30',
    },
    {
      id: 'protein-ration',
      title: 'Белок в рационе',
      preview: 'Сколько белка я получаю в день?',
      dateLabel: 'Вчера',
      timeLabel: 'Вчера, 09:15',
    },
    {
      id: 'snack-selection',
      title: 'Подбор перекусов',
      preview: 'Какие перекусы лучше выбрать...',
      dateLabel: '23 мая',
      timeLabel: '23 мая, 20:10',
    },
    {
      id: 'training-food',
      title: 'Тренировки и питание',
      preview: 'Как питание влияет на мои...',
      dateLabel: '23 мая',
      timeLabel: '23 мая, 14:22',
    },
    {
      id: 'calorie-deficit',
      title: 'Дефицит калорий',
      preview: 'Создай план для дефицита калорий',
      dateLabel: '21 мая',
      timeLabel: '21 мая, 11:05',
    },
    {
      id: 'water-hydration',
      title: 'Вода и гидратация',
      preview: 'Сколько воды мне нужно пить?',
      dateLabel: '21 мая',
      timeLabel: '21 мая, 08:40',
    },
  ],
}

export type ProfileBootstrap = {
  user: CurrentUser
  entries: MealEntryItem[]
}
