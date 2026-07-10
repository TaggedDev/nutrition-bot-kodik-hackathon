import { useCallback, useEffect, useMemo, useState } from 'react'
import type { CSSProperties, Dispatch, FormEvent, SetStateAction } from 'react'
import type {
  CurrentUser,
  DailyGoal,
  MealEntriesByType,
  MealEntryItem,
  NutritionChatSearchResponse,
  NutritionSummary,
  ProductNutrition,
  ProfileDay,
} from './types'
import './ChatPage.css'

type Props = {
  currentUser: CurrentUser
  onOpenProfile: () => void
  onUnauthorized: () => void
}

type PortionKey = '100' | '50' | 'custom'
type MealType = 'Breakfast' | 'Lunch' | 'Dinner' | 'Snack'

type PortionState = {
  portion: PortionKey
  grams: number
}

type ChatLine =
  | { id: string; role: 'user'; text: string; time: string }
  | { id: string; role: 'assistant'; kind: 'text'; text: string }
  | { id: string; role: 'assistant'; kind: 'results'; text: string; products: ProductNutrition[] }

const emptySummary: NutritionSummary = { calories: 0, protein: 0, fat: 0, carbs: 0 }
const mealOrder: MealType[] = ['Breakfast', 'Lunch', 'Dinner', 'Snack']
const mealLabels: Record<MealType, string> = {
  Breakfast: 'Завтрак',
  Lunch: 'Обед',
  Dinner: 'Ужин',
  Snack: 'Перекус',
}

