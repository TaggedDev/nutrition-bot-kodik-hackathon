import { useRef, useCallback, useEffect } from 'react'
import type { Attachment, RecordingState } from './types'
import { AttachmentsPreview } from './AttachmentsPreview'
import { FilePicker } from './FilePicker'
import { MicrophonePresenter } from './MicrophonePresenter'
import './ChatInputFooter.css'

const MAX_ATTACHMENTS = 10

type Props = {
  inputText: string
  attachments: Attachment[]
  recordingState: RecordingState
  loading: boolean
  onInputTextChange: (text: string) => void
  onAddAttachments: (atts: Attachment[]) => void
  onRemoveAttachment: (id: string) => void
  onClearAttachments: () => void
  onRecordingStateChange: (state: RecordingState) => void
  onSendText: (text: string, attachments: Attachment[]) => void
  onSendVoice: (blob: Blob, duration: number) => void
}

export function ChatInputFooter({
  inputText,
  attachments,
  recordingState,
  loading,
  onInputTextChange,
  onAddAttachments,
  onRemoveAttachment,
  onClearAttachments,
  onRecordingStateChange,
  onSendText,
  onSendVoice,
}: Props) {
  const textareaRef = useRef<HTMLTextAreaElement>(null)

  const autoResize = useCallback(() => {
    const el = textareaRef.current
    if (!el) return
    el.style.height = 'auto'
    el.style.height = `${Math.min(el.scrollHeight, 120)}px`
  }, [])

  useEffect(() => {
    autoResize()
  }, [inputText, autoResize])

  const handleSend = useCallback(() => {
    const trimmed = inputText.trim()
    if (!trimmed || loading) return
    onSendText(trimmed, attachments)
    // Clear attachments after sending (double cleanup in case reducer doesn't do it)
    onClearAttachments()
  }, [inputText, attachments, loading, onSendText, onClearAttachments])

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault()
        handleSend()
      }
    },
    [handleSend],
  )

  const handleVoiceReady = useCallback(
    (blob: Blob, duration: number) => {
      onSendVoice(blob, duration)
    },
    [onSendVoice],
  )

  const isRecording = recordingState !== 'idle'
  const showMicButton = inputText.trim().length === 0 && attachments.length === 0 && !isRecording

  return (
    <footer className="chat-footer">
      <AttachmentsPreview attachments={attachments} onRemove={onRemoveAttachment} />

      {isRecording ? (
        <div className="chat-footer-row recording-row">
          <MicrophonePresenter
            recordingState={recordingState}
            onRecordingStateChange={onRecordingStateChange}
            onVoiceReady={handleVoiceReady}
          />
        </div>
      ) : (
        <div className="chat-footer-row">
          <FilePicker
            currentCount={attachments.length}
            maxCount={MAX_ATTACHMENTS}
            onPick={onAddAttachments}
          />

          <div className="chat-footer-input-area">
            <textarea
              ref={textareaRef}
              className="chat-textarea"
              value={inputText}
              onChange={(e) => onInputTextChange(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder="Введите продукт..."
              rows={1}
              disabled={loading}
              aria-label="Текст сообщения"
            />
          </div>

          {showMicButton ? (
            <MicrophonePresenter
              recordingState={recordingState}
              onRecordingStateChange={onRecordingStateChange}
              onVoiceReady={handleVoiceReady}
            />
          ) : (
            <button
              type="button"
              className="chat-send-btn"
              onClick={handleSend}
              disabled={!inputText.trim() || loading}
              aria-label="Отправить"
            >
              {loading ? '⏳' : '➤'}
            </button>
          )}
        </div>
      )}
    </footer>
  )
}
