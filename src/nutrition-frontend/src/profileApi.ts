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
      id: 'breakfast-balance',
      title: 'Баланс завтрака',
      preview: 'Овсянка, йогурт и ягоды',
      dateLabel: 'Сегодня',
      timeLabel: '09:24',
    },
    {
      id: 'protein-lunch',
      title: 'Белок в обед',
      preview: 'Курица, гречка, овощи',
      dateLabel: 'Сегодня',
      timeLabel: '13:12',
    },
    {
      id: 'dinner-check',
      title: 'Проверка ужина',
      preview: 'Сравнили пасту и салат',
      dateLabel: 'Вчера',
      timeLabel: '20:18',
    },
    {
      id: 'weekly-goals',
      title: 'Цели на неделю',
      preview: 'Обновили калории и БЖУ',
      dateLabel: '23 мая',
      timeLabel: '18:40',
    },
    {
      id: 'snack-ideas',
      title: 'Идеи перекусов',
      preview: 'Творог, орехи, фрукты',
      dateLabel: '21 мая',
      timeLabel: '11:05',
    },
  ],
}

export type ProfileBootstrap = {
  user: CurrentUser
  entries: MealEntryItem[]
}
