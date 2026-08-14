import MetricCard from "../components/MetricCard.jsx";
import StatusBadge from "../components/StatusBadge.jsx";

export default function Dashboard({ data }) {
  const coverage = data?.competenceCoverage ?? 0;
  const actions = data?.actionRequiredShifts ?? 0;

  return (
    <>
      <div className="page-heading">
        <div>
          <p className="kicker">Overview</p>
          <h1>Workforce dashboard</h1>
          <p>Staffing, competence coverage and upcoming workforce risks.</p>
        </div>
      </div>

      <section className="metrics">
        <MetricCard label="Employees" value={data?.totalEmployees ?? "—"} status="GOOD" detail="Active workforce" />
        <MetricCard label="Competences" value={data?.activeCompetences ?? "—"} status="ACTIVE" detail="Tracked skills" />
        <MetricCard
          label="Coverage"
          value={`${coverage}%`}
          status={coverage >= 90 ? "GOOD" : coverage >= 75 ? "ATTENTION" : "ACTION_REQUIRED"}
          detail="Competence requirement coverage"
        />
        <MetricCard
          label="Action required"
          value={actions}
          status={actions === 0 ? "GOOD" : "ACTION_REQUIRED"}
          detail="Shifts with staffing or competence gaps"
        />
      </section>

      <section className="panel">
        <div className="panel-heading">
          <div>
            <h2>Upcoming shifts</h2>
            <p>Coverage is calculated from staffing and competence requirements.</p>
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
              {(data?.upcomingShifts ?? []).map((shift) => (
                <tr key={shift.id}>
                  <td>{shift.date}</td>
                  <td><strong>{shift.shiftType}</strong></td>
                  <td>{shift.assignedStaff} / {shift.minimumStaff}</td>
                  <td>
                    <div className="coverage-cell">
                      <div className="progress"><span style={{ width: `${shift.competenceCoverage}%` }} /></div>
                      <span>{shift.competenceCoverage}%</span>
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
