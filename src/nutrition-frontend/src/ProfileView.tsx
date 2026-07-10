import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import type { KeyboardEvent } from 'react'
import { authApi, chatApi, profileApi } from './profileApi'
import type { ChatHistoryItemData, ProfileStatistics } from './profileApi'
import type { DailyGoal, MealEntryItem, ProfileData } from './types'
import './ProfileView.css'

type Props = {
  onBackToChat: () => void
  onUnauthorized: () => void
  onEditMeal: (entry: MealEntryItem) => void
}

type EditableField = 'firstName' | 'secondName' | 'targetCalories' | 'targetProtein' | 'targetFat' | 'targetCarbs' | 'breakfastPercent' | 'lunchPercent' | 'dinnerPercent' | 'snackPercent'

type Toast = {
  id: number
  text: string
}

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
  const [chatHistory, setChatHistory] = useState<ChatHistoryItemData[]>([])
  const [selectedRange, setSelectedRange] = useState(7)
  const [chatQuery, setChatQuery] = useState('')
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
    setStatsError(null)
    try {
      const payload = await profileApi.getStatistics(rangeDays, todayIso)
      setStatistics({
        ...payload,
        dailyCaloriesTarget: currentGoal.targetCalories || payload.dailyCaloriesTarget,
      })
    } catch (error) {
      handleApiError(error, 'Не удалось загрузить статистику.', setStatsError)
    }
  }, [handleApiError])

  const loadProfilePage = useCallback(async () => {
    setLoading(true)
    setProfileError(null)
    setGoalsError(null)
    setStatsError(null)

    try {
      const [profilePayload, goalPayload, chatsPayload] = await Promise.all([
        profileApi.getMe(),
        profileApi.getGoals(),
        chatApi.getHistory(),
      ])
      const nextGoal = goalPayload ?? defaultGoal
      setProfile(profilePayload)
      setGoal(nextGoal)
      setChatHistory(chatsPayload)
      await loadStatistics(7, nextGoal)
    } catch (error) {
      handleApiError(error, 'Не удалось загрузить личный кабинет.', setProfileError)
    } finally {
      setLoading(false)
    }
  }, [handleApiError, loadStatistics])

  useEffect(() => {
    loadProfilePage()
  }, [loadProfilePage])

  const filteredChats = useMemo(() => {
    const query = chatQuery.trim().toLowerCase()
    if (!query) return chatHistory
    return chatHistory.filter((item) => `${item.title} ${item.preview}`.toLowerCase().includes(query))
  }, [chatHistory, chatQuery])

  const chatGroups = useMemo(() => {
    return filteredChats.reduce<Record<string, ChatHistoryItemData[]>>((groups, item) => {
      groups[item.dateLabel] = [...(groups[item.dateLabel] ?? []), item]
      return groups
    }, {})
  }, [filteredChats])

  const dateRangeLabel = useMemo(() => {
    const formatter = new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'short' })
    const end = new Date(`${todayIso}T00:00:00`)
    const start = new Date(end)
    start.setDate(end.getDate() - selectedRange + 1)
    return `${formatter.format(start)} - ${formatter.format(end)}`
  }, [selectedRange])

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
      handleApiError(error, 'Не удалось сохранить изменения.', field.startsWith('target') || field.endsWith('Percent') ? setGoalsError : setProfileError)
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
    }
  }

  async function handleLogout() {
    try {
      await authApi.logout()
      onUnauthorized()
    } catch (error) {
      handleApiError(error, 'Не удалось выйти из аккаунта.', setProfileError)
      showToast('Не удалось выйти')
    }
  }

  async function handleDeleteRequest() {
    try {
      await profileApi.requestAccountDeletion()
      setConfirmDeleteOpen(false)
      showToast('Запрос на удаление аккаунта создан')
    } catch (error) {
      handleApiError(error, 'Не удалось создать запрос на удаление.', setProfileError)
    }
  }

  if (loading) {
    return <ProfileSkeleton />
  }

  const displayName = profile ? `${profile.firstName} ${profile.secondName}`.trim() : 'Профиль'

  return (
    <main className="profile-page">
      <ProfileSidebar
        profile={profile}
        chatGroups={chatGroups}
        chatQuery={chatQuery}
        onChatQueryChange={setChatQuery}
        onBackToChat={onBackToChat}
      />

      <section className="profile-main" aria-label="Личный кабинет">
        <header className="profile-topbar">
          <div className="profile-title-group">
            <span className="profile-title-icon" aria-hidden="true">◎</span>
            <div>
              <h1>Личный кабинет</h1>
              <p>Профиль, цели и статистика</p>
            </div>
          </div>
          <button type="button" className="date-range-button" aria-label="Выбранный период">
            <span aria-hidden="true">▣</span>
            {dateRangeLabel}
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
            onRangeChange={handleRangeChange}
          />
          <AiInsightsStubCard />
          <SystemCard
            unitsOpen={unitsOpen}
            onToggleUnits={() => setUnitsOpen((value) => !value)}
            onCloseUnits={() => setUnitsOpen(false)}
            onExportCsv={handleExportCsv}
            onLogout={handleLogout}
            onOpenDelete={() => setConfirmDeleteOpen(true)}
          />
        </div>
      </section>

      {toast && <div className="profile-toast" role="status">{toast.text}</div>}
      {confirmDeleteOpen && (
        <ConfirmDeleteAccountModal
          onCancel={() => setConfirmDeleteOpen(false)}
          onConfirm={handleDeleteRequest}
        />
      )}
    </main>
  )
}

