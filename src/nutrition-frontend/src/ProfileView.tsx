import { useCallback, useEffect, useRef, useState } from 'react'
import type { KeyboardEvent, ReactNode } from 'react'
import {
  BarChart3,
  Apple,
  Check,
  ChevronRight,
  Crown,
  Coffee,
  Download,
  LogOut,
  MoonStar,
  Pencil,
  Plus,
  Shield,
  Sparkles,
  Soup,
  Target,
  Trash2,
  User,
  X,
} from 'lucide-react'
import aiCrystalUrl from './assets/ai-crystal.svg'
import { authApi, profileApi } from './profileApi'
import type { ProfileStatistics } from './profileApi'
import type { DailyGoal, MealEntryItem, ProfileData } from './types'
import './ProfileView.css'

type Props = {
  onBackToChat: () => void
  onUnauthorized: () => void
  onEditMeal: (entry: MealEntryItem) => void
}

type EditableField =
  | 'firstName'
  | 'secondName'
  | 'targetCalories'
  | 'targetProtein'
  | 'targetFat'
  | 'targetCarbs'
  | 'breakfastPercent'
  | 'lunchPercent'
  | 'dinnerPercent'
  | 'snackPercent'

type Toast = {
  id: number
  text: string
}

type SystemBusyAction = 'export' | 'logout' | 'delete'

const defaultGoal: DailyGoal = {
  targetCalories: 2100,
  targetProtein: 120,
  targetFat: 70,
  targetCarbs: 250,
  breakfastPercent: 25,
  lunchPercent: 35,
  dinnerPercent: 30,
  snackPercent: 10,
}

const todayIso = new Date().toISOString().slice(0, 10)

