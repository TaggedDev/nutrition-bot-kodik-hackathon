import type { Attachment } from './types'
import './AttachmentsPreview.css'

type Props = {
  attachments: Attachment[]
  onRemove: (id: string) => void
}

export function AttachmentsPreview({ attachments, onRemove }: Props) {
  if (attachments.length === 0) return null

  return (
    <div className="attachments-preview">
      {attachments.map((att) => (
        <div key={att.id} className="attachments-preview-item">
          <img src={att.previewUrl} alt={att.file.name} />
          <button
            type="button"
            className="attachments-preview-remove"
            onClick={() => onRemove(att.id)}
            aria-label={`Удалить ${att.file.name}`}
          >
            ✕
          </button>
        </div>
      ))}
    </div>
  )
}
