import { useEffect, useMemo, useState } from "react";

const STORAGE_KEY = "vaktklar-task-prototype-v1";

const defaultTasks = [
  { name: "Legemiddelutdeling", competency: "Medication management", count: 1, critical: true },
  { name: "Tilsyn og fallforebygging", competency: "First aid", count: 1, critical: false },
  { name: "Dokumentasjon", competency: "System training", count: 1, critical: false },
];

function loadTasks() {
  try {
    return JSON.parse(localStorage.getItem(STORAGE_KEY)) || defaultTasks;
  } catch {
    return defaultTasks;
  }
}

function saveTasks(tasks) {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(tasks));
}

function evaluateShift(shift, tasks) {
  const issues = [];
  if (shift.assignedStaff < shift.minimumStaff) {
    issues.push({ type: "staff", text: `Mangler ${shift.minimumStaff - shift.assignedStaff} person(er)` });
  }

  const requirements = shift.requirements || [];
  requirements.forEach((r) => {
    if (!r.covered) {
      issues.push({ type: "competence", text: `${r.competence}: ${r.qualifiedCount}/${r.minimumCount} kvalifisert` });
    }
  });

  const shiftTasks = tasks.filter((task) => shift.taskNames?.includes(task.name));
  shiftTasks.forEach((task) => {
    const requirement = requirements.find((r) => r.competence === task.competency);
    if (requirement && !requirement.covered) {
      issues.push({ type: "task", text: `${task.name} kan ikke dekkes med registrert kompetanse` });
    }
  });

  const level = issues.some((x) => x.type === "staff" || x.type === "competence" || x.type === "task") ? "bad" : "ok";
  return { level, issues };
}

function statusLabel(level) {
  return level === "ok" ? "Klar" : "Ikke klar";
}