export function ProfileView({ onBackToChat, onUnauthorized }: Props) {
  const [profile, setProfile] = useState<ProfileData | null>(null)
  const [goal, setGoal] = useState<DailyGoal>(defaultGoal)
  const [statistics, setStatistics] = useState<ProfileStatistics | null>(null)
  const [statisticsLoading, setStatisticsLoading] = useState(false)
  const [selectedRange, setSelectedRange] = useState(7)
  const [editingField, setEditingField] = useState<EditableField | null>(null)
  const [draftValue, setDraftValue] = useState('')
  const [savingField, setSavingField] = useState<EditableField | null>(null)
  const [fieldError, setFieldError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [profileError, setProfileError] = useState<string | null>(null)
  const [goalsError, setGoalsError] = useState<string | null>(null)
  const [statsError, setStatsError] = useState<string | null>(null)
  const [toast, setToast] = useState<Toast | null>(null)
  const [unitsOpen, setUnitsOpen] = useState(false)
  const [confirmDeleteOpen, setConfirmDeleteOpen] = useState(false)
  const [systemBusy, setSystemBusy] = useState<SystemBusyAction | null>(null)

  const showToast = useCallback((text: string) => {
    const nextToast = { id: Date.now(), text }
    setToast(nextToast)
    window.setTimeout(() => {
      setToast((current) => (current?.id === nextToast.id ? null : current))
    }, 2800)
  }, [])

  const handleApiError = useCallback((error: unknown, fallback: string, setLocalError: (value: string | null) => void) => {
    if (error instanceof Error && error.message === 'unauthorized') {
      onUnauthorized()
      return
    }
    setLocalError(fallback)
  }, [onUnauthorized])

  const loadStatistics = useCallback(async (rangeDays: number, currentGoal: DailyGoal) => {
    setStatisticsLoading(true)
    setStatsError(null)
    try {
      const payload = await profileApi.getStatistics(rangeDays, todayIso)
      setStatistics({
        ...payload,
        dailyCaloriesTarget: currentGoal.targetCalories || payload.dailyCaloriesTarget,
      })
    } catch (error) {
      handleApiError(error, 'Не удалось загрузить статистику.', setStatsError)
    } finally {
      setStatisticsLoading(false)
    }
  }, [handleApiError])

  const loadProfilePage = useCallback(async () => {
    setLoading(true)
    setProfileError(null)
    setGoalsError(null)
    setStatsError(null)

    try {
      const [profilePayload, goalPayload] = await Promise.all([
        profileApi.getMe(),
        profileApi.getGoals(),
      ])
      const nextGoal = goalPayload ?? defaultGoal
      setProfile(profilePayload)
      setGoal(nextGoal)
      await loadStatistics(7, nextGoal)
    } catch (error) {
      handleApiError(error, 'Не удалось загрузить личный кабинет.', setProfileError)
    } finally {
      setLoading(false)
    }
  }, [handleApiError, loadStatistics])

  useEffect(() => {
    const timeoutId = window.setTimeout(() => void loadProfilePage(), 0)
    return () => window.clearTimeout(timeoutId)
  }, [loadProfilePage])

  const displayName = profile ? `${profile.firstName} ${profile.secondName}`.trim() : 'Профиль'

  function startEdit(field: EditableField, value: string | number | undefined) {
    setEditingField(field)
    setDraftValue(String(value ?? ''))
    setFieldError(null)
  }

  function cancelEdit() {
    setEditingField(null)
    setDraftValue('')
    setFieldError(null)
  }

  async function saveField(field: EditableField) {
    if (!profile) return

    const trimmed = draftValue.trim()
    if (!trimmed) {
      setFieldError('Поле не должно быть пустым.')
      return
    }

    setSavingField(field)
    setFieldError(null)

    try {
      if (field === 'firstName' || field === 'secondName') {
        const updated = await profileApi.updateMe({
          firstName: field === 'firstName' ? trimmed : profile.firstName,
          secondName: field === 'secondName' ? trimmed : profile.secondName,
        })
        setProfile(updated)
        showToast('Профиль обновлён')
      } else {
        const numericValue = Number(trimmed)
        if (!Number.isFinite(numericValue) || numericValue < 0 || (field === 'targetCalories' && numericValue <= 0)) {
          setFieldError(field === 'targetCalories' ? 'Калории должны быть больше нуля.' : 'Введите число не меньше нуля.')
          return
        }

        const nextGoal = normalizeGoal({
          ...goal,
          [field]: numericValue,
        })
        const saved = await profileApi.updateGoals(nextGoal)
        setGoal(saved)
        setStatistics((current) => current ? { ...current, dailyCaloriesTarget: saved.targetCalories } : current)
        showToast('Цели обновлены')
      }
      cancelEdit()
    } catch (error) {
      const isGoalField = field.startsWith('target') || field.endsWith('Percent')
      handleApiError(error, 'Не удалось сохранить изменения.', isGoalField ? setGoalsError : setProfileError)
      setFieldError('Не удалось сохранить изменения.')
    } finally {
      setSavingField(null)
    }
  }

  function handleEditKeyDown(event: KeyboardEvent<HTMLInputElement>, field: EditableField) {
    if (event.key === 'Enter') {
      event.preventDefault()
      void saveField(field)
    }
    if (event.key === 'Escape') {
      event.preventDefault()
      cancelEdit()
    }
  }

  async function handleRangeChange(rangeDays: number) {
    setSelectedRange(rangeDays)
    await loadStatistics(rangeDays, goal)
  }

  async function handleExportCsv() {
    setSystemBusy('export')
    try {
      showToast('Экспорт CSV начат')
      const blob = await profileApi.exportDailyCsv()
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = 'nutrimate-daily-export.csv'
      link.click()
      URL.revokeObjectURL(url)
      showToast('CSV скачан')
    } catch (error) {
      handleApiError(error, 'Не удалось экспортировать CSV.', setProfileError)
      showToast('Не удалось экспортировать CSV')
    } finally {
      setSystemBusy(null)
    }
  }

  async function handleLogout() {
    setSystemBusy('logout')
    try {
      await authApi.logout()
      onUnauthorized()
    } catch (error) {
      handleApiError(error, 'Не удалось выйти из аккаунта.', setProfileError)
      showToast('Не удалось выйти')
      setSystemBusy(null)
    }
  }

  async function handleDeleteRequest() {
    setSystemBusy('delete')
    try {
      await profileApi.requestAccountDeletion()
      setConfirmDeleteOpen(false)
      showToast('Запрос на удаление аккаунта создан')
    } catch (error) {
      handleApiError(error, 'Не удалось создать запрос на удаление.', setProfileError)
      showToast('Не удалось создать запрос')
    } finally {
      setSystemBusy(null)
    }
  }

  function handleSelectMetricUnits() {
    setUnitsOpen(false)
    showToast('Единицы измерения: метрические')
  }

  if (loading) {
    return <ProfileSkeleton />
  }

  return (
    <main className="profile-page">
      <section className="profile-main" aria-label="Личный кабинет">
        <header className="profile-topbar">
          <div className="profile-title-group">
            <span className="profile-title-icon" aria-hidden="true">
              <User size={30} strokeWidth={2.4} />
            </span>
            <div>
              <h1>Личный кабинет</h1>
              <p>Профиль, цели и статистика</p>
            </div>
          </div>
          <button type="button" className="new-chat-action" onClick={onBackToChat}>
            <Plus size={19} aria-hidden="true" />
            <span>Новый чат</span>
          </button>
        </header>

        {profileError && <TileError text={profileError} />}

        <div className="profile-dashboard">
          <ProfileCard
            profile={profile}
            displayName={displayName}
            editingField={editingField}
            draftValue={draftValue}
            savingField={savingField}
            fieldError={fieldError}
            onStartEdit={startEdit}
            onDraftChange={setDraftValue}
            onSave={saveField}
            onCancel={cancelEdit}
            onKeyDown={handleEditKeyDown}
          />
          <DailyGoalsCard
            goal={goal}
            statistics={statistics}
            error={goalsError}
            editingField={editingField}
            draftValue={draftValue}
            savingField={savingField}
            fieldError={fieldError}
            onStartEdit={startEdit}
            onDraftChange={setDraftValue}
            onSave={saveField}
            onCancel={cancelEdit}
            onKeyDown={handleEditKeyDown}
          />
          <NutritionDynamicsCard
            statistics={statistics}
            goal={goal}
            rangeDays={selectedRange}
            error={statsError}
            isLoading={statisticsLoading}
            onRangeChange={handleRangeChange}
          />
          <AiInsightsStubCard />
          <SystemCard
            unitsOpen={unitsOpen}
            onToggleUnits={() => setUnitsOpen((value) => !value)}
            onSelectMetricUnits={handleSelectMetricUnits}
            onExportCsv={handleExportCsv}
            onLogout={handleLogout}
            onOpenDelete={() => setConfirmDeleteOpen(true)}
            busyAction={systemBusy}
          />
        </div>
      </section>

      {toast && <div className="profile-toast" role="status">{toast.text}</div>}
      {confirmDeleteOpen && (
        <ConfirmDeleteAccountModal
          onCancel={() => setConfirmDeleteOpen(false)}
          onConfirm={handleDeleteRequest}
          isSubmitting={systemBusy === 'delete'}
        />
      )}
    </main>
  )
}

function ProfileCard(props: {
  profile: ProfileData | null
  displayName: string
  editingField: EditableField | null
  draftValue: string
  savingField: EditableField | null
  fieldError: string | null
  onStartEdit: (field: EditableField, value: string | number | undefined) => void
  onDraftChange: (value: string) => void
  onSave: (field: EditableField) => void
  onCancel: () => void
  onKeyDown: (event: KeyboardEvent<HTMLInputElement>, field: EditableField) => void
}) {
  return (
    <article className="profile-tile profile-card-tile">
      <TileHeader icon={<User size={22} />} title="Профиль" />
      <div className="profile-form-shell">
        <span className="profile-avatar-large">{getInitials(props.profile)}</span>
        <div className="profile-fields">
          <EditableProfileField label="Имя" field="firstName" value={props.profile?.firstName ?? ''} {...props} />
          <EditableProfileField label="Фамилия" field="secondName" value={props.profile?.secondName ?? ''} {...props} />
          <EditableProfileField label="Email" value={props.profile?.email ?? ''} readOnlyTitle="Изменение email пока недоступно" {...props} />
        </div>
      </div>
      <div className="subscription-strip">
        <span className="subscription-icon">
          <Crown size={24} fill="currentColor" />
        </span>
        <span>
          <strong>Тариф: Премиум</strong>
          <small>Доступен до 20.09.2026</small>
        </span>
        <button type="button" className="subscription-button">Управление</button>
      </div>
    </article>
  )
}

function DailyGoalsCard(props: {
  goal: DailyGoal
  statistics: ProfileStatistics | null
  error: string | null
  editingField: EditableField | null
  draftValue: string
  savingField: EditableField | null
  fieldError: string | null
  onStartEdit: (field: EditableField, value: string | number | undefined) => void
  onDraftChange: (value: string) => void
  onSave: (field: EditableField) => void
  onCancel: () => void
  onKeyDown: (event: KeyboardEvent<HTMLInputElement>, field: EditableField) => void
}) {
  const lastStatsDay = props.statistics?.items.find((item) => item.date === todayIso)
    ?? props.statistics?.items[(props.statistics?.items.length ?? 0) - 1]
  const mealCalories = {
    breakfast: lastStatsDay?.breakfastCalories ?? 0,
    lunch: lastStatsDay?.lunchCalories ?? 0,
    dinner: lastStatsDay?.dinnerCalories ?? 0,
    snack: lastStatsDay?.snackCalories ?? 0,
  }
  const mealTotal = mealCalories.breakfast + mealCalories.lunch + mealCalories.dinner + mealCalories.snack
  const meals = [
    { label: 'Завтрак', kcal: mealCalories.breakfast, iconClass: 'sun' },
    { label: 'Обед', kcal: mealCalories.lunch, iconClass: 'lunch' },
    { label: 'Ужин', kcal: mealCalories.dinner, iconClass: 'moon' },
    { label: 'Перекус', kcal: mealCalories.snack, iconClass: 'snack' },
  ].map((meal) => ({
    ...meal,
    percent: mealTotal > 0 ? Math.round((meal.kcal / mealTotal) * 1000) / 10 : 0,
  }))

  return (
    <article className="profile-tile goals-tile">
      <TileHeader icon={<Target size={22} />} title="Цели на день" />
      {props.error && <TileError text={props.error} />}
      <div className="goal-summary-panel">
        <EditableMetric label="Калории" suffix="ккал" field="targetCalories" value={props.goal.targetCalories} {...props} />
        <EditableMetric label="Белки" suffix="г" field="targetProtein" value={props.goal.targetProtein} tone="protein" {...props} />
        <EditableMetric label="Жиры" suffix="г" field="targetFat" value={props.goal.targetFat} tone="fat" {...props} />
        <EditableMetric label="Углеводы" suffix="г" field="targetCarbs" value={props.goal.targetCarbs} tone="carbs" {...props} />
      </div>
      <div className="meal-distribution">
        <h3>Распределение калорий за день</h3>
        {meals.map((meal) => (
          <MealGoalRow
            key={meal.label}
            label={meal.label}
            kcal={Math.round(meal.kcal)}
            percent={meal.percent}
            iconClass={meal.iconClass}
          />
        ))}
      </div>
    </article>
  )
}

function NutritionDynamicsCard({ statistics, goal, rangeDays, error, isLoading, onRangeChange }: {
  statistics: ProfileStatistics | null
  goal: DailyGoal
  rangeDays: number
  error: string | null
  isLoading: boolean
  onRangeChange: (rangeDays: number) => void
}) {
  return (
    <article className="profile-tile dynamics-tile">
      <div className="tile-header wide">
        <div className="tile-heading">
          <span className="tile-icon" aria-hidden="true">
            <BarChart3 size={22} />
          </span>
          <div>
            <h2>Статистика и динамика</h2>
            <p>Калории по макронутриентам (ккал)</p>
          </div>
        </div>
        <RangeSegmentedControl value={rangeDays} onChange={onRangeChange} />
      </div>
      {error && <TileError text={error} />}
      <div className={`statistics-visual ${isLoading ? 'is-loading' : ''}`} aria-busy={isLoading}>
      {statistics ? (
        <StackedMacroBarChart statistics={statistics} goal={goal} rangeDays={rangeDays} />
      ) : (
        <div className="empty-chart">Нет данных для графика.</div>
      )}
      <div className="macro-legend">
        <span><i className="protein" />Белки (г)</span>
        <span><i className="fat" />Жиры (г)</span>
        <span><i className="carbs" />Углеводы (г)</span>
      </div>
      {isLoading && <span className="statistics-loader" aria-label="Загрузка статистики" />}
      </div>
    </article>
  )
}

function StackedMacroBarChart({ statistics, goal, rangeDays }: { statistics: ProfileStatistics; goal: DailyGoal; rangeDays: number }) {
  const rawMaxValue = Math.max(goal.targetCalories, ...statistics.items.map((item) => item.totalCalories), 1)
  const maxValue = Math.max(500, Math.ceil((rawMaxValue * 1.18) / 250) * 250)
  const chartHeight = 220
  const plotTop = 14
  const plotBottom = 190
  const plotHeight = plotBottom - plotTop
  const goalY = plotBottom - (goal.targetCalories / maxValue) * plotHeight
  const barStep = 704 / Math.max(statistics.items.length, 1)
  const barWidth = rangeDays === 7 ? 42 : rangeDays === 14 ? 22 : 10
  const showLabels = rangeDays === 7

  return (
    <div className="chart-wrap" aria-label="График калорий по макронутриентам">
      <svg viewBox={`0 0 780 ${chartHeight}`} role="img">
        {[0, 0.25, 0.5, 0.75, 1].map((ratio) => {
          const tickValue = maxValue * ratio
          const tickY = plotBottom - ratio * plotHeight
          return (
          <g key={ratio}>
            <line x1="54" x2="748" y1={tickY} y2={tickY} className="axis-line" />
            <text x="4" y={tickY + 4} className="axis-label">{formatNumber(tickValue)}</text>
          </g>
          )
        })}
        <line x1="54" y1={goalY} x2="748" y2={goalY} className="goal-line" />
        <text x="646" y={Math.max(16, goalY - 7)} className="goal-label">Цель {formatNumber(goal.targetCalories)} ккал</text>
        {statistics.items.map((item, index) => {
          const x = 68 + index * barStep + (barStep - barWidth) / 2
          const date = new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'short' }).format(new Date(`${item.date}T00:00:00`))
          const labelVisible = rangeDays !== 30 || index % 4 === 0

          if (!item.hasData || item.totalCalories <= 0) {
            const emptyHeight = showLabels ? 58 : 34
            return (
              <g key={item.date}>
                <title>{date}: нет данных</title>
                <rect x={x} y={plotBottom - emptyHeight} width={barWidth} height={emptyHeight} rx="7" className="empty-bar" />
                {showLabels && <text x={x + barWidth / 2} y={plotBottom - emptyHeight / 2 - 2} className="bar-empty-label">Нет</text>}
                {showLabels && <text x={x + barWidth / 2} y={plotBottom - emptyHeight / 2 + 11} className="bar-empty-label">данных</text>}
                {labelVisible && <text x={x + barWidth / 2} y="210" className="x-label">{shortDate(item.date, rangeDays)}</text>}
              </g>
            )
          }

          const totalHeight = Math.max(4, (item.totalCalories / maxValue) * plotHeight)
          const barTop = plotBottom - totalHeight
          const macroKcal = {
            protein: item.proteinGrams * 4,
            fat: item.fatGrams * 9,
            carbs: item.carbsGrams * 4,
          }
          const macroTotal = Math.max(macroKcal.protein + macroKcal.fat + macroKcal.carbs, 1)
          const proteinHeight = Math.max(macroKcal.protein > 0 ? 2 : 0, totalHeight * macroKcal.protein / macroTotal)
          const fatHeight = Math.max(macroKcal.fat > 0 ? 2 : 0, totalHeight * macroKcal.fat / macroTotal)
          const carbsHeight = Math.max(0, totalHeight - proteinHeight - fatHeight)
          const proteinPct = Math.round(macroKcal.protein / macroTotal * 100)
          const fatPct = Math.round(macroKcal.fat / macroTotal * 100)
          const carbsPct = Math.max(0, 100 - proteinPct - fatPct)
          const goalPercent = Math.round(item.totalCalories / Math.max(goal.targetCalories, 1) * 100)
          const clipId = `macro-bar-${rangeDays}-${index}`
          const radius = Math.min(7, barWidth / 2, totalHeight)
          const barPath = [
            `M ${x} ${plotBottom}`,
            `L ${x} ${barTop + radius}`,
            `Q ${x} ${barTop} ${x + radius} ${barTop}`,
            `L ${x + barWidth - radius} ${barTop}`,
            `Q ${x + barWidth} ${barTop} ${x + barWidth} ${barTop + radius}`,
            `L ${x + barWidth} ${plotBottom}`,
            'Z',
          ].join(' ')
          const title = `${date}
${formatNumber(item.totalCalories)} ккал
Белки: ${formatNumber(item.proteinGrams)} г / ${formatNumber(macroKcal.protein)} ккал
Жиры: ${formatNumber(item.fatGrams)} г / ${formatNumber(macroKcal.fat)} ккал
Углеводы: ${formatNumber(item.carbsGrams)} г / ${formatNumber(macroKcal.carbs)} ккал
${goalPercent}% от цели`

          return (
            <g key={item.date}>
              <title>{title}</title>
              <defs>
                <clipPath id={clipId}>
                  <path d={barPath} />
                </clipPath>
              </defs>
              {showLabels && <text x={x + barWidth / 2} y={Math.max(13, barTop - 7)} className="bar-total">{formatNumber(item.totalCalories)}</text>}
              <path d={barPath} className="bar-shell" />
              <g clipPath={`url(#${clipId})`}>
                <rect x={x} y={plotBottom - carbsHeight} width={barWidth} height={carbsHeight} className="bar-carbs" />
                <rect x={x} y={plotBottom - carbsHeight - fatHeight} width={barWidth} height={fatHeight} className="bar-fat" />
                <rect x={x} y={barTop} width={barWidth} height={proteinHeight} className="bar-protein" />
              </g>
              {showLabels && proteinHeight > 22 && <text x={x + barWidth / 2} y={barTop + proteinHeight / 2 + 4} className="bar-percent">{proteinPct}%</text>}
              {showLabels && fatHeight > 22 && <text x={x + barWidth / 2} y={plotBottom - carbsHeight - fatHeight / 2 + 4} className="bar-percent">{fatPct}%</text>}
              {showLabels && carbsHeight > 22 && <text x={x + barWidth / 2} y={plotBottom - carbsHeight / 2 + 4} className="bar-percent">{carbsPct}%</text>}
              {labelVisible && <text x={x + barWidth / 2} y="210" className="x-label">{shortDate(item.date, rangeDays)}</text>}
            </g>
          )
        })}
      </svg>
    </div>
  )
}