export function ChatPage({ currentUser, onOpenProfile, onUnauthorized }: Props) {
  const [lines, setLines] = useState<ChatLine[]>([])
  const [input, setInput] = useState('')
  const [loadingSearch, setLoadingSearch] = useState(false)
  const [loadingDay, setLoadingDay] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [day, setDay] = useState<ProfileDay | null>(null)
  const [selectedDate, setSelectedDate] = useState(() => new Date())
  const [currentMealType, setCurrentMealType] = useState<MealType>(() => getCurrentMealType(new Date()))
  const [portionMap, setPortionMap] = useState<Record<string, PortionState>>({})
  const [editingGrams, setEditingGrams] = useState<Record<string, string>>({})
  const [isCurrentMealExpanded, setIsCurrentMealExpanded] = useState(false)

  const selectedDateOnly = useMemo(() => toDateOnly(selectedDate), [selectedDate])

  const loadDay = useCallback(async () => {
    setLoadingDay(true)
    setError(null)
    try {
      const utcOffsetMinutes = -new Date().getTimezoneOffset()
      const response = await fetch(`/api/v1/profile/day?date=${selectedDateOnly}&utcOffsetMinutes=${utcOffsetMinutes}`, { credentials: 'include' })
      if (response.status === 401) {
        onUnauthorized()
        return
      }
      if (!response.ok) {
        throw new Error(`Ошибка загрузки дня: ${response.status}`)
      }

      setDay(normalizeDay((await response.json()) as ProfileDay, selectedDateOnly))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Не удалось загрузить текущий день.')
    } finally {
      setLoadingDay(false)
    }
  }, [onUnauthorized, selectedDateOnly])

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadDay()
  }, [loadDay])

  const currentMeal = useMemo(
    () => findMeal(day, currentMealType),
    [currentMealType, day],
  )

  const currentMealEntries = useMemo(() => currentMeal?.entries ?? [], [currentMeal])
  const currentMealSummary = currentMeal?.summary ?? emptySummary
  const addedByProduct = useMemo(() => {
    const entries = new Map<string, MealEntryItem>()
    for (const entry of currentMealEntries) {
      if (entry.sourceReference) {
        entries.set(entry.sourceReference, entry)
      }
    }
    return entries
  }, [currentMealEntries])

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const text = input.trim()
    if (!text || loadingSearch) return

    setLines((current) => [...current, { id: crypto.randomUUID(), role: 'user', text, time: formatTime(new Date()) }])
    setInput('')
    setLoadingSearch(true)
    setError(null)

    try {
      const response = await fetch(`/api/v1/nutrition/search?query=${encodeURIComponent(text)}`, {
        credentials: 'include',
      })
      if (!response.ok) {
        throw new Error(`Ошибка поиска: ${response.status}`)
      }

      const payload = (await response.json()) as NutritionChatSearchResponse
      const products = extractProducts(payload).slice(0, 3)
      setPortionMap((current) => {
        const next = { ...current }
        for (const product of products) {
          const key = productKey(product)
          if (!next[key]) {
            next[key] = defaultPortionState(product)
          }
        }
        return next
      })
      setLines((current) => [
        ...current,
        products.length > 0
          ? {
              id: crypto.randomUUID(),
              role: 'assistant',
              kind: 'results',
              text: 'Нашел подходящие варианты. Выберите продукт и граммовку:',
              products,
            }
          : {
              id: crypto.randomUUID(),
              role: 'assistant',
              kind: 'text',
              text: 'Ничего не нашел. Попробуйте уточнить название, бренд или вес продукта.',
            },
      ])
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Не удалось выполнить поиск.')
      setLines((current) => [
        ...current,
        {
          id: crypto.randomUUID(),
          role: 'assistant',
          kind: 'text',
          text: 'Не удалось выполнить поиск. Проверьте, что backend запущен, и попробуйте еще раз.',
        },
      ])
    } finally {
      setLoadingSearch(false)
    }
  }

  async function handleAdd(product: ProductNutrition) {
    const key = productKey(product)
    const added = addedByProduct.get(key)
    if (added) {
      await handleRemoveEntry(added.id)
      return
    }

    const state = portionMap[key] ?? { portion: '100' as const, grams: 100 }
    const facts = calculateFacts(product, state.grams)
    const response = await fetch('/api/v1/profile/entry', {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        productName: product.productName,
        brand: product.brand ?? '',
        calories: facts.calories,
        protein: facts.protein,
        fat: facts.fat,
        carbs: facts.carbs,
        mealType: currentMealType,
        servingGrams: state.grams,
        portionLabel: portionLabel(state),
        sourceType: product.sourceType,
        sourceReference: key,
        loggedAtUtc: toLoggedAtIso(selectedDate),
      }),
    })

    if (response.status === 401) {
      onUnauthorized()
      return
    }
    if (!response.ok) {
      setError(`Не удалось добавить продукт: ${response.status}`)
      return
    }

    const entry = (await response.json()) as MealEntryItem
    setDay((current) => addEntryToDay(current, normalizeEntry(entry), currentMealType, selectedDateOnly))
    setLines((current) => [
      ...current,
      {
        id: crypto.randomUUID(),
        role: 'assistant',
        kind: 'text',
        text: `Добавил «${product.productName}» в ${mealLabels[currentMealType].toLowerCase()}.`,
      },
    ])
  }

  async function handleRemoveEntry(entryId: string) {
    const response = await fetch(`/api/v1/profile/entry/${entryId}`, {
      method: 'DELETE',
      credentials: 'include',
    })

    if (response.status === 401) {
      onUnauthorized()
      return
    }
    if (!response.ok) {
      setError(`Не удалось удалить продукт: ${response.status}`)
      return
    }

    setDay((current) => removeEntryFromDay(current, entryId, selectedDateOnly))
  }

  async function handleClearMeal() {
    if (currentMealEntries.length === 0 || loadingDay) return

    setError(null)
    for (const entry of currentMealEntries) {
      const response = await fetch(`/api/v1/profile/entry/${entry.id}`, {
        method: 'DELETE',
        credentials: 'include',
      })

      if (response.status === 401) {
        onUnauthorized()
        return
      }
      if (!response.ok) {
        setError(`Не удалось очистить приём пищи: ${response.status}`)
        await loadDay()
        return
      }
    }

    setDay((current) => {
      let next = current
      for (const entry of currentMealEntries) {
        next = removeEntryFromDay(next, entry.id, selectedDateOnly)
      }
      return next
    })
  }

  async function handleMealEntryGrams(entry: MealEntryItem, gramsValue: string) {
    const grams = Number(gramsValue)
    if (!Number.isFinite(grams) || grams <= 0 || entry.servingGrams <= 0) {
      setEditingGrams((current) => ({ ...current, [entry.id]: formatNumber(entry.servingGrams) }))
      return
    }

    const ratio = grams / entry.servingGrams
    const nextEntry = {
      ...entry,
      servingGrams: grams,
      portionLabel: `${formatNumber(grams)} г`,
      calories: round(entry.calories * ratio),
      protein: round(entry.protein * ratio),
      fat: round(entry.fat * ratio),
      carbs: round(entry.carbs * ratio),
    }

    setDay((current) => replaceEntryInDay(current, nextEntry, selectedDateOnly))

    const response = await fetch(`/api/v1/profile/entry/${entry.id}`, {
      method: 'PUT',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        productName: nextEntry.productName,
        brand: nextEntry.brand,
        calories: nextEntry.calories,
        protein: nextEntry.protein,
        fat: nextEntry.fat,
        carbs: nextEntry.carbs,
        mealType: nextEntry.mealType,
        servingGrams: nextEntry.servingGrams,
        portionLabel: nextEntry.portionLabel,
        sourceType: nextEntry.sourceType,
        sourceReference: nextEntry.sourceReference,
        loggedAtUtc: nextEntry.loggedAtUtc,
      }),
    })

    if (response.status === 401) {
      onUnauthorized()
      return
    }
    if (!response.ok) {
      setError(`Не удалось изменить порцию: ${response.status}`)
      await loadDay()
      return
    }

    const savedEntry = normalizeEntry((await response.json()) as MealEntryItem)
    setDay((current) => replaceEntryInDay(current, savedEntry, selectedDateOnly))
  }

  function resetChat() {
    setLines([])
    setInput('')
    setPortionMap({})
    setError(null)
  }

  return (
    <main className="nutri-page">
      <section className="chat-workspace" aria-label="Чат питания">
        <header className="chat-header">
          <div className="assistant-title">
            <span className="ai-icon"><UtensilsIcon /></span>
            <div>
              <h1>AI-ассистент <span>NutriMate AI</span></h1>
              <p>Ваш помощник по питанию и КБЖУ</p>
            </div>
          </div>
          <div className="header-actions">
            <button type="button" className="clear-btn" onClick={resetChat} aria-label="Очистить чат">
              Очистить чат
            </button>
          </div>
        </header>

        <CurrentMealPanel
          loading={loadingDay}
          isExpanded={isCurrentMealExpanded}
          mealType={currentMealType}
          entries={currentMealEntries}
          summary={currentMealSummary}
          editingGrams={editingGrams}
          onToggleExpanded={() => setIsCurrentMealExpanded((expanded) => !expanded)}
          onClearMeal={handleClearMeal}
          onEditingGramsChange={(id, value) => setEditingGrams((current) => ({ ...current, [id]: value }))}
          onCommitGrams={handleMealEntryGrams}
          onRemove={handleRemoveEntry}
        />

        <div className="conversation">
          {lines.length === 0 ? (
            <div className="empty-chat">
              <span><UtensilsIcon /></span>
              <h2>Напишите, что вы съели</h2>
              <p>Я найду продукты через OpenFoodFacts, покажу КБЖУ и дам выбрать граммовку перед добавлением.</p>
            </div>
          ) : (
            lines.map((line) => (
              <MessageRow
                key={line.id}
                line={line}
                portionMap={portionMap}
                addedByProduct={addedByProduct}
                onPortionChange={(product, portion) => setPortion(product, portion, setPortionMap)}
                onGramsChange={(product, grams) => setCustomGrams(product, grams, setPortionMap)}
                onAdd={handleAdd}
              />
            ))
          )}

          {loadingSearch && (
            <div className="message-row assistant-row">
              <div className="bot-avatar">AI</div>
              <div className="assistant-content">
                <p>Ищу подходящие продукты...</p>
              </div>
            </div>
          )}
        </div>

        {error && <div className="chat-error">{error}</div>}

        <form className="chat-input-bar" onSubmit={handleSubmit}>
          <div className="input-row">
            <label className="chat-input-shell">
              <span className="sr-only">Сообщение</span>
              <input
                value={input}
                onChange={(event) => setInput(event.target.value)}
                placeholder="Напишите сообщение..."
              />
            </label>
            <button type="submit" className="send-btn" aria-label="Отправить сообщение" disabled={loadingSearch}>›</button>
          </div>
          <div className="input-helper">
            <span>Например: Я съел творог 150 г</span>
            <span>NutriMate AI может ошибаться. Проверяйте важную информацию.</span>
          </div>
        </form>
      </section>

      <MealContextPanel
        selectedDate={selectedDate}
        onDateChange={setSelectedDate}
        currentMealType={currentMealType}
        onMealTypeChange={setCurrentMealType}
        day={day}
        summary={currentMealSummary}
        currentUser={currentUser}
        onOpenProfile={onOpenProfile}
      />
    </main>
  )
}

