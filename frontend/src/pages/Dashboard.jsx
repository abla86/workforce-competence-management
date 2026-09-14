import MetricCard from "../components/MetricCard.jsx";
import StatusBadge from "../components/StatusBadge.jsx";

export default function Dashboard({ data, employees = [] }) {
  const coverage = data?.competenceCoverage ?? 0;
  const actions = data?.actionRequiredShifts ?? 0;
  const warnings = data?.warningShifts ?? 0;
  const shifts = data?.upcomingShifts ?? [];
  const goodShifts = shifts.filter((s) => s.overallStatus === "GREEN").length;
  const redShifts = shifts.filter((s) => s.overallStatus === "RED");
  const competenceAlerts = employees.flatMap((employee) =>
    (employee.competences ?? []).filter((c) => c.status === "EXPIRED" || c.status === "REVIEW_DUE").map((c) => ({ employeeId: employee.id, employeeName: employee.name, ...c }))
  );

  return (
    <>
      <div className="page-heading"><div><p className="kicker">Operativ oversikt</p><h1>Bemanning og kompetanse</h1><p>Systemet viser hva som er trygt dekket, hva som krever handling og hvorfor.</p></div></div>

      {(actions > 0 || competenceAlerts.length > 0) && (
        <section className="panel" aria-label="Handlinger">
          <div className="panel-heading"><div><h2>Dette krever oppmerksomhet</h2><p>Prioriter røde vakter og utløpt kompetanse først.</p></div></div>
          <div className="action-list">
            {redShifts.slice(0, 6).map((shift) => (
              <div className="action-item" key={shift.id}><StatusBadge status={shift.overallStatus} /><div><strong>{shift.date} · {shift.shiftType}</strong><div>{(shift.warnings ?? []).slice(0, 2).join(" · ") || "Åpne vakten for detaljert analyse og kandidatforslag."}</div></div></div>
            ))}
            {competenceAlerts.slice(0, 4).map((alert) => (
              <div className="action-item" key={`${alert.employeeId}-${alert.competenceId}`}><StatusBadge status={alert.status} /><div><strong>{alert.employeeName}</strong><div>{alert.name}</div></div></div>
            ))}
          </div>
        </section>
      )}

      <section className="metrics">
        <MetricCard label="Ansatte" value={data?.totalEmployees ?? "—"} status="GOOD" detail="Aktiv arbeidsstyrke" />
        <MetricCard label="Kompetanser" value={data?.activeCompetences ?? "—"} status="ACTIVE" detail="Registrerte kompetanser" />
        <MetricCard label="Kompetansedekning" value={`${coverage}%`} status={coverage >= 90 ? "GOOD" : coverage >= 75 ? "ATTENTION" : "ACTION_REQUIRED"} detail={data?.competencesExpiring45Days ? `${data.competencesExpiring45Days} utløper innen 45 dager` : "Ingen nærstående utløp"} />
        <MetricCard label="Handling kreves" value={actions} status={actions === 0 ? "GOOD" : "ACTION_REQUIRED"} detail={actions === 0 ? "Ingen røde vakter" : `${actions} vakter krever tiltak`} />
      </section>

      <section className="coverage-summary">
        <article className="summary-tile good-tile"><span>GREEN</span><strong>{goodShifts}</strong><p>vakter oppfyller bemanning og kompetansekrav</p></article>
        <article className={`summary-tile ${actions ? "bad-tile" : "good-tile"}`}><span>{actions ? "RED" : "NO GAPS"}</span><strong>{actions}</strong><p>{actions ? "vakter har krav som ikke er oppfylt" : "alle planlagte vakter er dekket"}</p></article>
        <article className="summary-tile"><span>YELLOW</span><strong>{warnings}</strong><p>vakter har varsler som bør vurderes</p></article>
      </section>

      <section className="panel"><div className="panel-heading"><div><h2>Vakter</h2><p>Hver status kan forklares med konkrete bemannings- og kompetanseårsaker.</p></div></div>
        <div className="table-wrap"><table><thead><tr><th>Dato</th><th>Vakt</th><th>Bemanning</th><th>Kompetanse</th><th>Status</th><th>Årsaker</th></tr></thead>
          <tbody>{shifts.map((shift) => <tr className={shift.overallStatus === "GREEN" ? "good-table-row" : "bad-table-row"} key={shift.id}>
            <td>{shift.date}</td><td><strong>{shift.shiftType}</strong></td><td><strong>{shift.assignedStaff} / {shift.minimumStaff}</strong>{!shift.staffingCovered && <span className="cell-warning">Mangler {shift.missingStaff}</span>}</td>
            <td><div className="coverage-cell"><div className={`progress ${shift.competenceCoverage < 100 ? "progress-danger" : ""}`}><span style={{ width: `${shift.competenceCoverage}%` }} /></div><strong>{shift.competenceCoverage}%</strong></div></td>
            <td><StatusBadge status={shift.overallStatus} /></td><td>{(shift.warnings ?? []).join(" · ") || "Ingen avvik"}</td>
          </tr>)}</tbody></table></div>
      </section>
    </>
  );
}
