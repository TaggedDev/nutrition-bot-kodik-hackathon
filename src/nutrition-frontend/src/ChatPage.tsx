import { useMemo, useState } from 'react'
import type { CSSProperties, FormEvent } from 'react'
import './ChatPage.css'

type PortionKey = '1/2' | '1/3' | 'Custom'

type FoodResult = {
  id: string
  title: string
  weight: number
  description: string
  source: string
  calories: number
  protein: number
  fat: number
  carbs: number
  selectedPortion?: PortionKey
  selectedGrams?: number
}

type MealItem = {
  id: string
  title: string
  portion: string
  grams: number
  calories: number
  protein: number
  fat: number
  carbs: number
}

type ChatLine =
  | { id: string; role: 'user'; text: string; time: string }
  | { id: string; role: 'assistant'; kind: 'intro' | 'search' | 'confirmed' | 'fake'; text: string }

const searchResults: FoodResult[] = [
  {
    id: 'tanuki-arigato-12',
    title: 'Tanuki - Arigato Set (12 pcs)',
    weight: 550,
    description: 'Set of rolls',
    source: 'Tanuki.ru',
    calories: 960,
    protein: 36,
    fat: 32,
    carbs: 102,
    selectedPortion: '1/2',
    selectedGrams: 275,
  },
  {
    id: 'tanuki-arigato-8',
    title: 'Tanuki - Arigato Set (8 pcs)',
    weight: 420,
    description: 'Set of rolls',
    source: 'Tanuki.ru',
    calories: 720,
    protein: 28,
    fat: 24,
    carbs: 76,
  },
  {
    id: 'tanuki-arigato-16',
    title: 'Tanuki - Arigato Set (16 pcs)',
    weight: 680,
    description: 'Set of rolls',
    source: 'Tanuki.ru',
    calories: 1240,
    protein: 48,
    fat: 40,
    carbs: 132,
  },
]

const initialMealItems: MealItem[] = [
  {
    id: 'meal-arigato',
    title: 'Tanuki - Arigato Set (12 pcs)',
    portion: '1/2',
    grams: 275,
    calories: 480,
    protein: 18,
    fat: 16,
    carbs: 51,
  },
  {
    id: 'meal-chicken',
    title: 'Boiled chicken breast',
    portion: '200 g',
    grams: 200,
    calories: 330,
    protein: 41,
    fat: 7,
    carbs: 0,
  },
  {
    id: 'meal-curd',
    title: 'Curd Glazed Bar "Baya Alexander"',
    portion: '50 g',
    grams: 50,
    calories: 180,
    protein: 0,
    fat: 3,
    carbs: 48,
  },
]

const initialLines: ChatLine[] = [
  {
    id: 'm1',
    role: 'user',
    text: 'I ate half of the Arigato set at Tanuki',
    time: '10:31',
  },
  {
    id: 'm2',
    role: 'assistant',
    kind: 'intro',
    text: 'I found several options for "Arigato Set" at Tanuki. Which one matches your meal?',
  },
  {
    id: 'm3',
    role: 'assistant',
    kind: 'search',
    text: '',
  },
  {
    id: 'm4',
    role: 'user',
    text: 'Choose the first option, half portion',
    time: '10:33',
  },
  {
    id: 'm5',
    role: 'assistant',
    kind: 'confirmed',
    text: 'Great. I added it to the current meal.',
  },
]

const navItems = ['Dashboard', 'Chat', 'Meals', 'History', 'Statistics', 'Settings']
const portions: PortionKey[] = ['1/2', '1/3', 'Custom']