function SystemCard({ unitsOpen, onToggleUnits, onSelectMetricUnits, onExportCsv, onLogout, onOpenDelete, busyAction }: {
  unitsOpen: boolean
  onToggleUnits: () => void
  onSelectMetricUnits: () => void
  onExportCsv: () => void
  onLogout: () => void
  onOpenDelete: () => void
  busyAction: SystemBusyAction | null
}) {
  return (
    <article className="profile-tile system-tile">
      <TileHeader icon={<Shield size={22} />} title="Система" />
      <div className="system-row with-popover">
        <span>
          <strong>Единицы измерения</strong>
          <small>Метрические (кг, см)</small>
        </span>
        <button type="button" className="icon-action" aria-label="Изменить единицы измерения" onClick={onToggleUnits}>
          <Pencil size={17} />
        </button>
        {unitsOpen && (
          <div className="units-popover">
            <button type="button" onClick={onSelectMetricUnits}>Метрические (кг, см)</button>
            <button type="button" disabled title="Будет позже">Имперские (lb, ft)</button>
          </div>
        )}
      </div>
      <SystemActionRow icon={<Download size={18} />} label="Экспорт CSV" onClick={onExportCsv} isLoading={busyAction === 'export'} />
      <SystemActionRow icon={<LogOut size={18} />} label="Разлогиниться" onClick={onLogout} isLoading={busyAction === 'logout'} />
      <SystemActionRow icon={<Trash2 size={18} />} label="Удаление аккаунта" onClick={onOpenDelete} destructive />
    </article>
  )
}

