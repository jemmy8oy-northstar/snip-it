import { useEffect, useState } from 'react';
import type { KeptRange } from '../types';

interface EditListPanelProps {
  ranges: KeptRange[];
  onApply: (ranges: KeptRange[]) => void;
}

/**
 * JSON view of the computed kept ranges — port of review.html's
 * "Edit List JSON" panel. Lets you copy/save the cut list, or paste one
 * back in and Apply it to restore a prior set of cuts.
 */
export function EditListPanel({ ranges, onApply }: EditListPanelProps) {
  const [text, setText] = useState(() => JSON.stringify(ranges, null, 2));
  const [copyLabel, setCopyLabel] = useState('Copy');
  const [error, setError] = useState<string | null>(null);

  // Keep the textarea in sync with live edits, unless the user is actively
  // editing pasted JSON they haven't applied yet — simplest correct
  // behaviour for v1 is to always reflect current state, same as review.html.
  useEffect(() => {
    setText(JSON.stringify(ranges, null, 2));
  }, [ranges]);

  const handleApply = () => {
    try {
      const parsed = JSON.parse(text) as KeptRange[];
      onApply(parsed);
      setError(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Invalid JSON');
    }
  };

  const handleCopy = async () => {
    await navigator.clipboard.writeText(text);
    setCopyLabel('Copied!');
    setTimeout(() => setCopyLabel('Copy'), 1500);
  };

  const handleSave = () => {
    const blob = new Blob([text], { type: 'application/json' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = 'edit-list.json';
    a.click();
    URL.revokeObjectURL(a.href);
  };

  return (
    <div className="editor-json glass" style={{ padding: 10, display: 'flex', flexDirection: 'column', gap: 6 }}>
      <span className="editor-stat-label">Edit List JSON</span>
      <textarea
        aria-label="Edit list JSON"
        spellCheck={false}
        value={text}
        onChange={(e) => setText(e.target.value)}
      />
      {error && <span style={{ fontSize: 11, color: '#ef4444' }}>{error}</span>}
      <div style={{ display: 'flex', gap: 6 }}>
        <button className="editor-btn" type="button" onClick={handleCopy} style={{ flex: 1 }}>
          {copyLabel}
        </button>
        <button className="editor-btn" type="button" onClick={handleSave} style={{ flex: 1 }}>
          Save
        </button>
        <button className="editor-btn" type="button" onClick={handleApply} style={{ flex: 1 }}>
          ↩ Apply
        </button>
      </div>
    </div>
  );
}
