/**
 * ThoughtsPage - Journal notes/thoughts page
 *
 * Allows creating, editing, and deleting personal notes.
 * Notes are persisted via JournalContext (localStorage).
 * Maximum 20 notes.
 */

import { useState } from "react";
import { useJournal } from "@/contexts/JournalContext";
import { NoteEditor } from "../components/NoteEditor";
import "@/ui/css/journal/Notes.css";

const MAX_NOTES = 20;

export function ThoughtsPage() {
  const { notes, addNote, updateNote, deleteNote } = useJournal();
  const [isCreating, setIsCreating] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);

  const isAtLimit = notes.length >= MAX_NOTES;

  const handleCreate = (content: string) => {
    addNote(content);
    setIsCreating(false);
  };

  const handleUpdate = (id: string, content: string) => {
    updateNote(id, content);
    setEditingId(null);
  };

  const handleDelete = (id: string) => {
    if (window.confirm("このメモを削除しますか？")) {
      deleteNote(id);
      if (editingId === id) {
        setEditingId(null);
      }
    }
  };

  const formatDate = (isoString: string): string => {
    const date = new Date(isoString);
    return date.toLocaleDateString("ja-JP", {
      year: "numeric",
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });
  };

  return (
    <div className="journal-page journal-thoughts-page">
      {/* Header with create button */}
      <div className="journal-thoughts-header">
        <button
          className="journal-btn"
          onClick={() => setIsCreating(true)}
          disabled={isCreating || isAtLimit}
        >
          新規メモ
        </button>
        <span className="journal-thoughts-count">
          {notes.length} / {MAX_NOTES}
        </span>
      </div>

      {isAtLimit && !isCreating && (
        <p className="journal-thoughts-limit-msg">
          メモの上限（{MAX_NOTES}件）に達しました。新しいメモを作成するには既存のメモを削除してください。
        </p>
      )}

      {/* Create new note editor */}
      {isCreating && (
        <div className="journal-note-item journal-note-item-new">
          <NoteEditor
            onSave={handleCreate}
            onCancel={() => setIsCreating(false)}
          />
        </div>
      )}

      {/* Note list */}
      {notes.length === 0 && !isCreating ? (
        <div className="journal-thoughts-empty">
          <p>メモはまだありません。</p>
          <p>冒険のヒントや気づいたことを書き留めましょう。</p>
        </div>
      ) : (
        <div className="journal-notes-list">
          {notes.map((note) => (
            <div key={note.id} className="journal-note-item">
              {editingId === note.id ? (
                <NoteEditor
                  initialContent={note.content}
                  onSave={(content) => handleUpdate(note.id, content)}
                  onCancel={() => setEditingId(null)}
                />
              ) : (
                <>
                  <div className="journal-note-content">
                    {note.content}
                  </div>
                  <div className="journal-note-footer">
                    <span className="journal-note-timestamp">
                      {note.updatedAt !== note.createdAt
                        ? `編集: ${formatDate(note.updatedAt)}`
                        : formatDate(note.createdAt)}
                    </span>
                    <div className="journal-note-actions">
                      <button
                        className="journal-btn journal-btn-secondary journal-btn-sm"
                        onClick={() => setEditingId(note.id)}
                      >
                        編集
                      </button>
                      <button
                        className="journal-btn journal-btn-danger journal-btn-sm"
                        onClick={() => handleDelete(note.id)}
                      >
                        削除
                      </button>
                    </div>
                  </div>
                </>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
