import { useEffect, useMemo, useRef, useState } from 'react'
import type { ChatMessage, MealEditContext, NutritionClarification, ProductNutrition } from './types'
import './ChatView.css'

type Props = {
  messages: ChatMessage[]
  loading: boolean
  onResolveClarification: (
    messageId: string,
    clarificationId: string,
    product: ProductNutrition,
  ) => void
  onCancelClarification: (messageId: string, clarificationId: string) => void
  onManualClarification: (
    messageId: string,
    clarificationId: string,
    query: string,
  ) => void
  onSaveProduct: (product: ProductNutrition) => void
  onSetActiveClarification: (messageId: string, index: number) => void
  mealEditContext: MealEditContext | null
}

export function ChatView({
  messages,
  loading,
  onResolveClarification,
  onCancelClarification,
  onManualClarification,
  onSaveProduct,
  onSetActiveClarification,
  mealEditContext,
}: Props) {
  const bottomRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages, loading])

  return (
    <div className="chat-view">
      {mealEditContext && (
        <div className="edit-mode-banner">
          <strong>Редактирование приёма</strong>
          <span>{formatMealType(mealEditContext.mealType)} · {mealEditContext.mealEntryId}</span>
        </div>
      )}

      {messages.length === 0 && (
        <div className="chat-empty">
          <div className="chat-empty-icon">N</div>
          <h2>Поиск Nutrition продуктов</h2>
          <p>Введите блюдо или продукт, а я помогу подобрать КБЖУ на 100 г.</p>
        </div>
      )}

      {messages.map((msg) => (
        <MessageBubble
          key={msg.id}
          message={msg}
          onResolveClarification={onResolveClarification}
          onCancelClarification={onCancelClarification}
          onManualClarification={onManualClarification}
          onSaveProduct={onSaveProduct}
          onSetActiveClarification={onSetActiveClarification}
          mealEditContext={mealEditContext}
        />
      ))}

      {loading && (
        <div className="chat-bubble assistant">
          <div className="typing-indicator">
            <span />
            <span />
            <span />
          </div>
        </div>
      )}

      <div ref={bottomRef} />
    </div>
  )
}

function MessageBubble({
  message,
  onResolveClarification,
  onCancelClarification,
  onManualClarification,
  onSaveProduct,
  onSetActiveClarification,
  mealEditContext,
}: {
  message: ChatMessage
  onResolveClarification: Props['onResolveClarification']
  onCancelClarification: Props['onCancelClarification']
  onManualClarification: Props['onManualClarification']
  onSaveProduct: Props['onSaveProduct']
  onSetActiveClarification: Props['onSetActiveClarification']
  mealEditContext: MealEditContext | null
}) {
  switch (message.kind) {
    case 'user-text':
      return (
        <div className="chat-bubble user">
          <div className="bubble-text">{message.text}</div>
          {message.attachments.length > 0 && (
            <div className="bubble-attachments">
              {message.attachments.map((att) => (
                <img
                  key={att.id}
                  src={att.previewUrl}
                  alt={att.file.name}
                  className="bubble-attachment-img"
                />
              ))}
            </div>
          )}
        </div>
      )

    case 'user-voice':
      return (
        <div className="chat-bubble user voice">
          <span className="voice-icon">Voice</span>
          <span className="voice-duration">{formatDuration(message.audio.duration)}</span>
          <audio controls src={message.audio.url} className="voice-player" />
        </div>
      )

    case 'assistant-result': {
      const hasClarifications = message.clarifications.length > 0
      const activeIndex = Math.min(
        message.activeClarificationIndex,
        Math.max(message.clarifications.length - 1, 0),
      )
      const activeClarification = message.clarifications[activeIndex]
      const allClarificationsClosed =
        hasClarifications &&
        message.clarifications.every(
          (clarification) =>
            clarification.status === 'answered' || clarification.status === 'cancelled',
        )

      return (
        <div className="chat-bubble assistant result-bubble">
          <div className="bubble-header">Результат: &ldquo;{message.query}&rdquo;</div>
          {message.error ? (
            <div className="bubble-error">{message.error}</div>
          ) : (
            <>
              {allClarificationsClosed ? (
                <FinalSelectionSummary clarifications={message.clarifications} />
              ) : hasClarifications ? (
                <ClarificationSummary
                  clarifications={message.clarifications}
                  activeIndex={activeIndex}
                  onSelect={(index) => onSetActiveClarification(message.id, index)}
                />
              ) : null}

              {!allClarificationsClosed && message.items.length > 0 && (
                <ProductList
                  items={message.items}
                  onSaveProduct={onSaveProduct}
                  mealEditContext={mealEditContext}
                />
              )}

              {!allClarificationsClosed && hasClarifications && activeClarification && (
                <ClarificationPanel
                  messageId={message.id}
                  clarification={activeClarification}
                  clarifications={message.clarifications}
                  activeIndex={activeIndex}
                  onResolveClarification={onResolveClarification}
                  onCancelClarification={onCancelClarification}
                  onManualClarification={onManualClarification}
                  onSetActiveClarification={onSetActiveClarification}
                />
              )}
            </>
          )}
        </div>
      )
    }

    default:
      return null
  }
}