function AiInsightsStubCard() {
  return (
    <article className="profile-tile insights-tile">
      <div className="tile-header">
        <div className="tile-heading">
          <span className="tile-icon" aria-hidden="true">
            <Sparkles size={21} />
          </span>
          <h2>AI-инсайты и рекомендации</h2>
        </div>
        <span className="soon-badge">скоро</span>
      </div>
      <img className="ai-crystal" src={aiCrystalUrl} alt="" />
      <p>Мы анализируем ваши привычки питания и готовим персональные рекомендации.</p>
      <small className="insight-preview-label">Примеры рекомендаций</small>
      <ul>
        <li><Sparkles size={15} />Вы часто недобираете белок в обед.</li>
        <li><Sparkles size={15} />Попробуйте добавить больше белка в завтрак.</li>
        <li><Sparkles size={15} />Ваш ужин часто слишком калорийный.</li>
      </ul>
    </article>
  )
}

function EditableProfileField({ label, field, value, readOnlyTitle, editingField, draftValue, savingField, fieldError, onStartEdit, onDraftChange, onSave, onCancel, onKeyDown }: {
  label: string
  field?: EditableField
  value: string
  readOnlyTitle?: string
  editingField: EditableField | null
  draftValue: string
  savingField: EditableField | null
  fieldError: string | null
  onStartEdit: (field: EditableField, value: string | number | undefined) => void
  onDraftChange: (value: string) => void
  onSave: (field: EditableField) => void
  onCancel: () => void
  onKeyDown: (event: KeyboardEvent<HTMLInputElement>, field: EditableField) => void
}) {
  const isReadOnly = !field
  const isEditing = field ? editingField === field : false
  return (
    <label className={`editable-profile-field ${isReadOnly ? 'is-readonly' : ''}`} title={readOnlyTitle}>
      {isEditing ? (
        <span className="profile-field-shell is-editing">
          <span className="profile-field-copy">
            <span className="profile-field-label">{label}</span>
          </span>
          <input autoFocus value={draftValue} onChange={(event) => onDraftChange(event.target.value)} onKeyDown={(event) => onKeyDown(event, field!)} />
          <span className="profile-field-actions">
            <button type="button" aria-label="Сохранить" disabled={savingField === field} onClick={() => onSave(field!)}>
              <Check size={15} />
            </button>
            <button type="button" aria-label="Отмена" onClick={onCancel}>
              <X size={15} />
            </button>
          </span>
        </span>
      ) : (
        <span className="profile-field-shell">
          <span className="profile-field-copy">
            <span className="profile-field-label">{label}</span>
            <strong className="profile-field-value">{value}</strong>
          </span>
          {field && (
            <button type="button" className="profile-field-edit" aria-label={`Редактировать ${label}`} onClick={() => onStartEdit(field, value)}>
              <Pencil size={16} />
            </button>
          )}
        </span>
      )}
      {field && isEditing && fieldError && <small className="profile-field-error">{fieldError}</small>}
    </label>
  )
}

