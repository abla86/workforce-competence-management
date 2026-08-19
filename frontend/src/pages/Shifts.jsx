import { useState } from "react";
import StatusBadge from "../components/StatusBadge.jsx";

const today = new Date().toISOString().slice(0, 10);

export default function Shifts({ shifts, employees, competences, api, mutate }) {
  const [showCreate, setShowCreate] = useState(false);
  const [shiftForm, setShiftForm] = useState({ date: today, shiftType: "Day", hours: 7.5, minimumStaff: 2 });
  const [manageShift, setManageShift] = useState(null);
  const [employeeId, setEmployeeId] = useState("");
  const [requirement, setRequirement] = useState({ competenceId: "", minimumCount: 1, minimumLevel: "Basic" });
  const [candidates, setCandidates] = useState([]);
  const [candidateLoading, setCandidateLoading] = useState(false);

  function createShift(event) {
    event.preventDefault();
    mutate(() => api.createShift({ ...shiftForm, hours: Number(shiftForm.hours), minimumStaff: Number(shiftForm.minimumStaff) }), "Vakt opprettet.");
    setShowCreate(false);
  }

  function addAssignment(event) {
    event.preventDefault();
    if (!manageShift || !employeeId) return;
    mutate(() => api.assignEmployee(manageShift.id, Number(employeeId)), "Ansatt tildelt.");
    setEmployeeId("");
  }

  function addRequirement(event) {
    event.preventDefault();
    if (!manageShift || !requirement.competenceId) return;
    mutate(() => api.setShiftRequirement(manageShift.id, {
      competenceId: Number(requirement.competenceId), minimumCount: Number(requirement.minimumCount), minimumLevel: requirement.minimumLevel,
    }), "Kompetansekrav lagret.");
  }

  async function openShift(shift) {
    setManageShift(shift); setCandidates([]); setCandidateLoading(true);
    try { setCandidates(await api.candidates(shift.id)); } catch { setCandidates([]); }
    finally { setCandidateLoading(false); }
  }

  const current = manageShift ? shifts.find((shift) => shift.id === manageShift.id) || manageShift : null;
  const eligibleCandidates = candidates.filter((c) => c.eligible).slice(0, 5);

  return (
    <>
      <div className="page-heading action-heading">
        <div><p className="kicker">Planlegging</p><h1>Vakter</h1><p>Opprett vakter, tildel ansatte og la systemet kontrollere kompetanse før tildeling.</p></div>
        <button className="primary-button" onClick={() => setShowCreate(!showCreate)}>+ Ny vakt</button>
      </div>

      {showCreate && <section className="editor-panel"><div className="editor-title"><h2>Opprett vakt</h2><button className="icon-button" onClick={() => setShowCreate(false)}>Lukk</button></div>
        <form className="form-grid" onSubmit={createShift}>
          <label>Dato<input type="date" required value={shiftForm.date} onChange={(e) => setShiftForm({ ...shiftForm, date: e.target.value })} /></label>
          <label>Vakt<select value={shiftForm.shiftType} onChange={(e) => setShiftForm({ ...shiftForm, shiftType: e.target.value })}><option>Day</option><option>Evening</option><option>Night</option></select></label>
          <label>Timer<input type="number" step="0.5" min="0.5" max="24" value={shiftForm.hours} onChange={(e) => setShiftForm({ ...shiftForm, hours: e.target.value })} /></label>
          <label>Minimum bemanning<input type="number" min="1" value={shiftForm.minimumStaff} onChange={(e) => setShiftForm({ ...shiftForm, minimumStaff: e.target.value })} /></label>
          <div className="form-actions"><button className="primary-button">Opprett vakt</button></div>
        </form>
      </section>}

      {current && <section className="editor-panel accent-panel">
        <div className="editor-title"><div><p className="kicker">Vaktstyring</p><h2>{current.date} · {current.shiftType}</h2></div><button className="icon-button" onClick={() => setManageShift(null)}>Lukk</button></div>
        <div className="management-columns">
          <div>
            <h3>Tildelte ansatte</h3>
            <form className="inline-form" onSubmit={addAssignment}><select value={employeeId} onChange={(e) => setEmployeeId(e.target.value)} required><option value="">Velg ansatt</option>{employees.filter((e) => e.isActive && !current.assignments.some((a) => a.employeeId === e.id)).map((e) => <option key={e.id} value={e.id}>{e.name} · {e.role}</option>)}</select><button className="primary-button">Tildel</button></form>
            <div className="manage-list">{current.assignments.map((assignment) => <div className="manage-row" key={assignment.employeeId}><div><strong>{assignment.name}</strong><span>{assignment.role}</span></div><button className="mini-danger" onClick={() => mutate(() => api.removeAssignment(current.id, assignment.employeeId), "Tildeling fjernet.")}>Fjern</button></div>)}</div>

            <div className="editor-panel" style={{ marginTop: "18px", marginBottom: 0 }}>
              <div className="panel-heading"><div><h3>Smart kandidatforslag</h3><p>Rangerer kvalifiserte ansatte og viser hvorfor andre ikke kan brukes.</p></div></div>
              {candidateLoading && <p className="muted">Analyserer tilgjengelighet, kompetanse og konflikter…</p>}
              {!candidateLoading && eligibleCandidates.length === 0 && <p className="muted">Ingen kvalifiserte kandidater funnet.</p>}
              {!candidateLoading && eligibleCandidates.map((candidate) => <div className="manage-row" key={candidate.employeeId}><div><strong>{candidate.name}</strong><span>{candidate.role} · score {candidate.score}</span></div><button className="primary-button secondary" onClick={() => mutate(() => api.assignEmployee(current.id, candidate.employeeId), "Beste kandidat tildelt.")}>Velg</button></div>)}
            </div>
          </div>

          <div>
            <h3>Kompetansekrav</h3>
            <form className="requirement-form" onSubmit={addRequirement}><select required value={requirement.competenceId} onChange={(e) => setRequirement({ ...requirement, competenceId: e.target.value })}><option value="">Velg kompetanse</option>{competences.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}</select><input type="number" min="1" value={requirement.minimumCount} onChange={(e) => setRequirement({ ...requirement, minimumCount: e.target.value })} /><select value={requirement.minimumLevel} onChange={(e) => setRequirement({ ...requirement, minimumLevel: e.target.value })}><option>Basic</option><option>Intermediate</option><option>Advanced</option></select><button className="primary-button">Lagre krav</button></form>
            <div className="manage-list">{current.requirements.map((r) => <div className={`manage-row status-row ${r.covered ? "covered-row" : "missing-row"}`} key={r.competenceId}><div><strong>{r.competence}</strong><span>{r.qualifiedCount}/{r.minimumCount} kvalifiserte · {r.minimumLevel}</span></div><div className="row-actions"><StatusBadge status={r.status} /><button className="mini-danger" onClick={() => mutate(() => api.removeShiftRequirement(current.id, r.competenceId), "Kompetansekrav fjernet.")}>×</button></div></div>)}</div>
          </div>
        </div>
      </section>}

      <div className="shift-grid">{shifts.map((shift) => <article className={`shift-card ${shift.overallCovered ? "covered" : "gap"}`} key={shift.id}>
        <div className="shift-top"><div><span>{shift.date} · {shift.hours} t</span><h3>{shift.shiftType}</h3></div><StatusBadge status={shift.overallStatus} /></div>
        <div className={`coverage-banner ${shift.staffingCovered ? "green-banner" : "red-banner"}`}><div><span>Bemanning</span><strong>{shift.assignedStaff} / {shift.minimumStaff}</strong></div><StatusBadge status={shift.staffingStatus} /></div>
        <div className="requirements">{shift.requirements.map((r) => <div className={`requirement ${r.covered ? "good-requirement" : "bad-requirement"}`} key={r.competenceId}><div><strong>{r.competence}</strong><span>{r.qualifiedCount} / {r.minimumCount} · min. {r.minimumLevel}</span></div><StatusBadge status={r.status} /></div>)}{shift.requirements.length === 0 && <p className="muted">Ingen kompetansekrav definert.</p>}</div>
        <div className="shift-actions"><button className="primary-button secondary" onClick={() => openShift(shift)}>Administrer</button><button className="danger-button" onClick={() => { if (window.confirm(`Slette ${shift.date} ${shift.shiftType}?`)) mutate(() => api.deleteShift(shift.id), "Vakt slettet."); }}>Slett</button></div>
      </article>)}</div>
    </>
  );
}