function ProfileSidebar({
  profile,
  chatGroups,
  chatQuery,
  onChatQueryChange,
  onBackToChat,
}: {
  profile: ProfileData | null
  chatGroups: Record<string, ChatHistoryItemData[]>
  chatQuery: string
  onChatQueryChange: (value: string) => void
  onBackToChat: () => void
}) {
  const initials = getInitials(profile)
  return (
    <aside className="profile-sidebar" aria-label="История чатов">
      <div className="sidebar-brand">
        <span className="brand-mark">N</span>
        <strong>NutriMate AI</strong>
      </div>
      <div className="sidebar-actions">
        <button type="button" className="primary-action" onClick={onBackToChat}>
          <span aria-hidden="true">＋</span>
          Новый чат
        </button>
        <button type="button" className="icon-action" aria-label="Фильтры истории">⌘</button>
      </div>
      <label className="chat-search">
        <span>Поиск по чатам</span>
        <input value={chatQuery} onChange={(event) => onChatQueryChange(event.target.value)} placeholder="Поиск" />
      </label>
      <div className="chat-history-list">
        {Object.entries(chatGroups).map(([label, items]) => (
          <section key={label} className="chat-history-group">
            <h2>{label}</h2>
            {items.map((item, index) => (
              <button key={item.id} type="button" className={`chat-history-item ${index === 0 && label === 'Сегодня' ? 'selected' : ''}`} onClick={onBackToChat}>
                <span className="chat-icon" aria-hidden="true">□</span>
                <span className="chat-copy">
                  <strong>{item.title}</strong>
                  <small>{item.preview}</small>
                </span>
                <time>{item.timeLabel}</time>
              </button>
            ))}
          </section>
        ))}
      </div>
      <button type="button" className="sidebar-user-card" aria-label="Открыть меню пользователя">
        <span className="avatar">{initials}</span>
        <span>
          <strong>{profile ? `${profile.firstName} ${profile.secondName}` : 'Пользователь'}</strong>
          <small>Тариф: Базовый</small>
        </span>
        <span aria-hidden="true">⌄</span>
      </button>
    </aside>
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
      <TileHeader icon="◌" title="Профиль" />
      <div className="profile-identity">
        <span className="profile-avatar">{getInitials(props.profile)}</span>
        <div>
          <strong>{props.displayName}</strong>
          <span>Тариф: Базовый</span>
        </div>
      </div>
      <EditableRow label="Имя" field="firstName" value={props.profile?.firstName ?? ''} {...props} />
      <EditableRow label="Фамилия" field="secondName" value={props.profile?.secondName ?? ''} {...props} />
      <div className="readonly-row">
        <span>Email</span>
        <strong>{props.profile?.email}</strong>
        <small>Изменение email пока недоступно</small>
      </div>
      <button type="button" className="secondary-action" disabled>Управление подпиской</button>
    </article>
  )
}