function MessageRow({
  line,
  portionMap,
  addedByProduct,
  onPortionChange,
  onGramsChange,
  onAdd,
}: {
  line: ChatLine
  portionMap: Record<string, PortionState>
  addedByProduct: Map<string, MealEntryItem>
  onPortionChange: (product: ProductNutrition, portion: PortionKey) => void
  onGramsChange: (product: ProductNutrition, grams: number) => void
  onAdd: (product: ProductNutrition) => void
}) {
  if (line.role === 'user') {
    return (
      <div className="message-row user-row">
        <div className="user-bubble">
          <span>{line.text}</span>
          <small>{line.time}</small>
        </div>
      </div>
    )
  }

  return (
    <div className="message-row assistant-row">
      <div className="bot-avatar">AI</div>
      <div className="assistant-content">
        <p>{line.text}</p>
        {line.kind === 'results' && (
          <div className="search-results-group">
            {line.products.map((product, index) => {
              const key = productKey(product)
              return (
                <FoodSearchResultCard
                  key={key}
                  product={product}
                  index={index}
                  state={portionMap[key] ?? defaultPortionState(product)}
                  added={addedByProduct.has(key)}
                  onPortionChange={onPortionChange}
                  onGramsChange={onGramsChange}
                  onAdd={onAdd}
                />
              )
            })}
          </div>
        )}
      </div>
    </div>
  )
}

function FoodSearchResultCard({
  product,
  index,
  state,
  added,
  onPortionChange,
  onGramsChange,
  onAdd,
}: {
  product: ProductNutrition
  index: number
  state: PortionState
  added: boolean
  onPortionChange: (product: ProductNutrition, portion: PortionKey) => void
  onGramsChange: (product: ProductNutrition, grams: number) => void
  onAdd: (product: ProductNutrition) => void
}) {
  const facts = calculateFacts(product, state.grams)

  return (
    <article className={`food-card ${added ? 'is-added' : ''}`}>
      <FoodThumb index={index} title={product.productName} />
      <div className="food-info">
        <h3>{product.productName}</h3>
        <p>{product.brand || 'Бренд не указан'}</p>
        <small>{formatNutritionBasis(product)}</small>
        <SourceLink product={product} />
      </div>
      <div className="macro-block" aria-label="КБЖУ">
        <strong>{formatNumber(facts.calories)} ккал</strong>
        <span>Б {formatNumber(facts.protein)} г</span>
        <span>Ж {formatNumber(facts.fat)} г</span>
        <span>У {formatNumber(facts.carbs)} г</span>
      </div>
      <div className="portion-controls">
        <div className="portion-buttons">
          <button type="button" className={state.portion === '100' ? 'active' : ''} onClick={() => onPortionChange(product, '100')}>
            100 г
          </button>
          <button type="button" className={state.portion === '50' ? 'active' : ''} onClick={() => onPortionChange(product, '50')}>
            50 г
          </button>
          <button type="button" className={state.portion === 'custom' ? 'active' : ''} onClick={() => onPortionChange(product, 'custom')}>
            Своя
          </button>
        </div>
        <input
          aria-label={`Граммы для ${product.productName}`}
          type="number"
          value={state.grams}
          min={1}
          onChange={(event) => onGramsChange(product, Number(event.target.value))}
        />
        <button type="button" className="add-result-btn" data-added={added} onClick={() => onAdd(product)}>
          <span className="add-label">Добавить</span>
          <span className="added-label">✓</span>
          <span className="remove-label">×</span>
        </button>
      </div>
    </article>
  )
}

