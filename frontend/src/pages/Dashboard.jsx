import MetricCard from "../components/MetricCard.jsx";
import StatusBadge from "../components/StatusBadge.jsx";

export default function Dashboard({ data, employees = [] }) {
  const coverage = data?.competenceCoverage ?? 0;
  const actions = data?.actionRequiredShifts ?? 0;
  const shifts = data?.upcomingShifts ?? [];
  const goodShifts = shifts.filter((s) => s.overallCovered).length;

  const competenceAlerts = employees.flatMap((employee) =>
    (employee.competences ?? [])
      .filter((competence) => competence.status === "EXPIRED" || competence.status === "REVIEW_DUE")
      .map((competence) => ({
        employeeId: employee.id,
        employeeName: employee.name,
        ...competence,
      }))
  );

  const expiredAlerts = competenceAlerts.filter((item) => item.status === "EXPIRED");
  const reviewAlerts = competenceAlerts.filter((item) => item.status === "REVIEW_DUE");

  return (
    <>
      <div className="page-heading">
        <div>
          <p className="kicker">Operational overview</p>
          <h1>Workforce dashboard</h1>
          <p>Live staffing and competence status across planned shifts.</p>
        </div>
      </div>

      {competenceAlerts.length > 0 && (
        <section className="panel" style={{ border: "1px solid #f59e0b", background: "#fffbeb" }} aria-label="Competence alerts">
          <div className="panel-heading">
            <div>
              <h2>Competence alerts</h2>
              <p>
                {expiredAlerts.length > 0 && `${expiredAlerts.length} expired`}
                {expiredAlerts.length > 0 && reviewAlerts.length > 0 && " · "}
                {reviewAlerts.length > 0 && `${reviewAlerts.length} due within 45 days`}
              </p>
            </div>
          </div>
          <div style={{ display: "grid", gap: "0.65rem" }}>
            {competenceAlerts.map((alert) => (
              <div
                key={`${alert.employeeId}-${alert.competenceId}`}
                style={{
                  display: "flex",
                  justifyContent: "space-between",
                  gap: "1rem",
                  alignItems: "center",
                  padding: "0.75rem 1rem",
                  borderRadius: "0.6rem",
                  background: "white",
                  border: "1px solid #fde68a",
                }}
              >
                <div>
                  <strong>{alert.employeeName}</strong>
                  <div>{alert.name}</div>
                </div>
                <span style={{ fontWeight: 700 }}>
                  {alert.status === "EXPIRED" ? "EXPIRED" : "REVIEW DUE"}
                </span>
              </div>
            ))}
          </div>
        </section>
      )}

      <section className="metrics">
        <MetricCard label="Employees" value={data?.totalEmployees ?? "—"} status="GOOD" detail="Active workforce" />
        <MetricCard label="Competences" value={data?.activeCompetences ?? "—"} status="ACTIVE" detail="Tracked capabilities" />
        <MetricCard
          label="Competence coverage"
          value={`${coverage}%`}
          status={coverage >= 90 ? "GOOD" : coverage >= 75 ? "ATTENTION" : "ACTION_REQUIRED"}
          detail={coverage >= 90 ? "Strong coverage" : "Coverage needs attention"}
        />
        <MetricCard
          label="Action required"
          value={actions}
          status={actions === 0 ? "GOOD" : "ACTION_REQUIRED"}
          detail={actions === 0 ? "All shifts covered" : `${actions} shift${actions === 1 ? "" : "s"} need action`}
        />
      </section>

      <section className="coverage-summary">
        <article className="summary-tile good-tile">
          <span>COVERED</span>
          <strong>{goodShifts}</strong>
          <p>shifts meet staffing and competence requirements</p>
        </article>
        <article className={`summary-tile ${actions ? "bad-tile" : "good-tile"}`}>
          <span>{actions ? "ACTION REQUIRED" : "NO GAPS"}</span>
          <strong>{actions}</strong>
          <p>{actions ? "shifts currently have an operational gap" : "all planned shifts are covered"}</p>
        </article>
      </section>

      <section className="panel">
        <div className="panel-heading">
          <div>
            <h2>Upcoming shifts</h2>
            <p>Green means covered. Red means staffing or competence action is required.</p>
          </div>
        </div>

        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Date</th>
                <th>Shift</th>
                <th>Staffing</th>
                <th>Competence</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {shifts.map((shift) => (
                <tr className={shift.overallCovered ? "good-table-row" : "bad-table-row"} key={shift.id}>
                  <td>{shift.date}</td>
                  <td><strong>{shift.shiftType}</strong></td>
                  <td>
                    <strong>{shift.assignedStaff} / {shift.minimumStaff}</strong>
                    {!shift.staffingCovered && <span className="cell-warning">Missing {shift.missingStaff}</span>}
                  </td>
                  <td>
                    <div className="coverage-cell">
                      <div className={`progress ${shift.competenceCoverage < 100 ? "progress-danger" : ""}`}>
                        <span style={{ width: `${shift.competenceCoverage}%` }} />
                      </div>
                      <strong>{shift.competenceCoverage}%</strong>
                    </div>
                  </td>
                  <td><StatusBadge status={shift.overallStatus} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </>
  );
}