function DailyGoalsCard(props: {
  goal: DailyGoal
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
  const meals = [
    { field: 'breakfastPercent' as const, label: 'Завтрак', percent: props.goal.breakfastPercent ?? 25 },
    { field: 'lunchPercent' as const, label: 'Обед', percent: props.goal.lunchPercent ?? 35 },
    { field: 'dinnerPercent' as const, label: 'Ужин', percent: props.goal.dinnerPercent ?? 30 },
    { field: 'snackPercent' as const, label: 'Перекус', percent: props.goal.snackPercent ?? 10 },
  ]
  const mealPercentTotal = meals.reduce((sum, item) => sum + item.percent, 0)

  return (
    <article className="profile-tile goals-tile">
      <TileHeader icon="◍" title="Цели на день" />
      {props.error && <TileError text={props.error} />}
      <div className="goal-metric-grid">
        <EditableMetric label="Калории" suffix="ккал" field="targetCalories" value={props.goal.targetCalories} {...props} />
        <EditableMetric label="Белки" suffix="г" field="targetProtein" value={props.goal.targetProtein} tone="protein" {...props} />
        <EditableMetric label="Жиры" suffix="г" field="targetFat" value={props.goal.targetFat} tone="fat" {...props} />
        <EditableMetric label="Углеводы" suffix="г" field="targetCarbs" value={props.goal.targetCarbs} tone="carbs" {...props} />
      </div>
      <div className="meal-distribution">
        <h3>Распределение калорий по приёмам пищи</h3>
        {meals.map((meal) => (
          <MealGoalRow
            key={meal.field}
            label={meal.label}
            kcal={Math.round(props.goal.targetCalories * meal.percent / 100)}
            percent={meal.percent}
            field={meal.field}
            {...props}
          />
        ))}
        {mealPercentTotal !== 100 && <small className="goal-warning">Сумма приёмов отличается от дневной цели</small>}
      </div>
    </article>
  )
}

function NutritionDynamicsCard({ statistics, goal, rangeDays, error, onRangeChange }: {
  statistics: ProfileStatistics | null
  goal: DailyGoal
  rangeDays: number
  error: string | null
  onRangeChange: (rangeDays: number) => void
}) {
  return (
    <article className="profile-tile dynamics-tile">
      <div className="tile-header wide">
        <div className="tile-heading">
          <span className="tile-icon" aria-hidden="true">▥</span>
          <div>
            <h2>Статистика и динамика</h2>
            <p>Калории по макронутриентам (ккал)</p>
          </div>
        </div>
        <RangeSegmentedControl value={rangeDays} onChange={onRangeChange} />
      </div>
      {error && <TileError text={error} />}
      {statistics ? (
        <StackedMacroBarChart statistics={statistics} goal={goal} rangeDays={rangeDays} />
      ) : (
        <div className="empty-chart">Нет данных для графика.</div>
      )}
      <div className="macro-legend">
        <span><i className="protein" />Белки</span>
        <span><i className="fat" />Жиры</span>
        <span><i className="carbs" />Углеводы</span>
      </div>
      <div className="advice-strip">Совет: старайтесь держать белки на уровне 20-30% от дневной нормы.</div>
    </article>
  )
}

function StackedMacroBarChart({ statistics, goal, rangeDays }: { statistics: ProfileStatistics; goal: DailyGoal; rangeDays: number }) {
  const maxValue = Math.max(goal.targetCalories, ...statistics.items.map((item) => item.totalCalories), 1) * 1.12
  const goalY = 190 - (goal.targetCalories / maxValue) * 170
  const barStep = 700 / statistics.items.length
  const barWidth = rangeDays === 7 ? 44 : rangeDays === 14 ? 22 : 10
  const showLabels = rangeDays === 7

  return (
    <div className="chart-wrap" aria-label="График калорий по макронутриентам">
      <svg viewBox="0 0 760 230" role="img">
        <line x1="44" y1={goalY} x2="724" y2={goalY} className="goal-line" />
        <text x="616" y={Math.max(14, goalY - 6)} className="goal-label">Цель {formatNumber(goal.targetCalories)} ккал</text>
        {[0, 0.5, 1].map((ratio) => (
          <g key={ratio}>
            <line x1="44" x2="724" y1={190 - ratio * 170} y2={190 - ratio * 170} className="axis-line" />
            <text x="4" y={195 - ratio * 170} className="axis-label">{formatNumber(maxValue * ratio)}</text>
          </g>
        ))}
        {statistics.items.map((item, index) => {
          const x = 58 + index * barStep + (barStep - barWidth) / 2
          const totalHeight = item.hasData ? Math.max(6, (item.totalCalories / maxValue) * 170) : 36
          const baseY = 190
          const macroKcal = {
            protein: item.proteinGrams * 4,
            fat: item.fatGrams * 9,
            carbs: item.carbsGrams * 4,
          }
          const macroTotal = Math.max(macroKcal.protein + macroKcal.fat + macroKcal.carbs, item.totalCalories, 1)
          const proteinHeight = item.hasData ? totalHeight * macroKcal.protein / macroTotal : 0
          const fatHeight = item.hasData ? totalHeight * macroKcal.fat / macroTotal : 0
          const carbsHeight = item.hasData ? Math.max(0, totalHeight - proteinHeight - fatHeight) : 0
          const date = new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'short' }).format(new Date(`${item.date}T00:00:00`))
          const labelVisible = rangeDays !== 30 || index % 4 === 0
          const title = `${date}: ${formatNumber(item.totalCalories)} ккал, Б ${formatNumber(item.proteinGrams)} г, Ж ${formatNumber(item.fatGrams)} г, У ${formatNumber(item.carbsGrams)} г`

          if (!item.hasData) {
            return (
              <g key={item.date}>
                <title>{date}: нет данных</title>
                <rect x={x} y={baseY - totalHeight} width={barWidth} height={totalHeight} rx="6" className="empty-bar" />
                {showLabels && <text x={x + barWidth / 2} y={baseY - totalHeight - 8} className="bar-empty-label">Нет данных</text>}
                {labelVisible && <text x={x + barWidth / 2} y="214" className="x-label">{shortDate(item.date, rangeDays)}</text>}
              </g>
            )
          }

          return (
            <g key={item.date}>
              <title>{title}</title>
              {showLabels && <text x={x + barWidth / 2} y={baseY - totalHeight - 8} className="bar-total">{formatNumber(item.totalCalories)}</text>}
              <rect x={x} y={baseY - carbsHeight} width={barWidth} height={carbsHeight} rx="5" className="bar-carbs" />
              <rect x={x} y={baseY - carbsHeight - fatHeight} width={barWidth} height={fatHeight} className="bar-fat" />
              <rect x={x} y={baseY - carbsHeight - fatHeight - proteinHeight} width={barWidth} height={proteinHeight} rx="5" className="bar-protein" />
              {showLabels && totalHeight > 72 && <text x={x + barWidth / 2} y={baseY - totalHeight / 2} className="bar-percent">{Math.round(item.totalCalories / goal.targetCalories * 100)}%</text>}
              {labelVisible && <text x={x + barWidth / 2} y="214" className="x-label">{shortDate(item.date, rangeDays)}</text>}
            </g>
          )
        })}
      </svg>
    </div>
  )
}