function CurrentMealPanel({
  loading,
  isExpanded,
  mealType,
  entries,
  summary,
  editingGrams,
  onToggleExpanded,
  onClearMeal,
  onEditingGramsChange,
  onCommitGrams,
  onRemove,
}: {
  loading: boolean
  isExpanded: boolean
  mealType: MealType
  entries: MealEntryItem[]
  summary: NutritionSummary
  editingGrams: Record<string, string>
  onToggleExpanded: () => void
  onClearMeal: () => void
  onEditingGramsChange: (id: string, value: string) => void
  onCommitGrams: (entry: MealEntryItem, gramsValue: string) => void
  onRemove: (id: string) => void
}) {
  const itemCount = entries.length
  const summaryText = loading
    ? 'Загружаю текущий приём'
    : `${itemCount} ${pluralize(itemCount, 'позиция', 'позиции', 'позиций')} · ${formatNumber(summary.calories)} ккал · Б ${formatNumber(summary.protein)} г · Ж ${formatNumber(summary.fat)} г · У ${formatNumber(summary.carbs)} г`

  return (
    <section className={`current-meal-panel ${isExpanded ? 'expanded' : 'collapsed'}`} aria-label="Текущий приём пищи">
      <div className="current-meal-toolbar">
        <div className="current-meal-title">
          <span className="meal-icon" aria-hidden="true">
            <UtensilsIcon />
          </span>
          <div>
            <strong>Текущий приём пищи</strong>
            <span>
              {loading
                ? 'Загружаю данные'
                : itemCount === 0 && !isExpanded
                  ? 'В текущем приёме пищи пока ничего нет'
                  : isExpanded
                    ? `${itemCount} ${pluralize(itemCount, 'позиция', 'позиции', 'позиций')} · ${mealLabels[mealType]}`
                    : summaryText}
            </span>
            {itemCount === 0 && !loading && !isExpanded && (
              <small>Добавьте еду через чат · {formatNumber(summary.calories)} ккал · Б {formatNumber(summary.protein)} г · Ж {formatNumber(summary.fat)} г · У {formatNumber(summary.carbs)} г</small>
            )}
          </div>
        </div>

        <div className="current-meal-actions">
          <button type="button" className="meal-clear-btn" onClick={onClearMeal} disabled={loading || itemCount === 0}>
            Очистить приём
          </button>
          <button
            type="button"
            className="meal-toggle-btn"
            onClick={onToggleExpanded}
            aria-label={isExpanded ? 'Свернуть текущий приём пищи' : 'Раскрыть текущий приём пищи'}
            aria-expanded={isExpanded}
          >
            {isExpanded ? '⌃' : '⌄'}
          </button>
        </div>
      </div>

      {isExpanded && (
        <div className="current-meal-overlay-body">
          <div className="current-meal-scroll">
            {loading ? (
              <div className="current-meal-empty">Загружаю текущий приём пищи...</div>
            ) : entries.length === 0 ? (
              <div className="current-meal-empty">
                <span className="empty-meal-icon" aria-hidden="true">
                  <UtensilsIcon />
                </span>
                <strong>В этом приёме пищи пока ничего нет</strong>
                <p>Напишите в чат, например: «Я съел творог 150 г»</p>
              </div>
            ) : (
              <div className="current-meal-list">
                {entries.map((entry, index) => (
                  <CurrentMealItemCard
                    key={entry.id}
                    entry={entry}
                    index={index}
                    editingValue={editingGrams[entry.id]}
                    onEditingGramsChange={onEditingGramsChange}
                    onCommitGrams={onCommitGrams}
                    onRemove={onRemove}
                  />
                ))}
              </div>
            )}
          </div>

          <footer className="current-meal-footer">
            <span>Итого в текущем приёме</span>
            <strong>{formatNumber(summary.calories)} ккал</strong>
            <small>Б {formatNumber(summary.protein)} г · Ж {formatNumber(summary.fat)} г · У {formatNumber(summary.carbs)} г</small>
          </footer>
        </div>
      )}
    </section>
  )
}

