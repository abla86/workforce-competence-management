import { useEffect, useState } from "react";

export default function Plan({ api }) {
  const [departmentId, setDepartmentId] = useState(1);
  const [team, setTeam] = useState(null);
  const [dailyPlan, setDailyPlan] = useState(null);
  const [shiftPlan, setShiftPlan] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  async function load() {
    setLoading(true); setError("");
    try {
      const [t, d, s] = await Promise.all([api.teamAvailability(departmentId), api.todayPlan(departmentId), api.currentShiftPlan(departmentId)]);
      setTeam(t); setDailyPlan(d); setShiftPlan(s);
    } catch (e) { setError(e.message || "Kunne ikke hente planleggingsdata."); }
    finally { setLoading(false); }
  }

  useEffect(() => { load(); }, [departmentId]);

  async function publishDaily() {
    await api.publishDailyPlan(departmentId); await load();
  }

  return <div className="page">
    <div className="page-heading action-heading">
      <div><p className="kicker">Planlegging</p><h1>Dagsplan og skiftplan</h1><p>Se hvem som er tilgjengelig, hva som er planlagt og hvilken publisert vaktplan som gjelder.</p></div>
      <label>Avdeling <input type="number" min="1" value={departmentId} onChange={e => setDepartmentId(Number(e.target.value) || 1)} /></label>
    </div>
    {error && <div className="toast error">{error}</div>}
    {loading ? <div className="loading-state">Laster plan...</div> : <>
      <div className="metrics">
        <div className="metric-card"><span>Tilgjengelige</span><strong>{team?.available ?? 0}</strong><small>Kan brukes til bemanning</small></div>
        <div className="metric-card"><span>På vakt</span><strong>{team?.busy ?? 0}</strong><small>Automatisk fra vaktplan</small></div>
        <div className="metric-card"><span>Fraværende</span><strong>{team?.absent ?? 0}</strong><small>Syk, ferie eller annet fravær</small></div>
        <div className="metric-card"><span>Dagsplan</span><strong>{dailyPlan?.isPublished ? "Publisert" : "Kladd"}</strong><small>{dailyPlan?.planTitle || "Ingen plan"}</small></div>
      </div>
      <div className="two">
        <section className="panel"><div className="panel-heading"><div><h2>Dagens status</h2><p>Automatisk status kombinerer fravær og registrerte vakter.</p></div></div>
          <div className="vaktklar-staff-grid">{(team?.byStatus ? Object.values(team.byStatus).flat() : []).map((employee) => <article key={employee.employeeId}><strong>{employee.employeeName}</strong><span>{label(employee.status)} · {employee.statusText || ""}</span></article>)}</div>
        </section>
        <section className="panel"><div className="panel-heading"><div><h2>Dagens plan</h2><p>{dailyPlan?.planTitle || "Ingen plan"}</p></div><button className="primary-button" onClick={publishDaily} disabled={dailyPlan?.isPublished}>Publiser</button></div>
          {(dailyPlan?.tasks || []).length ? dailyPlan.tasks.map(t => <div className="vaktklar-task-list" key={t.id}><strong>{t.title}</strong><span>{t.description || ""}</span></div>) : <div className="empty-state">Ingen oppgaver er registrert.</div>}
        </section>
      </div>
      <section className="panel"><div className="panel-heading"><div><h2>Publisert skiftplan</h2><p>{shiftPlan ? `${formatDate(shiftPlan.startDate)} – ${formatDate(shiftPlan.endDate)}` : "Ingen aktiv publisert skiftplan"}</p></div></div>
        {shiftPlan ? <div className="vaktklar-shifts">{(shiftPlan.shifts || []).map(s => <article className="vaktklar-shift ok" key={s.id}><div className="vaktklar-shift-main"><div><strong>{formatDate(s.date)}</strong><span>{s.shiftType} · {s.hours} t</span></div><div><strong>{s.minimumStaff}</strong><span>minimum</span></div></div></article>)}</div> : <div className="empty-state">Opprett og publiser en skiftplan for avdelingen.</div>}
      </section>
    </>}
  </div>;
}

function label(status) { return ({ Available: "Tilgjengelig", Busy: "På vakt", Sick: "Syk", OnVacation: "Ferie", Away: "Borte", InMeeting: "Møte", Unknown: "Ukjent" })[status] || status; }
function formatDate(value) { return value ? new Date(value).toLocaleDateString("nb-NO") : ""; }
