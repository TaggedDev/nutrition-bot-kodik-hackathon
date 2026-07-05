import type { Attachment } from './types'
import { XIcon } from './ComposerIcons'
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
          <span className="attachments-preview-name" title={att.file.name}>
            {att.file.name}
          </span>
          <button
            type="button"
            className="attachments-preview-remove"
            onClick={() => onRemove(att.id)}
            aria-label={`Remove ${att.file.name}`}
          >
            <XIcon />
          </button>
        </div>
      ))}
    </div>
  )
}