function CurrentMealItemCard({
  entry,
  index,
  editingValue,
  onEditingGramsChange,
  onCommitGrams,
  onRemove,
}: {
  entry: MealEntryItem
  index: number
  editingValue: string | undefined
  onEditingGramsChange: (id: string, value: string) => void
  onCommitGrams: (entry: MealEntryItem, gramsValue: string) => void
  onRemove: (id: string) => void
}) {
  const gramsValue = editingValue ?? (entry.servingGrams > 0 ? formatNumber(entry.servingGrams) : '')

  return (
    <article className="current-meal-item">
      <FoodThumb index={index} title={entry.productName} small />
      <div className="current-meal-item-main">
        <div className="current-meal-item-head">
          <div>
            <h3>{entry.productName}</h3>
            <EntrySourceLink entry={entry} />
          </div>
          <strong>{formatNumber(entry.calories)} ккал</strong>
        </div>

        <div className="current-meal-item-grid">
          <span>Вы указали: <strong>{entry.portionLabel || 'Порция не указана'}</strong></span>
          <label className="current-meal-grams">
            <span>Будет записано:</span>
            <input
              type="number"
              min={1}
              disabled={entry.servingGrams <= 0}
              value={gramsValue}
              placeholder={entry.servingGrams > 0 ? undefined : '—'}
              onChange={(event) => onEditingGramsChange(entry.id, event.target.value)}
              onBlur={(event) => onCommitGrams(entry, event.target.value)}
              onKeyDown={(event) => {
                if (event.key === 'Enter') {
                  event.currentTarget.blur()
                }
              }}
            />
            <small>г</small>
          </label>
        </div>

        <div className="current-meal-macros">
          <span>Б {formatNumber(entry.protein)} г</span>
          <span>Ж {formatNumber(entry.fat)} г</span>
          <span>У {formatNumber(entry.carbs)} г</span>
        </div>

        <p className={`reference-basis ${entry.servingGrams > 0 ? '' : 'warning'}`}>{referenceBasisLabel(entry)}</p>
      </div>

      <div className="current-meal-item-actions">
        <button type="button" className="edit-entry-btn" onClick={() => onEditingGramsChange(entry.id, gramsValue || formatNumber(entry.servingGrams || 100))}>
          Изменить
        </button>
        <button type="button" className="delete-entry-btn" onClick={() => onRemove(entry.id)} aria-label={`Удалить ${entry.productName}`}>
          Удалить
        </button>
      </div>
    </article>
  )
}

function MealContextPanel({
  selectedDate,
  onDateChange,
  currentMealType,
  onMealTypeChange,
  day,
  summary,
  currentUser,
  onOpenProfile,
}: {
  selectedDate: Date
  onDateChange: (date: Date) => void
  currentMealType: MealType
  onMealTypeChange: (mealType: MealType) => void
  day: ProfileDay | null
  summary: NutritionSummary
  currentUser: CurrentUser
  onOpenProfile: () => void
}) {
  const [calendarOpen, setCalendarOpen] = useState(false)

  return (
    <aside className="meal-panel meal-context-panel" aria-label="Текущий приём пищи">
      <header className="meal-panel-header">
        <div className="calendar-anchor date-picker-anchor">
          <button
            type="button"
            className="panel-date-button"
            aria-label={`Выбрать дату, сейчас ${formatPanelDate(selectedDate)}`}
            aria-expanded={calendarOpen}
            onClick={() => setCalendarOpen((open) => !open)}
          >
            <CalendarIcon />
            <strong>{formatPanelDate(selectedDate)}</strong>
          </button>
          {calendarOpen && (
            <CalendarPopover
              selectedDate={selectedDate}
              onDateChange={(nextDate) => {
                onDateChange(nextDate)
                setCalendarOpen(false)
              }}
            />
          )}
        </div>
      </header>

      <section className="meal-total-card">
        <div className="total-head">
          <span>Итого</span>
          <strong>{formatNumber(summary.calories)} ккал</strong>
        </div>
        <div className="macro-total-row">
          <MacroDot label="Б" value={summary.protein} kind="protein" />
          <MacroDot label="Ж" value={summary.fat} kind="fat" />
          <MacroDot label="У" value={summary.carbs} kind="carbs" />
        </div>
      </section>

      <section className="today-card">
        <div className="section-head">
          <strong>Сегодня</strong>
        </div>
        {mealOrder.map((mealType) => {
          const meal = findMeal(day, mealType)
          const value = meal?.summary.calories ?? 0
          return (
            <ProgressRow
              key={mealType}
              label={mealLabels[mealType]}
              value={value}
              max={mealTarget(day?.goal, mealType)}
              active={mealType === currentMealType}
              onSelect={() => onMealTypeChange(mealType)}
            />
          )
        })}
      </section>

      <button type="button" className="profile-shortcut" onClick={onOpenProfile} aria-label="Открыть личный кабинет">
        <span className="avatar">{initials(currentUser)}</span>
        <span className="profile-shortcut-copy">
          <strong>{currentUser.firstName} {currentUser.secondName}</strong>
          <small>Личный кабинет</small>
        </span>
        <span className="chevron" aria-hidden="true">›</span>
      </button>
    </aside>
  )
}

function CalendarPopover({
  selectedDate,
  onDateChange,
}: {
  selectedDate: Date
  onDateChange: (date: Date) => void
}) {
  const [visibleMonth, setVisibleMonth] = useState(() => startOfMonth(selectedDate))
  const days = useMemo(() => getMonthDays(visibleMonth), [visibleMonth])
  const monthLabel = new Intl.DateTimeFormat('ru-RU', { month: 'long' }).format(visibleMonth)
  const weekDays = ['Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб', 'Вс']

  return (
    <div className="calendar-popover" role="dialog" aria-label="Календарь выбора дня">
      <div className="calendar-popover-header">
        <button type="button" aria-label="Предыдущий месяц" onClick={() => setVisibleMonth(addMonths(visibleMonth, -1))}>
          ‹
        </button>
        <strong>{capitalize(monthLabel)}</strong>
        <button type="button" aria-label="Следующий месяц" onClick={() => setVisibleMonth(addMonths(visibleMonth, 1))}>
          ›
        </button>
      </div>

      <div className="calendar-weekdays" aria-hidden="true">
        {weekDays.map((day) => (
          <span key={day}>{day}</span>
        ))}
      </div>

      <div className="calendar-grid">
        {days.map((item, index) =>
          item ? (
            <button
              key={item.toISOString()}
              type="button"
              className={isSameDay(item, selectedDate) ? 'active' : ''}
              onClick={() => onDateChange(item)}
            >
              {item.getDate()}
            </button>
          ) : (
            <span key={`empty-${index}`} aria-hidden="true" />
          ),
        )}
      </div>

      <div className="calendar-year">{visibleMonth.getFullYear()}</div>
    </div>
  )
}

