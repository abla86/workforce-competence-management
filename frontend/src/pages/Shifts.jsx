import StatusBadge from "../components/StatusBadge.jsx";

export default function Shifts({ shifts }) {
  return (
    <>
      <div className="page-heading">
        <div>
          <p className="kicker">Planning</p>
          <h1>Shifts</h1>
          <p>Staffing and competence requirements for planned work periods.</p>
        </div>
      </div>

      <div className="shift-grid">
        {shifts.map((shift) => (
          <article className={`shift-card ${shift.overallCovered ? "covered" : "gap"}`} key={shift.id}>
            <div className="shift-top">
              <div>
                <span>{shift.date}</span>
                <h3>{shift.shiftType}</h3>
              </div>
              <StatusBadge status={shift.overallStatus} />
            </div>

            <div className="staffing-line">
              <div>
                <span>Staffing</span>
                <strong>{shift.assignedStaff} / {shift.minimumStaff}</strong>
              </div>
              <StatusBadge status={shift.staffingStatus} />
            </div>

            <div className="requirements">
              {shift.requirements.map((r) => (
                <div className="requirement" key={r.competenceId}>
                  <div>
                    <strong>{r.competence}</strong>
                    <span>{r.qualifiedCount} / {r.minimumCount} · min. {r.minimumLevel}</span>
                  </div>
                  <StatusBadge status={r.status} />
                </div>
              ))}
            </div>
          </article>
        ))}
      </div>
    </>
  );
}