export default function Vaktklar({ shifts = [], employees = [], competences = [], api, mutate }) {
  const [tasks, setTasks] = useState(loadTasks);
  const [view, setView] = useState("oversikt");
  const [selectedShiftId, setSelectedShiftId] = useState(null);
  const [taskForm, setTaskForm] = useState({ name: "", competency: "", count: 1, critical: false });
  const [generator, setGenerator] = useState({ meds: 8, falls: 2, wounds: 1, docs: 1 });
  const [generated, setGenerated] = useState([]);

  useEffect(() => saveTasks(tasks), [tasks]);

  const evaluations = useMemo(() => shifts.map((shift) => ({ shift, result: evaluateShift(shift, tasks) })), [shifts, tasks]);
  const redCount = evaluations.filter((x) => x.result.level === "bad").length;
  const greenCount = evaluations.length - redCount;

  function addTask(event) {
    event.preventDefault();
    if (!taskForm.name.trim() || !taskForm.competency) return;
    setTasks((current) => [
      ...current.filter((task) => task.name !== taskForm.name.trim()),
      { ...taskForm, name: taskForm.name.trim(), count: Number(taskForm.count) },
    ]);
    setTaskForm({ name: "", competency: "", count: 1, critical: false });
  }

  function generateTasks(event) {
    event.preventDefault();
    const suggestions = [];
    if (Number(generator.meds) > 0) suggestions.push({ name: "Legemiddelutdeling", competency: "Medication management", count: Math.max(1, Math.ceil(Number(generator.meds) / 12)), critical: true });
    if (Number(generator.falls) > 0) suggestions.push({ name: "Tilsyn og fallforebygging", competency: "First aid", count: Math.max(1, Math.ceil(Number(generator.falls) / 5)), critical: false });
    if (Number(generator.wounds) > 0) suggestions.push({ name: "Sårstell", competency: competences.find((c) => /wound|sår/i.test(c.name))?.name || "Medication management", count: 1, critical: true });
    if (Number(generator.docs) > 0) suggestions.push({ name: "Dokumentasjon", competency: "System training", count: 1, critical: false });
    setGenerated(suggestions);
  }

  function acceptGenerated() {
    setTasks((current) => {
      const names = new Set(generated.map((task) => task.name));
      return [...current.filter((task) => !names.has(task.name)), ...generated];
    });
    setGenerated([]);
  }

  async function findReplacement(shift) {
    if (!api?.candidates || !shift.id) return;
    try {
      const candidates = await api.candidates(shift.id);
      window.alert(candidates.length ? `Beste kandidat: ${candidates[0].name} (${candidates[0].score} %)\n${candidates[0].reason || "Kvalifisert kandidat"}` : "Ingen kvalifiserte kandidater funnet.");
    } catch (error) {
      window.alert(error.message || "Kunne ikke hente kandidater.");
    }
  }

  return (
    <div className="vaktklar">
      <div className="page-heading action-heading">
        <div><p className="kicker">Vaktklar</p><h1>Bemanning og kompetanse</h1><p>Kontroller at hver vakt har nok folk med riktig kompetanse til arbeidsoppgavene.</p></div>
        <button className="primary-button" onClick={() => mutate?.(() => Promise.resolve(), "Vaktklar er oppdatert.")}>Oppdater kontroll</button>
      </div>

      <nav className="vaktklar-tabs" aria-label="Vaktklar">
        {[['oversikt','Oversikt'],['vakter','Vakter'],['oppgaver','Arbeidsoppgaver'],['ansatte','Ansatte og kompetanse']].map(([id,label]) => <button key={id} className={view === id ? 'active' : ''} onClick={() => setView(id)}>{label}</button>)}
      </nav>

      {view === 'oversikt' && <>
        <div className="metrics">
          <div className="metric-card"><span>Vakter i planen</span><strong>{evaluations.length}</strong><small>Registrerte vakter</small></div>
          <div className="metric-card"><span>Vakter klare</span><strong className="vaktklar-green">{greenCount}</strong><small>Alle krav dekket</small></div>
          <div className="metric-card"><span>Krever handling</span><strong className="vaktklar-red">{redCount}</strong><small>Bemanning eller kompetanse</small></div>
          <div className="metric-card"><span>Ansatte</span><strong>{employees.length}</strong><small>Registrerte i systemet</small></div>
        </div>
        <section className="panel"><div className="panel-heading"><div><h2>Varsler som krever handling</h2><p>Systemet viser hvorfor en vakt ikke er klar.</p></div></div>
          {evaluations.filter((x) => x.result.level === 'bad').map(({ shift, result }) => <div className="vaktklar-alert bad" key={shift.id}><strong>{shift.date} · {shift.shiftType}</strong>{result.issues.map((issue) => <span key={issue.text}>• {issue.text}</span>)}<button className="primary-button" onClick={() => findReplacement(shift)}>Finn kvalifisert erstatter</button></div>)}
          {!redCount && <div className="vaktklar-alert good"><strong>Ingen åpne avvik</strong><span>Alle registrerte vakter tilfredsstiller kravene.</span></div>}
        </section>
      </>}

      {view === 'vakter' && <section className="panel"><div className="panel-heading"><div><h2>Vaktplan og kontroll</h2><p>Rød betyr at et obligatorisk krav mangler.</p></div></div>
        <div className="vaktklar-shifts">{evaluations.map(({ shift, result }) => <article className={`vaktklar-shift ${result.level}`} key={shift.id}>
          <button className="vaktklar-shift-main" onClick={() => setSelectedShiftId(selectedShiftId === shift.id ? null : shift.id)}><div><strong>{shift.date}</strong><span>{shift.shiftType} · {shift.hours} t</span></div><div><strong>{shift.assignedStaff}/{shift.minimumStaff}</strong><span>bemanning</span></div><span className={`status ${result.level === 'ok' ? 'good' : 'bad'}`}>{statusLabel(result.level)}</span></button>
          {selectedShiftId === shift.id && <div className="vaktklar-details"><h3>Hvorfor?</h3>{result.issues.length ? result.issues.map((issue) => <p key={issue.text}>• {issue.text}</p>) : <p className="vaktklar-green">Alle registrerte krav er dekket.</p>}<h3>Registrerte krav</h3>{(shift.requirements || []).map((r) => <p key={r.competence}>{r.competence}: {r.qualifiedCount}/{r.minimumCount} · {r.covered ? 'OK' : 'MANGLER'}</p>)}<button className="primary-button" onClick={() => findReplacement(shift)}>Finn erstatter</button></div>}
        </article>)}</div>
      </section>}

      {view === 'oppgaver' && <div className="two"><section className="panel"><div className="panel-heading"><div><h2>Automatisk oppgaveforslag</h2><p>Forslagene kan redigeres før de blir brukt som krav.</p></div></div><form className="form-grid" onSubmit={generateTasks}><label>Brukere med legemidler<input type="number" min="0" value={generator.meds} onChange={(e) => setGenerator({ ...generator, meds: e.target.value })}/></label><label>Høy fallrisiko<input type="number" min="0" value={generator.falls} onChange={(e) => setGenerator({ ...generator, falls: e.target.value })}/></label><label>Sårstell<input type="number" min="0" value={generator.wounds} onChange={(e) => setGenerator({ ...generator, wounds: e.target.value })}/></label><label>Dokumentasjon<input type="number" min="0" value={generator.docs} onChange={(e) => setGenerator({ ...generator, docs: e.target.value })}/></label><div className="form-actions"><button className="primary-button">Generer forslag</button></div></form>{generated.length > 0 && <div className="vaktklar-generated"><h3>Forslag</h3>{generated.map((task) => <div key={task.name}><strong>{task.name}</strong><span>{task.count} × {task.competency}{task.critical ? ' · Kritisk' : ''}</span></div>)}<button className="primary-button" onClick={acceptGenerated}>Godkjenn forslag</button></div>}</section>
        <section className="panel"><div className="panel-heading"><div><h2>Definer arbeidsoppgave</h2><p>Kravene lagres i prototypen lokalt.</p></div></div><form className="form-grid" onSubmit={addTask}><label>Oppgave<input required value={taskForm.name} onChange={(e) => setTaskForm({ ...taskForm, name: e.target.value })} placeholder="For eksempel insulinadministrasjon"/></label><label>Kompetanse<select required value={taskForm.competency} onChange={(e) => setTaskForm({ ...taskForm, competency: e.target.value })}><option value="">Velg</option>{competences.map((c) => <option key={c.id} value={c.name}>{c.name}</option>)}</select></label><label>Minimum personer<input type="number" min="1" value={taskForm.count} onChange={(e) => setTaskForm({ ...taskForm, count: e.target.value })}/></label><div className="form-actions"><button className="primary-button">Lagre</button></div></form><div className="vaktklar-task-list">{tasks.map((task) => <div key={task.name}><strong>{task.name}</strong><span>{task.count} × {task.competency}{task.critical ? ' · Kritisk' : ''}</span><button className="mini-danger" onClick={() => setTasks((current) => current.filter((x) => x.name !== task.name))}>Fjern</button></div>)}</div></section></div>}

      {view === 'ansatte' && <section className="panel"><div className="panel-heading"><div><h2>Ansatte og kompetanse</h2><p>Data hentes fra den eksisterende backend-en.</p></div></div><div className="vaktklar-staff-grid">{employees.map((employee) => <article key={employee.id}><strong>{employee.name}</strong><span>{employee.role} · {employee.positionPercent}%</span><div>{(employee.competences || []).map((c) => <small key={c.competenceId}>{c.name} · {c.level} {c.status === 'EXPIRED' ? '· UTLØPT' : ''}</small>)}</div></article>)}</div></section>}
    </div>
  );
}
