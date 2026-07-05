import { useCallback, useEffect, useRef, useState } from 'react'
import type { Attachment, RecordingState } from './types'
import { AttachmentsPreview } from './AttachmentsPreview'
import { FilePicker } from './FilePicker'
import { MicrophonePresenter } from './MicrophonePresenter'
import { SendIcon, SpinnerIcon } from './ComposerIcons'
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
  const resizeFrameRef = useRef<number | null>(null)
  const [statusMessage, setStatusMessage] = useState<string | null>(null)

  const autoResize = useCallback(() => {
    const el = textareaRef.current
    if (!el) return

    if (resizeFrameRef.current !== null) {
      window.cancelAnimationFrame(resizeFrameRef.current)
    }

    resizeFrameRef.current = window.requestAnimationFrame(() => {
      const target = textareaRef.current
      if (!target) return
      const computed = window.getComputedStyle(target)
      const lineHeight = Number.parseFloat(computed.lineHeight) || 22
      const padding =
        Number.parseFloat(computed.paddingTop) + Number.parseFloat(computed.paddingBottom)
      const maxHeight = lineHeight * 5 + padding

      target.style.height = 'auto'
      target.style.height = `${Math.min(target.scrollHeight, maxHeight)}px`
      target.style.overflowY = target.scrollHeight > maxHeight ? 'auto' : 'hidden'
    })
  }, [])

  useEffect(() => {
    autoResize()
  }, [inputText, autoResize])

  useEffect(() => {
    return () => {
      if (resizeFrameRef.current !== null) {
        window.cancelAnimationFrame(resizeFrameRef.current)
      }
    }
  }, [])

  const handleSend = useCallback(() => {
    const trimmed = inputText.trim()
    if (loading || (!trimmed && attachments.length === 0)) return

    setStatusMessage('Отправляется...')
    onSendText(trimmed, attachments)
    onClearAttachments()
  }, [attachments, inputText, loading, onClearAttachments, onSendText])

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
      setStatusMessage('Голосовое сообщение отправлено')
    },
    [onSendVoice],
  )

  const isRecording = recordingState !== 'idle'
  const canSend = inputText.trim().length > 0 || attachments.length > 0

  useEffect(() => {
    if (!loading && statusMessage === 'Отправляется...') {
      const timeout = window.setTimeout(() => setStatusMessage('Сообщение отправлено'), 0)
      return () => window.clearTimeout(timeout)
    }
  }, [loading, statusMessage])

  useEffect(() => {
    if (!statusMessage || loading || isRecording) return
    const timeout = window.setTimeout(() => setStatusMessage(null), 2400)
    return () => window.clearTimeout(timeout)
  }, [isRecording, loading, statusMessage])

  return (
    <footer className={`chat-footer ${isRecording ? 'is-recording' : ''}`}>
      {!isRecording && (
        <AttachmentsPreview attachments={attachments} onRemove={onRemoveAttachment} />
      )}

      <div className={`chat-footer-row ${isRecording ? 'recording-row' : ''}`}>
        <div className="composer-input-group" aria-hidden={isRecording}>
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
              disabled={loading || isRecording}
              aria-label="Message text"
            />
          </div>

          {canSend && (
            <button
              type="button"
              className="composer-action-btn send"
              onClick={handleSend}
              disabled={loading}
              aria-label="Send message"
            >
              {loading ? <SpinnerIcon className="spinner-icon" /> : <SendIcon />}
            </button>
          )}
        </div>

        <div className={`composer-mic-slot ${canSend && !isRecording ? 'is-hidden' : ''}`}>
          <MicrophonePresenter
            recordingState={recordingState}
            onRecordingStateChange={onRecordingStateChange}
            onVoiceReady={handleVoiceReady}
            disabled={loading}
          />
        </div>
      </div>

      {(statusMessage || loading) && !isRecording && (
        <div className="composer-status" aria-live="polite">
          {loading ? 'Отправляется...' : statusMessage}
        </div>
      )}
    </footer>
  )
}