export function ChatPage() {
  const [lines, setLines] = useState<ChatLine[]>(initialLines)
  const [mealItems, setMealItems] = useState<MealItem[]>(initialMealItems)
  const [input, setInput] = useState('')
  const [selectedMap, setSelectedMap] = useState<Record<string, { portion: PortionKey; grams: number; selected: boolean }>>(
    () =>
      Object.fromEntries(
        searchResults.map((item) => [
          item.id,
          {
            portion: item.selectedPortion ?? '1/2',
            grams: item.selectedGrams ?? Math.round(item.weight / 2),
            selected: item.id === 'tanuki-arigato-12',
          },
        ]),
      ),
  )

  const totals = useMemo(
    () =>
      mealItems.reduce(
        (acc, item) => ({
          calories: acc.calories + item.calories,
          protein: acc.protein + item.protein,
          fat: acc.fat + item.fat,
          carbs: acc.carbs + item.carbs,
        }),
        { calories: 0, protein: 0, fat: 0, carbs: 0 },
      ),
    [mealItems],
  )

  function updatePortion(item: FoodResult, portion: PortionKey) {
    const grams =
      portion === '1/2'
        ? Math.round(item.weight / 2)
        : portion === '1/3'
          ? Math.round(item.weight / 3)
          : selectedMap[item.id]?.grams ?? item.weight

    setSelectedMap((current) => ({
      ...current,
      [item.id]: { ...current[item.id], portion, grams, selected: current[item.id]?.selected ?? false },
    }))
  }

  function updateGrams(item: FoodResult, grams: number) {
    setSelectedMap((current) => ({
      ...current,
      [item.id]: {
        ...current[item.id],
        portion: 'Custom',
        grams: Math.max(1, grams),
        selected: current[item.id]?.selected ?? false,
      },
    }))
  }

  function handleSelect(item: FoodResult) {
    const state = selectedMap[item.id]
    const calculated = calculateFood(item, state.grams)
    const nextItem: MealItem = {
      id: `meal-${item.id}-${Date.now()}`,
      title: item.title,
      portion: state.portion,
      grams: state.grams,
      ...calculated,
    }

    setMealItems((current) => [nextItem, ...current])
    setSelectedMap((current) => ({
      ...current,
      [item.id]: { ...current[item.id], selected: true },
    }))
    setLines((current) => [
      ...current,
      {
        id: crypto.randomUUID(),
        role: 'assistant',
        kind: 'confirmed',
        text: 'Added to the current meal.',
      },
    ])
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const text = input.trim()
    if (!text) return

    setLines((current) => [
      ...current,
      { id: crypto.randomUUID(), role: 'user', text, time: '10:42' },
      {
        id: crypto.randomUUID(),
        role: 'assistant',
        kind: 'fake',
        text: 'I am looking for matching foods and will ask you to choose the closest result.',
      },
    ])
    setInput('')
  }

  function resetDemo() {
    setLines([])
  }

  return (
    <main className="nutri-page">
      <aside className="nutri-sidebar" aria-label="Main navigation">
        <div className="brand-block">
          <div className="brand-mark">N</div>
          <div>
            <strong>NutriMate AI</strong>
            <span>AI-powered calorie tracker</span>
          </div>
        </div>

        <nav className="sidebar-nav">
          {navItems.map((item) => (
            <button key={item} type="button" className={item === 'Chat' ? 'active' : ''} aria-current={item === 'Chat' ? 'page' : undefined}>
              <span className="nav-icon" aria-hidden="true">{item.charAt(0)}</span>
              {item}
            </button>
          ))}
        </nav>

        <div className="sidebar-spacer" />

        <section className="premium-card" aria-label="NutriMate Premium">
          <span className="premium-kicker">Unlock more with</span>
          <strong>NutriMate Premium</strong>
          <p>Advanced insights, custom goals and more.</p>
          <button type="button">Upgrade now</button>
        </section>

        <button type="button" className="user-card" aria-label="Open user menu">
          <span className="avatar">JD</span>
          <span>
            <strong>Jane Doe</strong>
            <small>Premium</small>
          </span>
          <span className="chevron">⌄</span>
        </button>
      </aside>

      <section className="chat-workspace" aria-label="Nutrition chat">
        <header className="chat-header">
          <div className="assistant-title">
            <span className="ai-icon">AI</span>
            <div>
              <h1>AI Assistant <span>NutriMate AI</span></h1>
              <p>Your personal nutrition assistant</p>
            </div>
          </div>
          <div className="header-actions">
            <button type="button" className="clear-btn" onClick={resetDemo} aria-label="Clear chat">
              Clear chat
            </button>
            <button type="button" className="icon-btn" aria-label="Open menu">...</button>
          </div>
        </header>

        <div className="conversation">
          {lines.length === 0 ? (
            <div className="empty-chat">
              <span>AI</span>
              <h2>Tell me what you ate</h2>
              <p>NutriMate will search foods, keep grams visible, and let you choose the exact portion.</p>
            </div>
          ) : (
            lines.map((line) => (
              <MessageRow
                key={line.id}
                line={line}
                selectedMap={selectedMap}
                onPortion={updatePortion}
                onGrams={updateGrams}
                onSelect={handleSelect}
              />
            ))
          )}
        </div>

        <form className="chat-input-bar" onSubmit={handleSubmit}>
          <div className="input-row">
            <button type="button" className="attach-btn" aria-label="Attach file">+</button>
            <label className="chat-input-shell">
              <span className="sr-only">Message</span>
              <input
                value={input}
                onChange={(event) => setInput(event.target.value)}
                placeholder="Write a message..."
              />
            </label>
            <button type="submit" className="send-btn" aria-label="Send message">›</button>
          </div>
          <div className="input-helper">
            <span>For example: I ate 200 g of chicken breast</span>
            <span>NutriMate AI can make mistakes. Check important information.</span>
          </div>
        </form>
      </section>

      <MealContextPanel
        items={mealItems}
        totals={totals}
        onRemove={(id) => setMealItems((current) => current.filter((item) => item.id !== id))}
      />
    </main>
  )
}