function FoodThumb({ index, title, small = false }: { index: number; title: string; small?: boolean }) {
  return (
    <div className={`food-thumb thumb-${index % 3} ${small ? 'small' : ''}`} role="img" aria-label={title}>
      <span />
    </div>
  )
}

function MacroDot({ label, value, kind }: { label: string; value: number; kind: string }) {
  return (
    <span className={`macro-dot ${kind}`}>
      <i /> {label} {formatNumber(value)} г
    </span>
  )
}

function ProgressRow({
  label,
  value,
  max,
  active = false,
  onSelect,
}: {
  label: string
  value: number
  max: number
  active?: boolean
  onSelect: () => void
}) {
  const pct = Math.min(100, Math.round((value / Math.max(max, 1)) * 100))

  return (
    <button type="button" className={`progress-row ${active ? 'active' : ''}`} onClick={onSelect}>
      <span className={`meal-progress-icon ${mealIconClass(label)}`} aria-hidden="true"><MealTypeIcon label={label} /></span>
      <div className="meal-progress-content">
        <div className="meal-progress-head">
          <span>{label}</span>
          <strong>{formatNumber(value)} / {formatNumber(max)} ккал</strong>
        </div>
        <div className="progress-track">
          <span style={{ width: `${pct}%` }} />
        </div>
      </div>
    </button>
  )
}

function MealTypeIcon({ label }: { label: string }) {
  if (label === 'Завтрак') return <svg viewBox="0 0 24 24"><path d="M5 11h12v3a6 6 0 0 1-12 0v-3Zm12 1h2a2 2 0 0 1 0 4h-2M8 8V5m4 3V4m4 4V5" /></svg>
  if (label === 'Обед') return <svg viewBox="0 0 24 24"><path d="M4 13h16a8 8 0 0 1-16 0Zm8-7v4m-5 0a5 5 0 0 1 10 0" /></svg>
  if (label === 'Ужин') return <svg viewBox="0 0 24 24"><path d="M19 15.5A8 8 0 0 1 8.5 5 8 8 0 1 0 19 15.5Z" /></svg>
  return <svg viewBox="0 0 24 24"><path d="M12 7c-4-4-9 0-7 6 2 7 7 7 7 7s5 0 7-7c2-6-3-10-7-6Zm0 0c0-3 2-5 5-5" /></svg>
}

function mealIconClass(label: string): string {
  return label === 'Завтрак' ? 'breakfast' : label === 'Обед' ? 'lunch' : label === 'Ужин' ? 'dinner' : 'snack'
}

export function Donut({ value }: { value: number }) {
  return (
    <div className="donut" style={{ '--donut-value': `${value * 3.6}deg` } as CSSProperties}>
      <span>{value}%</span>
    </div>
  )
}

export function MacroProgress({ label, value, max }: { label: string; value: number; max: number }) {
  return (
    <div className="macro-progress">
      <div>
        <span>{label}</span>
        <strong>{formatNumber(value)} / {formatNumber(max)} г</strong>
      </div>
      <div className="progress-track">
        <span style={{ width: `${Math.min(100, (value / Math.max(max, 1)) * 100)}%` }} />
      </div>
    </div>
  )
}

function UtensilsIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
      <path d="M7 3v7.2a3 3 0 0 1-1.9 2.8V21H3.7v-8A3 3 0 0 1 2 10.2V3h1.4v6h1V3h1.3v6h1V3H7Zm6.8 0c2.9.8 4.7 3.2 4.7 6v3.4h-2.1V21H15V3h-1.2Z" />
    </svg>
  )
}

function CalendarIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
      <path d="M7 2.5A1.5 1.5 0 0 1 8.5 4v1h7V4a1.5 1.5 0 0 1 3 0v1H20a2 2 0 0 1 2 2v12.5a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2h1.5V4A1.5 1.5 0 0 1 7 2.5ZM4 10v9.5h16V10H4Zm3 3h2v2H7v-2Zm4 0h2v2h-2v-2Zm4 0h2v2h-2v-2Z" />
    </svg>
  )
}

function getCurrentMealType(date: Date): MealType {
  const hour = date.getHours()
  if (hour >= 5 && hour < 11) return 'Breakfast'
  if (hour >= 11 && hour < 16) return 'Lunch'
  if (hour >= 16 && hour < 22) return 'Dinner'
  return 'Snack'
}

function toDateOnly(date: Date): string {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function toLoggedAtIso(date: Date): string {
  const now = new Date()
  return new Date(
    date.getFullYear(),
    date.getMonth(),
    date.getDate(),
    now.getHours(),
    now.getMinutes(),
    now.getSeconds(),
    now.getMilliseconds(),
  ).toISOString()
}

function formatPanelDate(date: Date): string {
  return new Intl.DateTimeFormat('ru-RU', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(date)
}

function startOfMonth(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), 1)
}

function addMonths(date: Date, months: number): Date {
  return new Date(date.getFullYear(), date.getMonth() + months, 1)
}

