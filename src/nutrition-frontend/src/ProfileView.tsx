import { useCallback, useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import type {
  DailyGoal,
  MealEntryItem,
  NutritionSummary,
  ProfileData,
  ProfileHistory,
  ProfileSummaryByTypeResponse,
} from './types'
import './ProfileView.css'

type Props = {
  onBackToChat: () => void
  onUnauthorized: () => void
  onEditMeal: (entry: MealEntryItem) => void
}

type GoalForm = {
  targetCalories: string
  targetProtein: string
  targetFat: string
  targetCarbs: string
}

const emptySummary: NutritionSummary = { calories: 0, protein: 0, fat: 0, carbs: 0 }

export function ProfileView({ onBackToChat, onUnauthorized, onEditMeal }: Props) {
  const [profile, setProfile] = useState<ProfileData | null>(null)
  const [history, setHistory] = useState<ProfileHistory>({ entries: [], totalSummary: emptySummary })
  const [summary, setSummary] = useState<NutritionSummary>(emptySummary)
  const [summaryByType, setSummaryByType] = useState<ProfileSummaryByTypeResponse>({
    summaryByType: [],
    totalSummary: emptySummary,
  })
  const [goal, setGoal] = useState<DailyGoal | null>(null)
  const [goalForm, setGoalForm] = useState<GoalForm>({
    targetCalories: '',
    targetProtein: '',
    targetFat: '',
    targetCarbs: '',
  })
  const [loading, setLoading] = useState(true)
  const [savingGoal, setSavingGoal] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const loadProfile = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const [profileResponse, historyResponse, summaryResponse, byTypeResponse, goalResponse] = await Promise.all([
        fetchProfile('/api/v1/profile/me'),
        fetchProfile('/api/v1/profile/history'),
        fetchProfile('/api/v1/profile/summary'),
        fetchProfile('/api/v1/profile/summary-by-type'),
        fetch('/api/v1/profile/goal', { credentials: 'include' }),
      ])

      setProfile((await profileResponse.json()) as ProfileData)
      setHistory((await historyResponse.json()) as ProfileHistory)
      setSummary((await summaryResponse.json()) as NutritionSummary)
      setSummaryByType((await byTypeResponse.json()) as ProfileSummaryByTypeResponse)

      if (goalResponse.status === 401) {
        onUnauthorized()
        return
      }

      if (goalResponse.status === 204) {
        setGoal(null)
        setGoalForm(toGoalForm(null))
      } else if (goalResponse.ok) {
        const payload = (await goalResponse.json()) as DailyGoal | null
        setGoal(payload)
        setGoalForm(toGoalForm(payload))
      }
    } catch (err) {
      if (err instanceof Error && err.message === 'Сессия истекла.') {
        onUnauthorized()
        return
      }
      setError(err instanceof Error ? err.message : 'Не удалось загрузить профиль.')
    } finally {
      setLoading(false)
    }
  }, [onUnauthorized])

  useEffect(() => {
    loadProfile()
  }, [loadProfile])

  const remaining = useMemo(() => {
    if (!goal) return null
    return {
      calories: goal.targetCalories - summary.calories,
      protein: goal.targetProtein - summary.protein,
      fat: goal.targetFat - summary.fat,
      carbs: goal.targetCarbs - summary.carbs,
    }
  }, [goal, summary])

  async function handleGoalSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSavingGoal(true)
    setError(null)

    const payload: DailyGoal = {
      targetCalories: toNumber(goalForm.targetCalories),
      targetProtein: toNumber(goalForm.targetProtein),
      targetFat: toNumber(goalForm.targetFat),
      targetCarbs: toNumber(goalForm.targetCarbs),
    }

    try {
      const response = await fetch('/api/v1/profile/goal', {
        method: goal ? 'PUT' : 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      })

      if (response.status === 401) {
        onUnauthorized()
        return
      }

      if (!response.ok) {
        throw new Error(`Ошибка сохранения цели: ${response.status}`)
      }

      const saved = (await response.json()) as DailyGoal
      setGoal(saved)
      setGoalForm(toGoalForm(saved))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Не удалось сохранить цель.')
    } finally {
      setSavingGoal(false)
    }
  }

  async function handleDeleteEntry(entryId: string) {
    const response = await fetch(`/api/v1/profile/entry/${entryId}`, {
      method: 'DELETE',
      credentials: 'include',
    })

    if (response.status === 401) {
      onUnauthorized()
      return
    }

    if (response.ok) {
      await loadProfile()
    }
  }

  if (loading) {
    return <main className="profile-view profile-centered">Загрузка профиля...</main>
  }

  return (
    <main className="profile-view">
      <header className="profile-header">
        <button type="button" className="secondary-btn" onClick={onBackToChat}>
          К чатам
        </button>
        <div>
          <strong>{profile ? `${profile.firstName} ${profile.secondName}` : 'Профиль'}</strong>
          <span>{profile?.email}</span>
        </div>
      </header>

      {error && <div className="profile-error">{error}</div>}

      <section className="summary-strip" aria-label="Сводка КБЖУ">
        <Macro label="Калории" value={summary.calories} suffix="ккал" />
        <Macro label="Белки" value={summary.protein} suffix="г" />
        <Macro label="Жиры" value={summary.fat} suffix="г" />
        <Macro label="Углеводы" value={summary.carbs} suffix="г" />
      </section>

      {remaining && (
        <section className="summary-strip muted" aria-label="Остаток до цели">
          <Macro label="Осталось ккал" value={remaining.calories} suffix="ккал" />
          <Macro label="Осталось белков" value={remaining.protein} suffix="г" />
          <Macro label="Осталось жиров" value={remaining.fat} suffix="г" />
          <Macro label="Осталось углеводов" value={remaining.carbs} suffix="г" />
        </section>
      )}

      <section className="profile-section">
        <h2>Цель на день</h2>
        <form className="goal-form" onSubmit={handleGoalSubmit}>
          <NumberInput label="Калории" value={goalForm.targetCalories} onChange={(value) => setGoalForm((form) => ({ ...form, targetCalories: value }))} />
          <NumberInput label="Белки" value={goalForm.targetProtein} onChange={(value) => setGoalForm((form) => ({ ...form, targetProtein: value }))} />
          <NumberInput label="Жиры" value={goalForm.targetFat} onChange={(value) => setGoalForm((form) => ({ ...form, targetFat: value }))} />
          <NumberInput label="Углеводы" value={goalForm.targetCarbs} onChange={(value) => setGoalForm((form) => ({ ...form, targetCarbs: value }))} />
          <button type="submit" disabled={savingGoal}>
            {savingGoal ? 'Сохраняю...' : 'Сохранить цель'}
          </button>
        </form>
      </section>

      <section className="profile-section">
        <h2>По типам приёма</h2>
        <div className="type-summary-list">
          {summaryByType.summaryByType.length === 0 ? (
            <span className="empty-note">Пока нет записей.</span>
          ) : (
            summaryByType.summaryByType.map((item) => (
              <div key={item.mealType} className="type-summary-row">
                <strong>{formatMealType(item.mealType)}</strong>
                <span>{item.count} записей</span>
                <span>{formatNumber(item.calories)} ккал</span>
                <span>Б {formatNumber(item.protein)} / Ж {formatNumber(item.fat)} / У {formatNumber(item.carbs)}</span>
              </div>
            ))
          )}
        </div>
      </section>

      <section className="profile-section">
        <h2>История</h2>
        <div className="history-list">
          {history.entries.length === 0 ? (
            <span className="empty-note">Добавьте продукт из чата, и он появится здесь.</span>
          ) : (
            history.entries.map((entry) => (
              <article key={entry.id} className="history-row">
                <div>
                  <strong>{entry.productName}</strong>
                  <span>{entry.brand || 'Бренд не указан'} · {formatMealType(entry.mealType)} · {formatDate(entry.createdAtUtc)}</span>
                </div>
                <span className="history-macros">{formatNumber(entry.calories)} / {formatNumber(entry.protein)} / {formatNumber(entry.fat)} / {formatNumber(entry.carbs)}</span>
                <div className="history-actions">
                  <button type="button" onClick={() => onEditMeal(entry)}>Изменить</button>
                  <button type="button" className="danger-btn" onClick={() => handleDeleteEntry(entry.id)}>Удалить</button>
                </div>
              </article>
            ))
          )}
        </div>
      </section>
    </main>
  )
}