function SystemCard({ unitsOpen, onToggleUnits, onCloseUnits, onExportCsv, onLogout, onOpenDelete }: {
  unitsOpen: boolean
  onToggleUnits: () => void
  onCloseUnits: () => void
  onExportCsv: () => void
  onLogout: () => void
  onOpenDelete: () => void
}) {
  return (
    <article className="profile-tile system-tile">
      <TileHeader icon="⚙" title="Система" />
      <div className="system-row with-popover">
        <span>
          <strong>Единицы измерения</strong>
          <small>Метрические (кг, см)</small>
        </span>
        <button type="button" className="icon-action" aria-label="Изменить единицы измерения" onClick={onToggleUnits}>✎</button>
        {unitsOpen && (
          <div className="units-popover">
            <button type="button" onClick={onCloseUnits}>Метрические (кг, см)</button>
            <button type="button" disabled title="Будет позже">Имперские (lb, ft)</button>
          </div>
        )}
      </div>
      <SystemActionRow icon="⇩" label="Экспортировать данные в CSV" onClick={onExportCsv} />
      <SystemActionRow icon="↪" label="Разлогиниться" onClick={onLogout} />
      <SystemActionRow icon="!" label="Запросить удаление аккаунта" onClick={onOpenDelete} destructive />
    </article>
  )
}

function AiInsightsStubCard() {
  return (
    <article className="profile-tile insights-tile">
      <div className="tile-header">
        <div className="tile-heading">
          <span className="tile-icon" aria-hidden="true">✦</span>
          <h2>AI-инсайты и рекомендации</h2>
        </div>
        <span className="soon-badge">скоро</span>
      </div>
      <p>Мы анализируем ваши привычки питания и готовим персональные рекомендации.</p>
      <small>Пример рекомендаций</small>
      <ul>
        <li>Вы часто недобираете белок в обед.</li>
        <li>Попробуйте добавить больше белка в завтрак.</li>
        <li>Ваш ужин часто слишком калорийный.</li>
      </ul>
    </article>
  )
}