function ClarificationSummary({
  clarifications,
  activeIndex,
  onSelect,
}: {
  clarifications: NutritionClarification[]
  activeIndex: number
  onSelect: (index: number) => void
}) {
  return (
    <div className="answer-summary">
      {clarifications.map((item, index) => (
        <button
          key={item.id}
          type="button"
          className={`answer-summary-row ${index === activeIndex ? 'active' : ''} ${item.status}`}
          onClick={() => onSelect(index)}
        >
          <span className="answer-summary-title">{item.parsedProductName}</span>
          <span className="answer-summary-state">
            {item.status === 'answered'
              ? item.selectedProduct?.productName ?? 'Выбрано'
              : item.status === 'cancelled'
                ? 'Отменено'
                : item.status === 'refining'
                  ? 'Уточняю варианты'
                : 'Требует ответа'}
          </span>
        </button>
      ))}
    </div>
  )
}

function FinalSelectionSummary({
  clarifications,
}: {
  clarifications: NutritionClarification[]
}) {
  return (
    <div className="final-selection-list">
      {clarifications.map((clarification) => {
        const product = clarification.selectedProduct

        return (
          <div key={clarification.id} className={`final-selection-row ${clarification.status}`}>
            <span className="final-selection-source">{clarification.parsedProductName}</span>
            <span className="final-selection-choice">
              {product ? (
                <>
                  <strong>{product.productName}</strong>
                  <span>{product.brand || 'Бренд не указан'}</span>
                </>
              ) : (
                <strong>Отменено</strong>
              )}
            </span>
            <span className="final-selection-macros">
              {product ? formatMacroSlash(product) : '-/-/-/-'}
            </span>
          </div>
        )
      })}
    </div>
  )
}

function ProductList({
  items,
  onSaveProduct,
  mealEditContext,
}: {
  items: ProductNutrition[]
  onSaveProduct: (product: ProductNutrition) => void
  mealEditContext: MealEditContext | null
}) {
  return (
    <div className="product-list">
      {items.map((item, idx) => (
        <div key={`${item.productId}-${item.sourceReference}-${idx}`} className="product-save-row">
          <ProductSummary product={item} />
          <button type="button" onClick={() => onSaveProduct(item)}>
            {mealEditContext ? 'Обновить приём' : 'Добавить в профиль'}
          </button>
        </div>
      ))}
    </div>
  )
}

