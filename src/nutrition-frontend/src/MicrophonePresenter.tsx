import { useRef, useState, useCallback, useEffect } from 'react'
import type { RecordingState } from './types'
import './MicrophonePresenter.css'

const MAX_DURATION_SEC = 300 // 5 minutes

function getSupportedMimeType(): string {
  const candidates = ['audio/webm', 'audio/mp4', 'audio/ogg', 'audio/wav']
  for (const mime of candidates) {
    if (MediaRecorder.isTypeSupported(mime)) return mime
  }
  return 'audio/webm' // fallback
}

type Props = {
  recordingState: RecordingState
  onRecordingStateChange: (state: RecordingState) => void
  onVoiceReady: (blob: Blob, duration: number) => void
}

export function MicrophonePresenter({
  recordingState,
  onRecordingStateChange,
  onVoiceReady,
}: Props) {
  const buttonRef = useRef<HTMLButtonElement>(null)
  const mediaRecorderRef = useRef<MediaRecorder | null>(null)
  const streamRef = useRef<MediaStream | null>(null)
  const chunksRef = useRef<Blob[]>([])
  const startTimeRef = useRef<number>(0)
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null)
  const [elapsed, setElapsed] = useState(0)
  const [permissionError, setPermissionError] = useState<string | null>(null)

  // Lock mode: was the button swiped up?
  const lockRef = useRef(false)
  const pointerStartY = useRef(0)
  const SWIPE_THRESHOLD = 60

  const cleanup = useCallback(() => {
    if (timerRef.current) {
      clearInterval(timerRef.current)
      timerRef.current = null
    }
    if (mediaRecorderRef.current && mediaRecorderRef.current.state !== 'inactive') {
      mediaRecorderRef.current.stop()
    }
    if (streamRef.current) {
      streamRef.current.getTracks().forEach((t) => t.stop())
      streamRef.current = null
    }
    mediaRecorderRef.current = null
    chunksRef.current = []
    setElapsed(0)
    lockRef.current = false
  }, [])

  const startRecording = useCallback(async () => {
    setPermissionError(null)
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true })
      streamRef.current = stream

      const mimeType = getSupportedMimeType()
      const recorder = new MediaRecorder(stream, { mimeType })
      mediaRecorderRef.current = recorder
      chunksRef.current = []

      recorder.ondataavailable = (e) => {
        if (e.data.size > 0) chunksRef.current.push(e.data)
      }

      recorder.onstop = () => {
        const blob = new Blob(chunksRef.current, { type: mimeType })
        const duration = (Date.now() - startTimeRef.current) / 1000
        cleanup()
        if (blob.size > 0 && duration > 0.3) {
          onVoiceReady(blob, duration)
        }
        onRecordingStateChange('idle')
      }

      recorder.start()
      startTimeRef.current = Date.now()
      lockRef.current = false
      onRecordingStateChange('recording')

      timerRef.current = setInterval(() => {
        const sec = (Date.now() - startTimeRef.current) / 1000
        setElapsed(sec)
        if (sec >= MAX_DURATION_SEC) {
          stopRecording()
        }
      }, 100)
    } catch (err) {
      if (err instanceof DOMException && err.name === 'NotAllowedError') {
        setPermissionError('Доступ к микрофону запрещён. Разрешите в настройках браузера.')
      } else {
        setPermissionError('Не удалось получить доступ к микрофону.')
      }
    }
  }, [cleanup, onRecordingStateChange, onVoiceReady])

  const stopRecording = useCallback(() => {
    if (mediaRecorderRef.current && mediaRecorderRef.current.state !== 'inactive') {
      mediaRecorderRef.current.stop()
    }
  }, [])

  // Pointer handlers
  const handlePointerDown = useCallback(
    (e: React.PointerEvent) => {
      e.preventDefault()
      buttonRef.current?.setPointerCapture(e.pointerId)
      pointerStartY.current = e.clientY

      if (recordingState === 'idle') {
        startRecording()
      } else if (recordingState === 'locked') {
        // In locked mode, clicking stops and sends
        stopRecording()
      }
    },
    [recordingState, startRecording, stopRecording],
  )

  const handlePointerMove = useCallback(
    (e: React.PointerEvent) => {
      if (recordingState !== 'recording' || lockRef.current) return

      const deltaY = pointerStartY.current - e.clientY
      if (deltaY > SWIPE_THRESHOLD) {
        lockRef.current = true
        onRecordingStateChange('locked')
      }
    },
    [recordingState, onRecordingStateChange],
  )

  const handlePointerUp = useCallback(
    (e: React.PointerEvent) => {
      if (recordingState === 'recording') {
        if (!lockRef.current) {
          stopRecording()
        }
      }
      buttonRef.current?.releasePointerCapture(e.pointerId)
    },
    [recordingState, stopRecording],
  )

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      cleanup()
    }
  }, [cleanup])

  const isActive = recordingState !== 'idle'

  return (
    <div className={`mic-wrapper ${isActive ? 'active' : ''}`}>
      {permissionError && <div className="mic-error">{permissionError}</div>}

      {isActive && (
        <>
          <div className="mic-recording-indicator">
            <span className="mic-dot" />
            <span>{formatElapsed(elapsed)}</span>
          </div>

          {recordingState === 'locked' && (
            <button
              type="button"
              className="mic-send-locked"
              onClick={stopRecording}
              aria-label="Отправить запись"
            >
              ⬆ Отправить
            </button>
          )}

          {recordingState === 'recording' && (
            <div className="mic-swipe-hint">
              ⬆ Уведите вверх для блокировки
            </div>
          )}
        </>
      )}

      <button
        ref={buttonRef}
        type="button"
        className={`mic-btn ${recordingState}`}
        onPointerDown={handlePointerDown}
        onPointerMove={handlePointerMove}
        onPointerUp={handlePointerUp}
        onContextMenu={(e) => e.preventDefault()}
        aria-label={
          recordingState === 'idle'
            ? 'Записать голосовое сообщение'
            : recordingState === 'recording'
              ? 'Запись... отпустите для отправки'
              : 'Запись заблокирована, нажмите для отправки'
        }
      >
        {recordingState === 'idle' ? '🎤' : recordingState === 'recording' ? '🔴' : '🔒'}
      </button>

      {isActive && <div className="mic-waveform" data-state={recordingState} />}
    </div>
  )
}

function formatElapsed(sec: number): string {
  const m = Math.floor(sec / 60)
  const s = Math.floor(sec % 60)
  return `${m}:${s.toString().padStart(2, '0')}`
}
