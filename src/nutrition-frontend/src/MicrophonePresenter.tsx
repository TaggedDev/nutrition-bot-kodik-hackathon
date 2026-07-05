import { useCallback, useEffect, useRef, useState } from 'react'
import type { RecordingState } from './types'
import { LockIcon, MicIcon, SendIcon, XIcon } from './ComposerIcons'
import './MicrophonePresenter.css'

const MAX_DURATION_SEC = 300
const LOCK_THRESHOLD_PX = 68
const WAVE_BARS = Array.from({ length: 24 }, (_, index) => index)

type StopMode = 'send' | 'cancel'

type Props = {
  recordingState: RecordingState
  onRecordingStateChange: (state: RecordingState) => void
  onVoiceReady: (blob: Blob, duration: number) => void
  disabled?: boolean
}

function getSupportedMimeType(): string | null {
  if (!('MediaRecorder' in window)) return null

  const candidates = ['audio/webm;codecs=opus', 'audio/webm', 'audio/mp4', 'audio/ogg']
  for (const mime of candidates) {
    if (MediaRecorder.isTypeSupported(mime)) return mime
  }

  return ''
}

export function MicrophonePresenter({
  recordingState,
  onRecordingStateChange,
  onVoiceReady,
  disabled = false,
}: Props) {
  const buttonRef = useRef<HTMLButtonElement>(null)
  const mediaRecorderRef = useRef<MediaRecorder | null>(null)
  const streamRef = useRef<MediaStream | null>(null)
  const chunksRef = useRef<Blob[]>([])
  const startTimeRef = useRef(0)
  const timerRef = useRef<number | null>(null)
  const stopModeRef = useRef<StopMode>('send')
  const pointerStartYRef = useRef(0)
  const pointerHeldRef = useRef(false)
  const lockedRef = useRef(false)
  const [elapsed, setElapsed] = useState(0)
  const [error, setError] = useState<string | null>(null)

  const stopTracks = useCallback(() => {
    streamRef.current?.getTracks().forEach((track) => track.stop())
    streamRef.current = null
  }, [])

  const clearTimer = useCallback(() => {
    if (timerRef.current !== null) {
      window.clearInterval(timerRef.current)
      timerRef.current = null
    }
  }, [])

  const resetRecording = useCallback(() => {
    clearTimer()
    stopTracks()
    mediaRecorderRef.current = null
    chunksRef.current = []
    lockedRef.current = false
    setElapsed(0)
    onRecordingStateChange('idle')
  }, [clearTimer, onRecordingStateChange, stopTracks])

  const finishRecording = useCallback(
    (mode: StopMode) => {
      stopModeRef.current = mode
      const recorder = mediaRecorderRef.current

      if (!recorder || recorder.state === 'inactive') {
        resetRecording()
        return
      }

      recorder.stop()
    },
    [resetRecording],
  )

  const startRecording = useCallback(async () => {
    if (disabled || recordingState !== 'idle') return
    setError(null)

    if (!navigator.mediaDevices?.getUserMedia) {
      setError('Браузер не поддерживает запись с микрофона.')
      return
    }

    const mimeType = getSupportedMimeType()
    if (mimeType === null) {
      setError('Браузер не поддерживает MediaRecorder.')
      return
    }

    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true })
      const recorder = mimeType
        ? new MediaRecorder(stream, { mimeType })
        : new MediaRecorder(stream)

      streamRef.current = stream
      mediaRecorderRef.current = recorder
      chunksRef.current = []
      startTimeRef.current = Date.now()
      stopModeRef.current = 'send'
      lockedRef.current = false

      recorder.ondataavailable = (event) => {
        if (event.data.size > 0) {
          chunksRef.current.push(event.data)
        }
      }

      recorder.onstop = () => {
        const duration = (Date.now() - startTimeRef.current) / 1000
        const blob = new Blob(chunksRef.current, { type: recorder.mimeType || mimeType || 'audio/webm' })
        const shouldSend = stopModeRef.current === 'send' && blob.size > 0 && duration >= 0.3

        resetRecording()

        if (shouldSend) {
          onVoiceReady(blob, duration)
        } else if (stopModeRef.current === 'send') {
          setError('Запись слишком короткая.')
        }
      }

      recorder.onerror = () => {
        setError('Не удалось записать аудио.')
        finishRecording('cancel')
      }

      recorder.start()
      onRecordingStateChange('recording')
      if (!pointerHeldRef.current) {
        finishRecording('cancel')
        return
      }

      timerRef.current = window.setInterval(() => {
        const seconds = (Date.now() - startTimeRef.current) / 1000
        setElapsed(seconds)
        if (seconds >= MAX_DURATION_SEC) {
          finishRecording('send')
        }
      }, 100)
    } catch (err) {
      stopTracks()
      if (err instanceof DOMException && err.name === 'NotAllowedError') {
        setError('Доступ к микрофону запрещен. Разрешите его в настройках браузера.')
      } else {
        setError('Не удалось получить доступ к микрофону.')
      }
    }
  }, [disabled, finishRecording, onRecordingStateChange, recordingState, resetRecording, stopTracks, onVoiceReady])

  const handlePointerDown = useCallback(
    (event: React.PointerEvent<HTMLButtonElement>) => {
      if (disabled) return
      event.preventDefault()
      pointerStartYRef.current = event.clientY
      pointerHeldRef.current = true
      buttonRef.current?.setPointerCapture(event.pointerId)
      startRecording()
    },
    [disabled, startRecording],
  )

  const handlePointerMove = useCallback(
    (event: React.PointerEvent<HTMLButtonElement>) => {
      if (recordingState !== 'recording' || lockedRef.current) return
      if (pointerStartYRef.current - event.clientY < LOCK_THRESHOLD_PX) return

      lockedRef.current = true
      onRecordingStateChange('locked')
    },
    [onRecordingStateChange, recordingState],
  )

  const handlePointerUp = useCallback(
    (event: React.PointerEvent<HTMLButtonElement>) => {
      pointerHeldRef.current = false
      if (buttonRef.current?.hasPointerCapture(event.pointerId)) {
        buttonRef.current.releasePointerCapture(event.pointerId)
      }
      if (recordingState === 'recording' && !lockedRef.current) {
        finishRecording('send')
      }
    },
    [finishRecording, recordingState],
  )

  const handlePointerCancel = useCallback(
    (event: React.PointerEvent<HTMLButtonElement>) => {
      pointerHeldRef.current = false
      if (buttonRef.current?.hasPointerCapture(event.pointerId)) {
        buttonRef.current.releasePointerCapture(event.pointerId)
      }
      if (recordingState === 'recording') {
        finishRecording('cancel')
      }
    },
    [finishRecording, recordingState],
  )

  const handleKeyDown = useCallback(
    (event: React.KeyboardEvent<HTMLButtonElement>) => {
      if (event.key === 'Escape' && recordingState !== 'idle') {
        finishRecording('cancel')
      }
    },
    [finishRecording, recordingState],
  )

  useEffect(() => {
    return () => {
      const recorder = mediaRecorderRef.current
      if (recorder && recorder.state !== 'inactive') {
        stopModeRef.current = 'cancel'
        recorder.stop()
      }
      clearTimer()
      stopTracks()
    }
  }, [clearTimer, stopTracks])

  const isActive = recordingState !== 'idle'

  return (
    <div className={`mic-composer ${isActive ? 'active' : ''}`}>
      {error && !isActive && (
        <div className="mic-error" role="status">
          {error}
        </div>
      )}

      {recordingState !== 'locked' && (
        <button
          ref={buttonRef}
          type="button"
          className={`composer-action-btn neutral mic-idle-btn ${recordingState}`}
          onPointerDown={handlePointerDown}
          onPointerMove={handlePointerMove}
          onPointerUp={handlePointerUp}
          onPointerCancel={handlePointerCancel}
          onKeyDown={handleKeyDown}
          onContextMenu={(event) => event.preventDefault()}
          disabled={disabled}
          aria-label="Start voice recording"
        >
          <MicIcon />
        </button>
      )}

      {isActive && (
        <div className={`mic-panel ${recordingState}`}>
          <div className="mic-recording-status" aria-live="polite">
            <span className="mic-dot" />
            <span className="mic-time">{formatElapsed(elapsed)}</span>
            <span className="mic-status-text">
              {recordingState === 'locked'
                ? 'Запись идет'
                : 'Отпустите, чтобы отправить'}
            </span>
          </div>

          <div className="mic-timeline" aria-hidden="true">
            <div className="mic-wave">
              {WAVE_BARS.map((bar) => (
                <span key={bar} style={{ '--bar-index': bar } as React.CSSProperties} />
              ))}
            </div>
            <span className="mic-timeline-sweep" />
          </div>

          {recordingState === 'recording' && (
            <div className="mic-lock-hint">
              <LockIcon />
              <span>Потяните вверх, чтобы закрепить</span>
            </div>
          )}

          {recordingState === 'locked' && (
            <div className="mic-locked-actions">
              <span className="mic-locked-label">
                <LockIcon />
                Закреплено
              </span>
              <button
                type="button"
                className="composer-action-btn neutral"
                onClick={() => finishRecording('cancel')}
                aria-label="Cancel recording"
              >
                <XIcon />
              </button>
              <button
                type="button"
                className="composer-action-btn send"
                onClick={() => finishRecording('send')}
                aria-label="Send message"
              >
                <SendIcon />
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  )
}

function formatElapsed(seconds: number): string {
  const m = Math.floor(seconds / 60)
  const s = Math.floor(seconds % 60)
  return `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`
}