function MessageRow({
  line,
  selectedMap,
  onPortion,
  onGrams,
  onSelect,
}: {
  line: ChatLine
  selectedMap: Record<string, { portion: PortionKey; grams: number; selected: boolean }>
  onPortion: (item: FoodResult, portion: PortionKey) => void
  onGrams: (item: FoodResult, grams: number) => void
  onSelect: (item: FoodResult) => void
}) {
  if (line.role === 'user') {
    return (
      <div className="message-row user-row">
        <div className="user-bubble">
          <span>{line.text}</span>
          <small>{line.time} ✓✓</small>
        </div>
      </div>
    )
  }

  return (
    <div className="message-row assistant-row">
      <div className="bot-avatar">AI</div>
      <div className="assistant-content">
        {line.text && <p>{line.text}</p>}
        {line.kind === 'search' && (
          <SearchResultsGroup
            selectedMap={selectedMap}
            onPortion={onPortion}
            onGrams={onGrams}
            onSelect={onSelect}
          />
        )}
        {line.kind === 'confirmed' && <ConfirmedFoodCard />}
      </div>
    </div>
  )
}

function SearchResultsGroup({
  selectedMap,
  onPortion,
  onGrams,
  onSelect,
}: {
  selectedMap: Record<string, { portion: PortionKey; grams: number; selected: boolean }>
  onPortion: (item: FoodResult, portion: PortionKey) => void
  onGrams: (item: FoodResult, grams: number) => void
  onSelect: (item: FoodResult) => void
}) {
  return (
    <div className="search-results-group">
      {searchResults.map((item, index) => (
        <FoodSearchResultCard
          key={item.id}
          item={item}
          index={index}
          state={selectedMap[item.id]}
          onPortion={onPortion}
          onGrams={onGrams}
          onSelect={onSelect}
        />
      ))}
      <div className="clarify-row">
        <span>Do not see the right option?</span>
        <button type="button">Clarify</button>
      </div>
    </div>
  )
}

function FoodSearchResultCard({
  item,
  index,
  state,
  onPortion,
  onGrams,
  onSelect,
}: {
  item: FoodResult
  index: number
  state: { portion: PortionKey; grams: number; selected: boolean }
  onPortion: (item: FoodResult, portion: PortionKey) => void
  onGrams: (item: FoodResult, grams: number) => void
  onSelect: (item: FoodResult) => void
}) {
  const calculated = calculateFood(item, state.grams)

  return (
    <article className={`food-card ${state.selected ? 'is-selected' : ''}`}>
      <FoodThumb index={index} title={item.title} />
      <div className="food-info">
        <h3>{item.title}</h3>
        <p>{item.weight} g • {item.description}</p>
        <span>Source: {item.source} ⓘ</span>
      </div>
      <div className="macro-block" aria-label="Nutrition facts">
        <strong>{calculated.calories} kcal</strong>
        <span>P {calculated.protein} g</span>
        <span>F {calculated.fat} g</span>
        <span>C {calculated.carbs} g</span>
      </div>
      <div className="portion-controls">
        <div className="portion-buttons">
          {portions.map((portion) => (
            <button
              key={portion}
              type="button"
              className={state.portion === portion ? 'active' : ''}
              onClick={() => onPortion(item, portion)}
              aria-pressed={state.portion === portion}
            >
              {portion}
            </button>
          ))}
        </div>
        <input
          aria-label={`Grams for ${item.title}`}
          type="number"
          value={state.grams}
          min={1}
          onChange={(event) => onGrams(item, Number(event.target.value))}
        />
        <button type="button" className="select-btn" onClick={() => onSelect(item)}>
          {state.selected ? 'Selected' : 'Select'}
        </button>
      </div>
    </article>
  )
}

