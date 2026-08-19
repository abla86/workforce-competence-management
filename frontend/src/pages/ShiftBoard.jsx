import { useMemo, useState } from "react";

const STATUS = {
  ok: { label: "Klar", className: "ok" },
  warn: { label: "Følg opp", className: "warn" },
  bad: { label: "Ikke klar", className: "bad" },
};

function getStatus(shift) {
  const issues = [];
  if (shift.assignedCount < shift.minimumStaff) {
    issues.push(`Mangler ${shift.minimumStaff - shift.assignedCount} person(er)`);
  }
  if (shift.uncoveredTasks?.length) {
    issues.push(...shift.uncoveredTasks.map((task) => `${task.name}: mangler ${task.required} kvalifisert`));
  }
  if (shift.expiringCompetences?.length) {
    issues.push(...shift.expiringCompetences.map((item) => `${item} utløper snart`));
  }
  const level = issues.some((x) => x.includes("mangler")) ? "bad" : issues.length ? "warn" : "ok";
  return { level, issues };
}

export default function ShiftBoard({ shifts = [], onFindReplacement }) {
  const [filter, setFilter] = useState("all");
  const [selected, setSelected] = useState(null);

  const evaluated = useMemo(
    () => shifts.map((shift) => ({ shift, status: getStatus(shift) })),
    [shifts]
  );

  const visible = evaluated.filter(({ status }) => filter === "all" || status.level === filter);

  return (
    <section className="panel shift-board">
      <div className="page-heading action-heading">
        <div>
          <p className="kicker">Bemanning</p>
          <h1>Vaktkontroll</h1>
          <p>Se hvilke vakter som er klare, og løs avvik direkte.</p>
        </div>
        <div className="segmented-control" aria-label="Filtrer vakter">
          {[["all", "Alle"], ["bad", "Røde"], ["warn", "Gule"], ["ok", "Grønne"]].map(([value, label]) => (
            <button key={value} className={filter === value ? "active" : ""} onClick={() => setFilter(value)}>
              {label}
            </button>
          ))}
        </div>
      </div>

      <div className="shift-board-list">
        {visible.map(({ shift, status }) => {
          const meta = STATUS[status.level];
          return (
            <article className={`shift-board-card ${meta.className}`} key={shift.id}>
              <button className="shift-main" onClick={() => setSelected(shift)} aria-expanded={selected?.id === shift.id}>
                <div className="shift-date">
                  <strong>{shift.date}</strong>
                  <span>{shift.type}</span>
                </div>
                <div>
                  <strong>{shift.assignedCount}/{shift.minimumStaff} ansatte</strong>
                  <span>{shift.tasks?.length || 0} arbeidsoppgaver</span>
                </div>
                <span className={`status-pill ${meta.className}`}>{meta.label}</span>
              </button>

              {status.issues.length > 0 && (
                <div className="shift-issues">
                  {status.issues.map((issue) => <div key={issue}>{issue}</div>)}
                  {status.level === "bad" && (
                    <button className="primary-button" onClick={() => onFindReplacement?.(shift)}>
                      Finn kvalifisert erstatter
                    </button>
                  )}
                </div>
              )}

              {selected?.id === shift.id && (
                <div className="shift-details">
                  <h3>Hvorfor er denne vakten {meta.label.toLowerCase()}?</h3>
                  <ul>
                    {(status.issues.length ? status.issues : ["Alle registrerte krav er dekket."]).map((issue) => <li key={issue}>{issue}</li>)}
                  </ul>
                  {shift.tasks?.length > 0 && (
                    <>
                      <h3>Arbeidsoppgaver</h3>
                      <ul>{shift.tasks.map((task) => <li key={task.name || task}>{task.name || task}</li>)}</ul>
                    </>
                  )}
                </div>
              )}
            </article>
          );
        })}
      </div>
    </section>
  );
}
