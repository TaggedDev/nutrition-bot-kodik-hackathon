import { useRef } from 'react'
import type { Attachment } from './types'
import { AttachmentIcon } from './ComposerIcons'

const ACCEPT = 'image/jpeg,image/png,image/bmp'
const MAX_SIZE = 10 * 1024 * 1024

type Props = {
  currentCount: number
  maxCount: number
  onPick: (attachments: Attachment[]) => void
}

export function FilePicker({ currentCount, maxCount, onPick }: Props) {
  const inputRef = useRef<HTMLInputElement>(null)

  const handleChange = () => {
    const input = inputRef.current
    if (!input?.files) return

    const remaining = maxCount - currentCount
    if (remaining <= 0) {
      input.value = ''
      return
    }

    const files = Array.from(input.files)
    const valid: Attachment[] = []

    for (const file of files) {
      if (valid.length >= remaining) break
      if (file.size > MAX_SIZE) continue
      valid.push({
        id: crypto.randomUUID(),
        file,
        previewUrl: URL.createObjectURL(file),
      })
    }

    if (valid.length > 0) {
      onPick(valid)
    }

    input.value = ''
  }

  const isDisabled = currentCount >= maxCount

  return (
    <>
      <input
        ref={inputRef}
        type="file"
        accept={ACCEPT}
        multiple
        onChange={handleChange}
        className="file-picker-input"
        aria-hidden="true"
        tabIndex={-1}
      />
      <button
        type="button"
        className="file-picker-btn"
        onClick={() => inputRef.current?.click()}
        disabled={isDisabled}
        title={isDisabled ? `Максимум ${maxCount} изображений` : 'Прикрепить изображение'}
        aria-label="Attach file"
      >
        <AttachmentIcon />
      </button>
    </>
  )
}