function EditableMetric(props: {
  label: string
  helper?: string
  suffix: string
  field: EditableField
  value: number
  tone?: 'protein' | 'fat' | 'carbs'
  editingField: EditableField | null
  draftValue: string
  savingField: EditableField | null
  fieldError: string | null
  onStartEdit: (field: EditableField, value: string | number | undefined) => void
  onDraftChange: (value: string) => void
  onSave: (field: EditableField) => void
  onCancel: () => void
  onKeyDown: (event: KeyboardEvent<HTMLInputElement>, field: EditableField) => void
}) {
  const isEditing = props.editingField === props.field
  return (
    <label className={`goal-metric ${props.tone ?? ''}`}>
      <span>{props.label}</span>
      {props.helper && <small>{props.helper}</small>}
      {isEditing ? (
        <span className="metric-edit">
          <input autoFocus type="number" min="0" value={props.draftValue} onChange={(event) => props.onDraftChange(event.target.value)} onKeyDown={(event) => props.onKeyDown(event, props.field)} />
          <button type="button" aria-label="Сохранить" disabled={props.savingField === props.field} onClick={() => props.onSave(props.field)}>✓</button>
          <button type="button" aria-label="Отмена" onClick={props.onCancel}>×</button>
          {props.fieldError && <small>{props.fieldError}</small>}
        </span>
      ) : (
        <span className="metric-value">
          <strong>{formatNumber(props.value)}</strong>
          <em>{props.suffix}</em>
          <button type="button" aria-label={`Редактировать ${props.label}`} onClick={() => props.onStartEdit(props.field, props.value)}>
            <Pencil size={17} />
          </button>
        </span>
      )}
    </label>
  )
}

