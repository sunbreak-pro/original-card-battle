/**
 * NoteEditor - Inline editor for creating/editing journal notes
 *
 * Provides a textarea with save/cancel buttons.
 * Save is disabled when content is empty.
 */

import { useState } from "react";

interface NoteEditorProps {
  initialContent?: string;
  onSave: (content: string) => void;
  onCancel: () => void;
}

export function NoteEditor({ initialContent = "", onSave, onCancel }: NoteEditorProps) {
  const [content, setContent] = useState(initialContent);

  const trimmed = content.trim();
  const canSave = trimmed.length > 0;

  const handleSave = () => {
    if (canSave) {
      onSave(trimmed);
    }
  };

  return (
    <div className="journal-note-editor">
      <textarea
        className="journal-note-textarea"
        value={content}
        onChange={(e) => setContent(e.target.value)}
        placeholder="メモを入力..."
        rows={4}
        autoFocus
      />
      <div className="journal-note-editor-footer">
        <span className="journal-note-char-count">{trimmed.length} 文字</span>
        <div className="journal-note-editor-actions">
          <button
            className="journal-btn journal-btn-secondary"
            onClick={onCancel}
          >
            キャンセル
          </button>
          <button
            className="journal-btn"
            onClick={handleSave}
            disabled={!canSave}
          >
            保存
          </button>
        </div>
      </div>
    </div>
  );
}
