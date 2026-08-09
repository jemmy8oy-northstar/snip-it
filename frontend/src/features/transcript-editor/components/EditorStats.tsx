interface EditorStatsProps {
  rangeCount: number;
  keptSeconds: number;
  cutSeconds: number;
}

function fmt(seconds: number): string {
  const m = Math.floor(seconds / 60);
  const s = (seconds % 60).toFixed(1).padStart(4, '0');
  return `${m}:${s}`;
}

export function EditorStats({ rangeCount, keptSeconds, cutSeconds }: EditorStatsProps) {
  return (
    <div className="editor-stats glass">
      <div>
        <div className="editor-stat-label">Kept ranges</div>
        <div className="editor-stat-value">{rangeCount}</div>
      </div>
      <div>
        <div className="editor-stat-label">Kept</div>
        <div className="editor-stat-value" style={{ color: '#10b981' }}>
          {fmt(keptSeconds)}
        </div>
      </div>
      <div>
        <div className="editor-stat-label">Cut</div>
        <div className="editor-stat-value" style={{ color: '#ef4444' }}>
          {fmt(cutSeconds)}
        </div>
      </div>
    </div>
  );
}