function MealGoalRow(props: {
  label: string
  kcal: number
  percent: number
  iconClass: string
}) {
  return (
    <div className="meal-goal-row">
      <span className={`meal-square ${props.iconClass}`} aria-hidden="true">
        {props.iconClass === 'sun' ? <Coffee size={18} /> : props.iconClass === 'lunch' ? <Soup size={18} /> : props.iconClass === 'moon' ? <MoonStar size={18} /> : <Apple size={18} />}
      </span>
      <strong>{props.label}</strong>
      <span className="meal-bar"><i style={{ width: `${Math.min(100, props.percent)}%` }} /></span>
      <span>{props.kcal} ккал</span>
      <small>{formatNumber(props.percent)}%</small>
    </div>
  )
}

function RangeSegmentedControl({ value, onChange }: { value: number; onChange: (value: number) => void }) {
  return (
    <div className="range-control" role="group" aria-label="Период статистики">
      {[7, 14, 30].map((item) => (
        <button key={item} type="button" className={value === item ? 'active' : ''} onClick={() => onChange(item)}>
          {item} дней
        </button>
      ))}
    </div>
  )
}

function SystemActionRow({ icon, label, onClick, destructive = false, isLoading = false }: {
  icon: ReactNode
  label: string
  onClick: () => void
  destructive?: boolean
  isLoading?: boolean
}) {
  return (
    <button type="button" className={`system-action ${destructive ? 'destructive' : ''}`} onClick={onClick} disabled={isLoading}>
      <span aria-hidden="true">{icon}</span>
      <strong>{isLoading ? 'Выполняется...' : label}</strong>
      <ChevronRight size={16} aria-hidden="true" />
    </button>
  )
}