function ConfirmedFoodCard() {
  return (
    <article className="confirmed-card">
      <div className="confirmed-top">
        <FoodThumb index={0} title="Tanuki - Arigato Set (12 pcs)" />
        <div>
          <h3>Tanuki - Arigato Set (12 pcs)</h3>
          <p>Portion: 1/2 (275 g)</p>
          <span>Source: Tanuki.ru</span>
        </div>
        <span className="success-badge">Added</span>
      </div>
      <div className="confirmed-grid">
        <div>
          <span>Found (1/2 serving)</span>
          <strong>480 kcal</strong>
          <small>P 18 g   F 16 g   C 51 g</small>
        </div>
        <div>
          <span>You specified</span>
          <strong>~275 g</strong>
          <small>User-controlled grams</small>
        </div>
      </div>
      <button type="button">Change portion</button>
    </article>
  )
}

function MealContextPanel({
  items,
  totals,
  onRemove,
}: {
  items: MealItem[]
  totals: { calories: number; protein: number; fat: number; carbs: number }
  onRemove: (id: string) => void
}) {
  const remaining = Math.max(0, 2300 - (420 + totals.calories + 120))

  return (
    <aside className="meal-panel" aria-label="Current meal">
      <header className="meal-panel-header">
        <div>
          <span className="meal-icon">B</span>
          <strong>Current meal</strong>
        </div>
        <div>
          <button type="button">Lunch ⌄</button>
          <button type="button" aria-label="Meal menu">⋮</button>
        </div>
      </header>

      <div className="basket-list">
        {items.map((item, index) => (
          <article key={item.id} className="basket-item">
            <FoodThumb index={index} title={item.title} small />
            <div>
              <h3>{item.title}</h3>
              <span>{item.portion} ({item.grams} g)</span>
            </div>
            <strong>{item.calories} kcal</strong>
            <button type="button" onClick={() => onRemove(item.id)} aria-label={`Remove ${item.title}`}>×</button>
          </article>
        ))}
      </div>

      <section className="meal-total-card">
        <div className="total-head">
          <span>Total</span>
          <strong>{totals.calories} kcal</strong>
        </div>
        <div className="macro-total-row">
          <MacroDot label="P" value={totals.protein} kind="protein" />
          <MacroDot label="F" value={totals.fat} kind="fat" />
          <MacroDot label="C" value={totals.carbs} kind="carbs" />
        </div>
      </section>

      <section className="today-card">
        <div className="section-head">
          <strong>Today</strong>
          <button type="button">Details</button>
        </div>
        <ProgressRow label="Breakfast" value={420} max={500} />
        <ProgressRow label="Lunch" value={totals.calories} max={700} />
        <ProgressRow label="Dinner" value={0} max={700} />
        <ProgressRow label="Snack" value={120} max={300} />
      </section>

      <section className="recommend-card">
        <span>Recommendations</span>
        <h2>Calories left</h2>
        <div className="recommend-main">
          <div>
            <strong>{remaining.toLocaleString('en-US')} kcal</strong>
            <small>of 2,300 kcal</small>
          </div>
          <Donut value={53} />
        </div>
        <MacroProgress label="Protein" value={101} max={150} />
        <MacroProgress label="Fat" value={44} max={77} />
        <MacroProgress label="Carbs" value={151} max={288} />
      </section>
    </aside>
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
      <i /> {label} {value} g
    </span>
  )
}

function ProgressRow({ label, value, max }: { label: string; value: number; max: number }) {
  const pct = Math.min(100, Math.round((value / max) * 100))

  return (
    <div className="progress-row">
      <div>
        <span>{label}</span>
        <strong>{value} / {max} kcal</strong>
      </div>
      <div className="progress-track">
        <span style={{ width: `${pct}%` }} />
      </div>
    </div>
  )
}

function Donut({ value }: { value: number }) {
  return (
    <div className="donut" style={{ '--donut-value': `${value * 3.6}deg` } as CSSProperties}>
      <span>{value}%</span>
    </div>
  )
}

function MacroProgress({ label, value, max }: { label: string; value: number; max: number }) {
  return (
    <div className="macro-progress">
      <div>
        <span>{label}</span>
        <strong>{value} / {max} g</strong>
      </div>
      <div className="progress-track">
        <span style={{ width: `${Math.min(100, (value / max) * 100)}%` }} />
      </div>
    </div>
  )
}

function calculateFood(item: FoodResult, grams: number) {
  const ratio = grams / item.weight
  return {
    calories: Math.round(item.calories * ratio),
    protein: Math.round(item.protein * ratio),
    fat: Math.round(item.fat * ratio),
    carbs: Math.round(item.carbs * ratio),
  }
}