function getMonthDays(date: Date): Array<Date | null> {
  const firstDay = startOfMonth(date)
  const lastDay = new Date(date.getFullYear(), date.getMonth() + 1, 0)
  const mondayOffset = (firstDay.getDay() + 6) % 7
  const days: Array<Date | null> = Array.from({ length: mondayOffset }, () => null)

  for (let day = 1; day <= lastDay.getDate(); day += 1) {
    days.push(new Date(date.getFullYear(), date.getMonth(), day))
  }

  return days
}

function isSameDay(left: Date, right: Date): boolean {
  return toDateOnly(left) === toDateOnly(right)
}

function capitalize(value: string): string {
  return value.charAt(0).toUpperCase() + value.slice(1)
}

function extractProducts(payload: NutritionChatSearchResponse): ProductNutrition[] {
  const byKey = new Map<string, ProductNutrition>()
  for (const item of payload.items ?? []) {
    byKey.set(productKey(item), item)
  }
  for (const clarification of payload.clarifications ?? []) {
    for (const candidate of clarification.candidates ?? []) {
      byKey.set(productKey(candidate), candidate)
    }
  }
  return [...byKey.values()]
}

function productKey(product: ProductNutrition): string {
  return product.sourceReference || product.productId || product.productName
}

function defaultPortionState(product: ProductNutrition): PortionState {
  const servingSize = Number(product.servingSize)
  if (isPerServing(product) && Number.isFinite(servingSize) && servingSize > 0) {
    return { portion: 'custom', grams: servingSize }
  }

  return { portion: '100', grams: 100 }
}

function calculateFacts(product: ProductNutrition, grams: number): NutritionSummary {
  const denominator = isPerServing(product) && product.servingSize && product.servingSize > 0
    ? product.servingSize
    : 100
  const ratio = grams / denominator
  return {
    calories: round((product.nutritionFacts?.calories ?? 0) * ratio),
    protein: round((product.nutritionFacts?.protein ?? 0) * ratio),
    fat: round((product.nutritionFacts?.fat ?? 0) * ratio),
    carbs: round((product.nutritionFacts?.carbs ?? 0) * ratio),
  }
}

function setPortion(
  product: ProductNutrition,
  portion: PortionKey,
  setPortionMap: Dispatch<SetStateAction<Record<string, PortionState>>>,
) {
  setPortionMap((current) => ({
    ...current,
    [productKey(product)]: {
      portion,
      grams: portion === '100' ? 100 : portion === '50' ? 50 : current[productKey(product)]?.grams ?? defaultPortionState(product).grams,
    },
  }))
}

function setCustomGrams(
  product: ProductNutrition,
  grams: number,
  setPortionMap: Dispatch<SetStateAction<Record<string, PortionState>>>,
) {
  setPortionMap((current) => ({
    ...current,
    [productKey(product)]: {
      portion: 'custom',
      grams: Math.max(1, Number.isFinite(grams) ? grams : 1),
    },
  }))
}

function portionLabel(state: PortionState): string {
  return state.portion === '100' ? '100 г' : state.portion === '50' ? '50 г' : `${formatNumber(state.grams)} г`
}

function normalizeDay(day: ProfileDay, date: string): ProfileDay {
  return {
    ...day,
    date: day.date ?? date,
    goal: day.goal ?? null,
    meals: mealOrder.map((mealType) => {
      const found = day.meals?.find((meal) => meal.mealType === mealType)
      return {
        mealType,
        entries: (found?.entries ?? []).map(normalizeEntry),
        summary: found?.summary ?? emptySummary,
      }
    }),
    totalSummary: day.totalSummary ?? emptySummary,
  }
}

function normalizeEntry(entry: MealEntryItem): MealEntryItem {
  return {
    ...entry,
    servingGrams: entry.servingGrams ?? 0,
    portionLabel: entry.portionLabel || 'Порция не указана',
    sourceType: entry.sourceType ?? '',
    sourceReference: entry.sourceReference ?? '',
    loggedAtUtc: entry.loggedAtUtc ?? entry.createdAtUtc,
  }
}

function findMeal(day: ProfileDay | null, mealType: MealType): MealEntriesByType | null {
  return day?.meals.find((meal) => meal.mealType === mealType) ?? null
}

function addEntryToDay(day: ProfileDay | null, entry: MealEntryItem, currentMealType: MealType, date: string): ProfileDay {
  const normalized = normalizeDay(day ?? { date, goal: null, meals: [], totalSummary: emptySummary }, date)
  const meals = normalized.meals.map((meal) =>
    meal.mealType === currentMealType
      ? { ...meal, entries: [entry, ...meal.entries], summary: sumEntries([entry, ...meal.entries]) }
      : meal,
  )
  return { ...normalized, meals, totalSummary: sumEntries(meals.flatMap((meal) => meal.entries)) }
}

function removeEntryFromDay(day: ProfileDay | null, entryId: string, date: string): ProfileDay | null {
  if (!day) return day
  const normalized = normalizeDay(day, date)
  const meals = normalized.meals.map((meal) => {
    const entries = meal.entries.filter((entry) => entry.id !== entryId)
    return { ...meal, entries, summary: sumEntries(entries) }
  })
  return { ...normalized, meals, totalSummary: sumEntries(meals.flatMap((meal) => meal.entries)) }
}