function EditableRow({ label, field, value, editingField, draftValue, savingField, fieldError, onStartEdit, onDraftChange, onSave, onCancel, onKeyDown }: {
  label: string
  field: EditableField
  value: string
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
  const isEditing = editingField === field
  return (
    <label className="editable-row">
      <span>{label}</span>
      {isEditing ? (
        <span className="edit-control">
          <input autoFocus value={draftValue} onChange={(event) => onDraftChange(event.target.value)} onKeyDown={(event) => onKeyDown(event, field)} />
          <button type="button" aria-label="Сохранить" disabled={savingField === field} onClick={() => onSave(field)}>{savingField === field ? '…' : '✓'}</button>
          <button type="button" aria-label="Отмена" onClick={onCancel}>×</button>
          {fieldError && <small>{fieldError}</small>}
        </span>
      ) : (
        <span className="field-display">
          <strong>{value}</strong>
          <button type="button" aria-label={`Редактировать ${label}`} onClick={() => onStartEdit(field, value)}>✎</button>
        </span>
      )}
    </label>
  )
}

function EditableMetric(props: {
  label: string
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
      {isEditing ? (
        <span className="metric-edit">
          <input autoFocus type="number" min="0" value={props.draftValue} onChange={(event) => props.onDraftChange(event.target.value)} onKeyDown={(event) => props.onKeyDown(event, props.field)} />
          <button type="button" aria-label="Сохранить" disabled={props.savingField === props.field} onClick={() => props.onSave(props.field)}>{props.savingField === props.field ? '…' : '✓'}</button>
          <button type="button" aria-label="Отмена" onClick={props.onCancel}>×</button>
          {props.fieldError && <small>{props.fieldError}</small>}
        </span>
      ) : (
        <span className="metric-value">
          <strong>{formatNumber(props.value)}</strong>
          <small>{props.suffix}</small>
          <button type="button" aria-label={`Редактировать ${props.label}`} onClick={() => props.onStartEdit(props.field, props.value)}>✎</button>
        </span>
      )}
    </label>
  )
}

function MealGoalRow(props: {
  label: string
  kcal: number
  percent: number
  field: EditableField
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
    <div className="meal-goal-row">
      <span className="meal-square" aria-hidden="true" />
      <strong>{props.label}</strong>
      <span className="meal-bar"><i style={{ width: `${Math.min(100, props.percent)}%` }} /></span>
      {isEditing ? (
        <span className="meal-edit">
          <input autoFocus type="number" min="0" value={props.draftValue} onChange={(event) => props.onDraftChange(event.target.value)} onKeyDown={(event) => props.onKeyDown(event, props.field)} />
          <button type="button" aria-label="Сохранить" disabled={props.savingField === props.field} onClick={() => props.onSave(props.field)}>{props.savingField === props.field ? '…' : '✓'}</button>
          <button type="button" aria-label="Отмена" onClick={props.onCancel}>×</button>
        </span>
      ) : (
        <>
          <span>{props.kcal} ккал</span>
          <small>{formatNumber(props.percent)}%</small>
          <button type="button" aria-label={`Редактировать ${props.label}`} onClick={() => props.onStartEdit(props.field, props.percent)}>✎</button>
        </>
      )}
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

function SystemActionRow({ icon, label, onClick, destructive = false }: { icon: string; label: string; onClick: () => void; destructive?: boolean }) {
  return (
    <button type="button" className={`system-action ${destructive ? 'destructive' : ''}`} onClick={onClick}>
      <span aria-hidden="true">{icon}</span>
      {label}
    </button>
  )
}

function ConfirmDeleteAccountModal({ onCancel, onConfirm }: { onCancel: () => void; onConfirm: () => void }) {
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
        <p>Мы отметим аккаунт на удаление. Это действие чувствительное.</p>
        <div>
          <button type="button" className="secondary-action" ref={cancelRef} onClick={onCancel}>Отмена</button>
          <button type="button" className="danger-action" onClick={onConfirm}>Запросить удаление</button>
        </div>
      </section>
    </div>
  )
}

function TileHeader({ icon, title }: { icon: string; title: string }) {
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
      <aside className="profile-sidebar skeleton-block" />
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
  const first = profile?.firstName?.[0] ?? 'N'
  const second = profile?.secondName?.[0] ?? 'M'
  return `${first}${second}`.toUpperCase()
}

function formatNumber(value: number): string {
  return Number.isInteger(value) ? value.toString() : value.toFixed(1)
}

function shortDate(value: string, rangeDays: number): string {
  const date = new Date(`${value}T00:00:00`)
  return new Intl.DateTimeFormat('ru-RU', rangeDays === 7 ? { weekday: 'short' } : { day: 'numeric', month: 'numeric' }).format(date)
}