async function fetchProfile(url: string): Promise<Response> {
  const response = await fetch(url, { credentials: 'include' })
  if (response.status === 401) {
    throw new Error('Сессия истекла.')
  }
  if (!response.ok) {
    throw new Error(`Ошибка загрузки: ${response.status}`)
  }
  return response
}

function NumberInput({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
  return (
    <label>
      <span>{label}</span>
      <input type="number" min="0" step="0.1" value={value} onChange={(event) => onChange(event.target.value)} />
    </label>
  )
}

function Macro({ label, value, suffix }: { label: string; value: number; suffix: string }) {
  return (
    <div className="summary-cell">
      <span>{label}</span>
      <strong>{formatNumber(value)} {suffix}</strong>
    </div>
  )
}

function toGoalForm(goal: DailyGoal | null): GoalForm {
  return {
    targetCalories: goal?.targetCalories?.toString() ?? '',
    targetProtein: goal?.targetProtein?.toString() ?? '',
    targetFat: goal?.targetFat?.toString() ?? '',
    targetCarbs: goal?.targetCarbs?.toString() ?? '',
  }
}

function toNumber(value: string): number {
  return Number.isFinite(Number(value)) ? Number(value) : 0
}

function formatNumber(value: number): string {
  return Number.isInteger(value) ? value.toString() : value.toFixed(1)
}

function formatDate(value: string): string {
  return new Intl.DateTimeFormat('ru-RU', {
    day: '2-digit',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value))
}

function formatMealType(value: string): string {
  const map: Record<string, string> = {
    Breakfast: 'Завтрак',
    Lunch: 'Обед',
    Dinner: 'Ужин',
    Snack: 'Перекус',
  }
  return map[value] ?? value
}
