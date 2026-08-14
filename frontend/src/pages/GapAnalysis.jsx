import StatusBadge from "../components/StatusBadge.jsx";

export default function GapAnalysis({ shifts }) {
  const gaps = shifts.filter(s => !s.overallCovered);

  return (
    <>
      <div className="page-heading">
        <div>
          <p className="kicker">Risk</p>
          <h1>Gap analysis</h1>
          <p>Only shifts requiring attention are shown here.</p>
        </div>
      </div>

      {gaps.length === 0 ? (
        <section className="success-panel">
          <StatusBadge status="GOOD" />
          <h2>All requirements are covered</h2>
          <p>No staffing or competence gaps require action.</p>
        </section>
      ) : (
        <div className="gap-list">
          {gaps.map((shift) => (
            <article className="gap-panel" key={shift.id}>
              <div className="gap-summary">
                <div>
                  <span>{shift.date}</span>
                  <h2>{shift.shiftType}</h2>
                </div>
                <StatusBadge status="ACTION_REQUIRED" />
              </div>

              {!shift.staffingCovered && (
                <div className="alert red">
                  <strong>Understaffed</strong>
                  <span>Missing {shift.missingStaff} employee{shift.missingStaff === 1 ? "" : "s"}.</span>
                </div>
              )}

              {shift.requirements.filter(r => !r.covered).map(r => (
                <div className="alert red" key={r.competenceId}>
                  <strong>Missing competence: {r.competence}</strong>
                  <span>{r.qualifiedCount} qualified / {r.minimumCount} required at {r.minimumLevel} level.</span>
                </div>
              ))}

              {shift.requirements.filter(r => r.covered).map(r => (
                <div className="alert green" key={r.competenceId}>
                  <strong>{r.competence}</strong>
                  <span>COVERED · {r.qualifiedCount} qualified / {r.minimumCount} required.</span>
                </div>
              ))}
            </article>
          ))}
        </div>
      )}
    </>
  );
}