function ConfirmDeleteAccountModal({ onCancel, onConfirm, isSubmitting }: { onCancel: () => void; onConfirm: () => void; isSubmitting: boolean }) {
  const cancelRef = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    cancelRef.current?.focus()
    const handleKeyDown = (event: globalThis.KeyboardEvent) => {
      if (event.key === 'Escape') onCancel()
    }
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [onCancel])

  return (
    <div className="modal-backdrop" role="presentation">
      <section className="confirm-modal" role="dialog" aria-modal="true" aria-labelledby="delete-title">
        <h2 id="delete-title">Запросить удаление аккаунта?</h2>
        <p>Мы создадим запрос на удаление аккаунта. Это действие чувствительное.</p>
        <div>
          <button type="button" className="secondary-action" ref={cancelRef} onClick={onCancel} disabled={isSubmitting}>Отмена</button>
          <button type="button" className="danger-action" onClick={onConfirm} disabled={isSubmitting}>{isSubmitting ? 'Отправляем...' : 'Запросить удаление'}</button>
        </div>
      </section>
    </div>
  )
}

function TileHeader({ icon, title }: { icon: ReactNode; title: string }) {
  return (
    <div className="tile-header">
      <div className="tile-heading">
        <span className="tile-icon" aria-hidden="true">{icon}</span>
        <h2>{title}</h2>
      </div>
    </div>
  )
}