function ClarificationPanel({
  messageId,
  clarification,
  clarifications,
  activeIndex,
  onResolveClarification,
  onCancelClarification,
  onManualClarification,
  onSetActiveClarification,
}: {
  messageId: string
  clarification: NutritionClarification
  clarifications: NutritionClarification[]
  activeIndex: number
  onResolveClarification: Props['onResolveClarification']
  onCancelClarification: Props['onCancelClarification']
  onManualClarification: Props['onManualClarification']
  onSetActiveClarification: Props['onSetActiveClarification']
}) {
  const [selectedValue, setSelectedValue] = useState<string>('')
  const [manualQuery, setManualQuery] = useState('')
  const isManual = selectedValue === 'manual'
  const candidateKey = clarification.candidates
    .map((candidate) => candidate.productId)
    .join('|')
  const isRefining = clarification.status === 'refining'

  useEffect(() => {
    setSelectedValue(clarification.selectedProduct?.productId ?? '')
    setManualQuery('')
  }, [clarification.id, clarification.selectedProduct?.productId, clarification.status, candidateKey])

  const selectedProduct = useMemo(
    () => clarification.candidates.find((candidate) => candidate.productId === selectedValue),
    [clarification.candidates, selectedValue],
  )
  const completedCount = clarifications.filter(
    (item) => item.status === 'answered' || item.status === 'cancelled',
  ).length

  function handleAdd() {
    if (isManual) {
      onManualClarification(messageId, clarification.id, manualQuery)
      return
    }

    if (selectedProduct) {
      onResolveClarification(messageId, clarification.id, selectedProduct)
    }
  }

  return (
    <div className={`clarification-panel ${isRefining ? 'is-refining' : ''}`}>
      <div className="clarification-progress">
        <span>{completedCount} из {clarifications.length} закрыто</span>
        <div className="clarification-dots" aria-label="Статус уточнений">
          {clarifications.map((item, index) => (
            <button
              key={item.id}
              type="button"
              className={`clarification-dot ${index === activeIndex ? 'active' : ''} ${item.status}`}
              onClick={() => onSetActiveClarification(messageId, index)}
              aria-label={`${index + 1}: ${item.status}`}
            />
          ))}
        </div>
      </div>

      <div className="clarification-question">{clarification.question}</div>

      {isRefining && (
        <div className="refining-answer">
          <span>Подбираю новые варианты для вашего уточнения...</span>
        </div>
      )}

      {clarification.status === 'answered' && clarification.selectedProduct && (
        <div className="selected-answer">
          <span>Выбрано</span>
          <ProductSummary product={clarification.selectedProduct} compact />
        </div>
      )}

      {clarification.status === 'cancelled' && (
        <div className="cancelled-answer">
          <span>Этот товар отменён</span>
        </div>
      )}

      <fieldset className="clarification-options" disabled={clarification.status !== 'pending'}>
        {clarification.candidates.map((candidate) => (
          <label key={candidate.productId} className="clarification-option">
            <input
              type="radio"
              name={clarification.id}
              value={candidate.productId}
              checked={selectedValue === candidate.productId}
              onChange={(event) => setSelectedValue(event.target.value)}
            />
            <ProductSummary product={candidate} compact />
          </label>
        ))}

        <div className={`clarification-option manual-option ${isManual ? 'manual-active' : ''}`}>
          <input
            type="radio"
            name={clarification.id}
            value="manual"
            checked={isManual}
            onChange={(event) => setSelectedValue(event.target.value)}
            aria-label="Напишу уточнение сам"
          />
          <div className="manual-option-body">
            <strong>Напишу уточнение сам</strong>
            <span>Лучше указать тип, бренд, форму, готовность или упаковку.</span>
            <input
              type="text"
              value={manualQuery}
              onFocus={() => setSelectedValue('manual')}
              onChange={(event) => setManualQuery(event.target.value)}
              placeholder="Например: макароны Barilla fusilli, сухие"
            />
          </div>
        </div>
      </fieldset>

      <div className="clarification-actions">
        <button
          type="button"
          className="cancel-btn"
          onClick={() => onCancelClarification(messageId, clarification.id)}
          disabled={clarification.status !== 'pending'}
        >
          Отмена
        </button>
        <button
          type="button"
          className="add-btn"
          onClick={handleAdd}
          disabled={
            clarification.status !== 'pending' ||
            (!selectedProduct && !isManual) ||
            (isManual && manualQuery.trim().length === 0)
          }
        >
          Добавить
        </button>
      </div>
    </div>
  )
}

function ProductSummary({
  product,
  compact = false,
}: {
  product: ProductNutrition
  compact?: boolean
}) {
  return (
    <div className={`product-summary ${compact ? 'compact' : ''}`}>
      <div className="product-title">
        <strong>{product.productName}</strong>
        <span>{product.brand || 'Бренд не указан'}</span>
      </div>
      <div className="macro-grid">
        <Macro label="к" value={product.nutritionFacts?.calories} />
        <Macro label="б" value={product.nutritionFacts?.protein} />
        <Macro label="ж" value={product.nutritionFacts?.fat} />
        <Macro label="у" value={product.nutritionFacts?.carbs} />
      </div>
    </div>
  )
}

function Macro({ label, value }: { label: string; value?: number }) {
  return (
    <span className="macro-pill">
      <span>{label}</span>
      <strong>{typeof value === 'number' ? formatNumber(value) : '-'}</strong>
    </span>
  )
}

function formatNumber(value: number): string {
  return Number.isInteger(value) ? value.toString() : value.toFixed(1)
}

function formatMacroSlash(product: ProductNutrition): string {
  const facts = product.nutritionFacts
  return [
    facts?.calories,
    facts?.protein,
    facts?.fat,
    facts?.carbs,
  ]
    .map((value) => (typeof value === 'number' ? formatNumber(value) : '-'))
    .join('/')
}

function formatDuration(seconds: number): string {
  const m = Math.floor(seconds / 60)
  const s = Math.floor(seconds % 60)
  return `${m}:${s.toString().padStart(2, '0')}`
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
