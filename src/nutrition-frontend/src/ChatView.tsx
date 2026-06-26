import { useEffect, useRef } from 'react'
import type { ChatMessage } from './types'
import './ChatView.css'

type Props = {
  messages: ChatMessage[]
  loading: boolean
}

export function ChatView({ messages, loading }: Props) {
  const bottomRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages, loading])

  return (
    <div className="chat-view">
      {messages.length === 0 && (
        <div className="chat-empty">
          <div className="chat-empty-icon">N</div>
          <h2>Поиск Nutrition продуктов</h2>
          <p>
            Введите название продукта, прикрепите фото этикетки или запишите голосовое сообщение
          </p>
        </div>
      )}

      {messages.map((msg) => (
        <MessageBubble key={msg.id} message={msg} />
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

function MessageBubble({ message }: { message: ChatMessage }) {
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
          <span className="voice-duration">
            {formatDuration(message.audio.duration)}
          </span>
          <audio controls src={message.audio.url} className="voice-player" />
        </div>
      )

    case 'assistant-result':
      return (
        <div className="chat-bubble assistant">
          <div className="bubble-header">
            Результаты поиска: &ldquo;{message.query}&rdquo;
          </div>
          {message.error ? (
            <div className="bubble-error">{message.error}</div>
          ) : (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Название</th>
                    <th>Бренд</th>
                    <th>Ккал</th>
                    <th>Белки</th>
                    <th>Жиры</th>
                    <th>Углеводы</th>
                    <th>Источник</th>
                    <th>Conf.</th>
                  </tr>
                </thead>
                <tbody>
                  {message.items.map((item, idx) => (
                    <tr key={`${item.productId ?? idx}-${item.sourceReference ?? idx}`}>
                      <td>{item.productName}</td>
                      <td>{item.brand ?? '-'}</td>
                      <td>{item.nutritionFacts?.calories ?? '-'}</td>
                      <td>{item.nutritionFacts?.protein ?? '-'}</td>
                      <td>{item.nutritionFacts?.fat ?? '-'}</td>
                      <td>{item.nutritionFacts?.carbs ?? '-'}</td>
                      <td>{item.sourceType}</td>
                      <td>
                        {typeof item.confidenceScore === 'number'
                          ? item.confidenceScore.toFixed(2)
                          : '-'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )

    default:
      return null
  }
}

function formatDuration(seconds: number): string {
  const m = Math.floor(seconds / 60)
  const s = Math.floor(seconds % 60)
  return `${m}:${s.toString().padStart(2, '0')}`
}