function TileError({ text }: { text: string }) {
  return <div className="tile-error" role="alert">{text}</div>
}

function ProfileSkeleton() {
  return (
    <main className="profile-page skeleton-page">
      <section className="profile-main">
        <div className="profile-topbar skeleton-line" />
        <div className="profile-dashboard">
          {Array.from({ length: 5 }).map((_, index) => <div key={index} className="profile-tile skeleton-card" />)}
        </div>
      </section>
    </main>
  )
}

function normalizeGoal(goal: DailyGoal): DailyGoal {
  const mealFields = ['breakfastPercent', 'lunchPercent', 'dinnerPercent', 'snackPercent'] as const
  const total = mealFields.reduce((sum, field) => sum + (goal[field] ?? 0), 0)
  if (total !== 100 && total > 0) {
    return mealFields.reduce<DailyGoal>((nextGoal, field) => ({
      ...nextGoal,
      [field]: Math.round(((goal[field] ?? 0) / total) * 1000) / 10,
    }), goal)
  }
  return goal
}

function getInitials(profile: ProfileData | null): string {
  const first = profile?.firstName?.[0] ?? 'D'
  const second = profile?.secondName?.[0] ?? 'M'
  return `${first}${second}`.toUpperCase()
}

function formatNumber(value: number): string {
  return Number.isInteger(value) ? value.toString() : value.toFixed(1)
}

function shortDate(value: string, rangeDays: number): string {
  const date = new Date(`${value}T00:00:00`)
  return new Intl.DateTimeFormat('ru-RU', rangeDays === 7 ? { day: 'numeric', weekday: 'short' } : { day: 'numeric', month: 'numeric' }).format(date)
}