function replaceEntryInDay(day: ProfileDay | null, entry: MealEntryItem, date: string): ProfileDay | null {
  if (!day) return day
  const normalized = normalizeDay(day, date)
  const meals = normalized.meals.map((meal) => {
    const entries = meal.entries.map((item) => (item.id === entry.id ? entry : item))
    return { ...meal, entries, summary: sumEntries(entries) }
  })
  return { ...normalized, meals, totalSummary: sumEntries(meals.flatMap((meal) => meal.entries)) }
}

function sumEntries(entries: MealEntryItem[]): NutritionSummary {
  return entries.reduce(
    (total, entry) => ({
      calories: round(total.calories + entry.calories),
      protein: round(total.protein + entry.protein),
      fat: round(total.fat + entry.fat),
      carbs: round(total.carbs + entry.carbs),
    }),
    emptySummary,
  )
}

function mealTarget(goal: DailyGoal | null | undefined, mealType: MealType): number {
  const calories = goal?.targetCalories ?? 2300
  const weights: Record<MealType, number> = {
    Breakfast: mealPercent(goal?.breakfastPercent, 25) / 100,
    Lunch: mealPercent(goal?.lunchPercent, 35) / 100,
    Dinner: mealPercent(goal?.dinnerPercent, 30) / 100,
    Snack: mealPercent(goal?.snackPercent, 10) / 100,
  }
  return Math.round(calories * weights[mealType])
}

function mealPercent(value: number | null | undefined, fallback: number): number {
  return Number.isFinite(value) && value !== null && value !== undefined ? value : fallback
}

function SourceLink({ product }: { product: ProductNutrition }) {
  const label = product.sourceType === 'WebSearch' ? 'Поиск в интернете' : product.sourceType || 'OpenFoodFacts'
  const url = toHttpUrl(product.sourceReference)

  if (!url) {
    return <span>Источник: {label}</span>
  }

  return (
    <span>
      Источник:{' '}
      <a href={url} target="_blank" rel="noreferrer">
        {label}: {formatSourceHost(url)}
      </a>
    </span>
  )
}

function formatNutritionBasis(product: ProductNutrition): string {
  const size = product.servingSize && product.servingSize > 0 ? product.servingSize : null
  const unit = product.servingUnit || 'г'

  if (product.nutritionValueBasis === 'PerServing') {
    return size ? `КБЖУ на порцию ${formatNumber(size)} ${unit}` : 'КБЖУ на порцию'
  }

  if (product.nutritionValueBasis === 'Per100Milliliters') {
    return 'КБЖУ на 100 мл'
  }

  return 'КБЖУ на 100 г'
}

function isPerServing(product: ProductNutrition): boolean {
  return product.nutritionValueBasis === 'PerServing'
}

function toHttpUrl(value: string): string | null {
  try {
    const url = new URL(value)
    return url.protocol === 'http:' || url.protocol === 'https:' ? url.toString() : null
  } catch {
    return null
  }
}

function formatSourceHost(url: string): string {
  try {
    return new URL(url).hostname.replace(/^www\./, '')
  } catch {
    return url
  }
}

function formatEntrySource(entry: MealEntryItem): string {
  if (entry.sourceType && entry.sourceReference) return `Источник: ${entry.sourceType} · ${entry.sourceReference}`
  if (entry.sourceType) return `Источник: ${entry.sourceType}`
  if (entry.sourceReference) return `Источник: ${entry.sourceReference}`
  return 'Источник: ручной ввод или AI estimate'
}

function EntrySourceLink({ entry }: { entry: MealEntryItem }) {
  const label = entry.sourceType === 'WebSearch' ? 'Поиск в интернете' : entry.sourceType || 'OpenFoodFacts'
  const url = toHttpUrl(entry.sourceReference)

  if (!url) {
    return <span>{formatEntrySource(entry)}</span>
  }

  return (
    <span>
      Источник:{' '}
      <a href={url} target="_blank" rel="noreferrer">
        {label}: {formatSourceHost(url)}
      </a>
    </span>
  )
}

function referenceBasisLabel(entry: MealEntryItem): string {
  const sourceType = entry.sourceType.toLowerCase()
  if (entry.servingGrams <= 0) {
    return 'Основа расчёта не определена — проверьте граммовку'
  }
  if (sourceType.includes('manual')) {
    return 'Основа расчёта: ручной ввод'
  }
  if (sourceType.includes('ai')) {
    return 'Основа расчёта: AI-оценка на 100 г'
  }
  if (!entry.sourceType && !entry.sourceReference) {
    return 'Основа расчёта: ручной ввод'
  }
  if (entry.servingGrams === 100) {
    return 'Основа расчёта: на 100 г'
  }
  return `Основа расчёта: выбранная порция = ${formatNumber(entry.servingGrams)} г`
}

function pluralize(value: number, one: string, few: string, many: string): string {
  const mod10 = value % 10
  const mod100 = value % 100
  if (mod10 === 1 && mod100 !== 11) return one
  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return few
  return many
}

function formatTime(date: Date): string {
  return new Intl.DateTimeFormat('ru-RU', { hour: '2-digit', minute: '2-digit' }).format(date)
}

function formatNumber(value: number): string {
  return Number.isInteger(value) ? value.toString() : value.toFixed(1)
}

function round(value: number): number {
  return Math.round(value * 10) / 10
}

function initials(user: CurrentUser): string {
  return `${user.firstName.charAt(0)}${user.secondName.charAt(0)}`.toUpperCase()
}
