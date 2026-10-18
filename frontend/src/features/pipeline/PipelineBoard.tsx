import { useEffect, useState, type FormEvent } from 'react';
import {
  STAGES,
  STAGE_LABELS,
  addCard,
  cardsInStage,
  deleteCard,
  editCard,
  moveCard,
  setScript,
  type Card,
  type Stage,
} from './pipeline';
import { loadBoard, saveBoard } from './storage';
import './PipelineBoard.css';

interface CardViewProps {
  card: Card;
  onMove: (stage: Stage) => void;
  onEdit: (patch: { title: string; notes: string }) => void;
  onSaveScript: (script: string) => void;
  onDelete: () => void;
}

function CardView({ card, onMove, onEdit, onSaveScript, onDelete }: CardViewProps) {
  const [open, setOpen] = useState(false);
  const [title, setTitle] = useState(card.title);
  const [notes, setNotes] = useState(card.notes);
  const [script, setScriptDraft] = useState(card.script);

  const toggle = () => {
    if (!open) {
      // Re-seed the drafts from the saved card each time the editor opens.
      setTitle(card.title);
      setNotes(card.notes);
      setScriptDraft(card.script);
    }
    setOpen(!open);
  };

  const handleDelete = () => {
    if (window.confirm(`Delete "${card.title}"? This cannot be undone.`)) onDelete();
  };

  return (
    <li className="pipe-card" data-testid={`card-${card.id}`}>
      <h3 className="pipe-card-title">{card.title}</h3>
      {card.notes && <p className="pipe-card-notes">{card.notes}</p>}
      <div className="pipe-card-row">
        <label className="pipe-sr" htmlFor={`stage-${card.id}`}>
          Stage of {card.title}
        </label>
        <select
          id={`stage-${card.id}`}
          className="pipe-input"
          value={card.stage}
          onChange={(e) => onMove(e.target.value as Stage)}
        >
          {STAGES.map((s) => (
            <option key={s} value={s}>
              {STAGE_LABELS[s]}
            </option>
          ))}
        </select>
        <button type="button" className="pipe-btn" aria-expanded={open} onClick={toggle}>
          {open ? 'Close' : 'Open'}
        </button>
      </div>

      {open && (
        <div className="pipe-detail">
          <label className="pipe-label">
            Title
            <input className="pipe-input" value={title} onChange={(e) => setTitle(e.target.value)} />
          </label>
          <label className="pipe-label">
            Notes
            <textarea className="pipe-input" rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} />
          </label>
          <button
            type="button"
            className="pipe-btn"
            disabled={title.trim() === ''}
            onClick={() => onEdit({ title, notes })}
          >
            Save details
          </button>
          <label className="pipe-label">
            Script
            <textarea
              className="pipe-input pipe-script"
              rows={10}
              value={script}
              onChange={(e) => setScriptDraft(e.target.value)}
            />
          </label>
          <div className="pipe-card-row">
            <button type="button" className="pipe-btn pipe-btn-primary" onClick={() => onSaveScript(script)}>
              Save script
            </button>
            <button type="button" className="pipe-btn pipe-btn-danger" onClick={handleDelete}>
              Delete
            </button>
          </div>
        </div>
      )}
    </li>
  );
}

export function PipelineBoard() {
  const [board, setBoard] = useState(loadBoard);
  const [title, setTitle] = useState('');
  const [notes, setNotes] = useState('');

  useEffect(() => {
    saveBoard(board);
  }, [board]);

  const submit = (e: FormEvent) => {
    e.preventDefault();
    if (title.trim() === '') return;
    setBoard((b) => addCard(b, { title, notes }));
    setTitle('');
    setNotes('');
  };

  return (
    <div className="pipe">
      <h1 className="pipe-heading">Video pipeline</h1>
      <p className="pipe-sub">Every video, from idea to published.</p>

      <form className="pipe-add glass" onSubmit={submit} aria-label="Add an idea">
        <input
          className="pipe-input"
          placeholder="Working title"
          aria-label="Working title"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
        />
        <textarea
          className="pipe-input"
          placeholder="Notes (optional)"
          aria-label="Notes"
          rows={2}
          value={notes}
          onChange={(e) => setNotes(e.target.value)}
        />
        <button type="submit" className="pipe-btn pipe-btn-primary" disabled={title.trim() === ''}>
          Add idea
        </button>
      </form>

      <div className="pipe-columns">
        {STAGES.map((stage) => {
          const cards = cardsInStage(board, stage);
          return (
            <section key={stage} className="pipe-col glass" aria-label={STAGE_LABELS[stage]} data-testid={`col-${stage}`}>
              <h2 className="pipe-col-title">
                {STAGE_LABELS[stage]} <span className="pipe-count">{cards.length}</span>
              </h2>
              {cards.length === 0 ? (
                <p className="pipe-empty">
                  {stage === 'idea' ? 'No ideas yet. Add one above.' : 'Nothing here yet.'}
                </p>
              ) : (
                <ul className="pipe-list">
                  {cards.map((card) => (
                    <CardView
                      key={card.id}
                      card={card}
                      onMove={(s) => setBoard((b) => moveCard(b, card.id, s))}
                      onEdit={(patch) => setBoard((b) => editCard(b, card.id, patch))}
                      onSaveScript={(s) => setBoard((b) => setScript(b, card.id, s))}
                      onDelete={() => setBoard((b) => deleteCard(b, card.id))}
                    />
                  ))}
                </ul>
              )}
            </section>
          );
        })}
      </div>
    </div>
  );
}
