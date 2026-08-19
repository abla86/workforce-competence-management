import { useEffect, useMemo, useState } from "react";
import "./Vaktklar.css";

const STORAGE_KEY = "vaktklar-task-prototype-v1";
const defaultTasks = [
  { name: "Legemiddelutdeling", competency: "Medication management", count: 1, critical: true },
  { name: "Tilsyn og fallforebygging", competency: "First aid", count: 1, critical: false },
  { name: "Dokumentasjon", competency: "System training", count: 1, critical: false },
];
function loadTasks() { try { return JSON.parse(localStorage.getItem(STORAGE_KEY)) || defaultTasks; } catch { return defaultTasks; } }
function saveTasks(tasks) { localStorage.setItem(STORAGE_KEY, JSON.stringify(tasks)); }
function evaluateShift(shift, tasks) {
  const issues = [];
  if (shift.assignedStaff < shift.minimumStaff) issues.push({ type: "staff", text: `Mangler ${shift.minimumStaff - shift.assignedStaff} person(er)` });
  const requirements = shift.requirements || [];
  requirements.forEach(r => { if (!r.covered) issues.push({ type: "competence", text: `${r.competence}: ${r.qualifiedCount}/${r.minimumCount} kvalifisert` }); });
  tasks.filter(t => shift.taskNames?.includes(t.name)).forEach(t => { const r = requirements.find(x => x.competence === t.competency); if (r && !r.covered) issues.push({ type: "task", text: `${t.name} kan ikke dekkes med registrert kompetanse` }); });
  return { level: issues.length ? "bad" : "ok", issues };
}

export default function Vaktklar({ shifts = [], employees = [], competences = [], api, mutate }) {
  const [tasks, setTasks] = useState(loadTasks);
  const [view, setView] = useState("oversikt");
  const [selected, setSelected] = useState(null);
  const [form, setForm] = useState({ name: "", competency: "", count: 1, critical: false });
  const [generator, setGenerator] = useState({ meds: 8, falls: 2, wounds: 1, docs: 1 });
  const [generated, setGenerated] = useState([]);
  const [candidateState, setCandidateState] = useState({ shiftId: null, loading: false, candidates: [], error: "" });

  useEffect(() => saveTasks(tasks), [tasks]);
  const evaluations = useMemo(() => shifts.map(shift => ({ shift, result: evaluateShift(shift, tasks) })), [shifts, tasks]);
  const red = evaluations.filter(x => x.result.level === "bad").length;

  function addTask(e) { e.preventDefault(); if (!form.name.trim() || !form.competency) return; setTasks(c => [...c.filter(x => x.name !== form.name.trim()), { ...form, name: form.name.trim(), count: Number(form.count) }]); setForm({ name: "", competency: "", count: 1, critical: false }); }
  function generate(e) { e.preventDefault(); const s = []; if (+generator.meds) s.push({ name: "Legemiddelutdeling", competency: "Medication management", count: Math.max(1, Math.ceil(+generator.meds / 12)), critical: true }); if (+generator.falls) s.push({ name: "Tilsyn og fallforebygging", competency: "First aid", count: Math.max(1, Math.ceil(+generator.falls / 5)), critical: false }); if (+generator.wounds) s.push({ name: "Sårstell", competency: competences.find(c => /wound|sår/i.test(c.name))?.name || "Medication management", count: 1, critical: true }); if (+generator.docs) s.push({ name: "Dokumentasjon", competency: "System training", count: 1, critical: false }); setGenerated(s); }
  function accept() { setTasks(c => [...c.filter(x => !new Set(generated.map(g => g.name)).has(x.name)), ...generated]); setGenerated([]); }
  async function replacement(shift) {
    if (!api?.candidates || !shift.id) return;
    setCandidateState({ shiftId: shift.id, loading: true, candidates: [], error: "" });
    try { const candidates = await api.candidates(shift.id); setCandidateState({ shiftId: shift.id, loading: false, candidates, error: "" }); }
    catch (e) { setCandidateState({ shiftId: shift.id, loading: false, candidates: [], error: e.message || "Kunne ikke hente kandidater." }); }
  }

  return <div className="vaktklar">
    <div className="page-heading action-heading"><div><p className="kicker">Vaktklar</p><h1>Bemanning og kompetanse</h1><p>Kontroller at hver vakt har nok folk med riktig kompetanse til arbeidsoppgavene.</p></div><button className="primary-button" onClick={() => mutate?.(() => Promise.resolve(), "Vaktklar er oppdatert.")}>Oppdater kontroll</button></div>
    <nav className="vaktklar-tabs" aria-label="Vaktklar">{[["oversikt", "Oversikt"], ["vakter", "Vakter"], ["oppgaver", "Arbeidsoppgaver"], ["ansatte", "Ansatte og kompetanse"]].map(([id, label]) => <button key={id} className={view === id ? "active" : ""} onClick={() => setView(id)}>{label}</button>)}</nav>
    {view === "oversikt" && <><div className="metrics"><div className="metric-card"><span>Vakter i planen</span><strong>{evaluations.length}</strong><small>Registrerte vakter</small></div><div className="metric-card"><span>Vakter klare</span><strong className="vaktklar-green">{evaluations.length - red}</strong><small>Alle registrerte krav dekket</small></div><div className="metric-card"><span>Krever handling</span><strong className="vaktklar-red">{red}</strong><small>Bemanning eller kompetanse</small></div><div className="metric-card"><span>Ansatte</span><strong>{employees.length}</strong><small>Registrerte i systemet</small></div></div><section className="panel"><div className="panel-heading"><div><h2>Varsler som krever handling</h2><p>Systemet viser hvorfor en vakt ikke er klar.</p></div></div>{evaluations.filter(x => x.result.level === "bad").map(({ shift, result }) => <div className="vaktklar-alert bad" key={shift.id}><strong>{shift.date} · {shift.shiftType}</strong>{result.issues.map(i => <span key={i.text}>• {i.text}</span>)}<button className="primary-button" onClick={() => replacement(shift)}>Finn kvalifisert erstatter</button>{candidateState.shiftId === shift.id && <CandidatePanel state={candidateState} />}</div>)}{!red && <div className="vaktklar-alert good"><strong>Ingen åpne avvik</strong><span>Alle registrerte vakter tilfredsstiller kravene.</span></div>}</section></>}
    {view === "vakter" && <section className="panel"><div className="panel-heading"><div><h2>Vaktplan og kontroll</h2><p>Rød betyr at et obligatorisk krav mangler.</p></div></div><div className="vaktklar-shifts">{evaluations.map(({ shift, result }) => <article className={`vaktklar-shift ${result.level}`} key={shift.id}><button className="vaktklar-shift-main" onClick={() => setSelected(selected === shift.id ? null : shift.id)}><div><strong>{shift.date}</strong><span>{shift.shiftType} · {shift.hours} t</span></div><div><strong>{shift.assignedStaff}/{shift.minimumStaff}</strong><span>bemanning</span></div><span className={`status ${result.level === "ok" ? "good" : "bad"}`}>{result.level === "ok" ? "Klar" : "Ikke klar"}</span></button>{selected === shift.id && <div className="vaktklar-details"><h3>Hvorfor?</h3>{result.issues.length ? result.issues.map(i => <p key={i.text}>• {i.text}</p>) : <p className="vaktklar-green">Alle registrerte krav er dekket.</p>}<h3>Registrerte krav</h3>{(shift.requirements || []).map(r => <p key={r.competence}>{r.competence}: {r.qualifiedCount}/{r.minimumCount} · {r.covered ? "OK" : "MANGLER"}</p>)}<button className="primary-button" onClick={() => replacement(shift)}>Finn erstatter</button>{candidateState.shiftId === shift.id && <CandidatePanel state={candidateState} />}</div>}</article>)}</div></section>}
    {view === "oppgaver" && <div className="two"><section className="panel"><div className="panel-heading"><div><h2>Automatisk oppgaveforslag</h2><p>Forslagene kan redigeres før de brukes som krav.</p></div></div><form className="form-grid" onSubmit={generate}><label>Brukere med legemidler<input type="number" min="0" value={generator.meds} onChange={e => setGenerator({ ...generator, meds: e.target.value })} /></label><label>Høy fallrisiko<input type="number" min="0" value={generator.falls} onChange={e => setGenerator({ ...generator, falls: e.target.value })} /></label><label>Sårstell<input type="number" min="0" value={generator.wounds} onChange={e => setGenerator({ ...generator, wounds: e.target.value })} /></label><label>Dokumentasjon<input type="number" min="0" value={generator.docs} onChange={e => setGenerator({ ...generator, docs: e.target.value })} /></label><div className="form-actions"><button className="primary-button">Generer forslag</button></div></form>{generated.length > 0 && <div className="vaktklar-generated"><h3>Forslag</h3>{generated.map(t => <div key={t.name}><strong>{t.name}</strong><span>{t.count} × {t.competency}{t.critical ? " · Kritisk" : ""}</span></div>)}<button className="primary-button" onClick={accept}>Godkjenn forslag</button></div>}</section><section className="panel"><div className="panel-heading"><div><h2>Definer arbeidsoppgave</h2><p>Kravene lagres lokalt i prototypen.</p></div></div><form className="form-grid" onSubmit={addTask}><label>Oppgave<input required value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} /></label><label>Kompetanse<select required value={form.competency} onChange={e => setForm({ ...form, competency: e.target.value })}><option value="">Velg</option>{competences.map(c => <option key={c.id} value={c.name}>{c.name}</option>)}</select></label><label>Minimum personer<input type="number" min="1" value={form.count} onChange={e => setForm({ ...form, count: e.target.value })} /></label><div className="form-actions"><button className="primary-button">Lagre</button></div></form><div className="vaktklar-task-list">{tasks.map(t => <div key={t.name}><strong>{t.name}</strong><span>{t.count} × {t.competency}{t.critical ? " · Kritisk" : ""}</span><button className="mini-danger" onClick={() => setTasks(c => c.filter(x => x.name !== t.name))}>Fjern</button></div>)}</div></section></div>}
    {view === "ansatte" && <section className="panel"><div className="panel-heading"><div><h2>Ansatte og kompetanse</h2><p>Data hentes fra eksisterende backend.</p></div></div><div className="vaktklar-staff-grid">{employees.map(e => <article key={e.id}><strong>{e.name}</strong><span>{e.role} · {e.positionPercent}%</span><div>{(e.competences || []).map(c => <small key={c.competenceId}>{c.name} · {c.level} {c.status === "EXPIRED" ? "· UTLØPT" : ""}</small>)}</div></article>)}</div></section>}
  </div>;
}

function CandidatePanel({ state }) {
  if (state.loading) return <div className="candidate-panel">Henter kvalifiserte kandidater…</div>;
  if (state.error) return <div className="candidate-panel error">{state.error}</div>;
  if (!state.candidates.length) return <div className="candidate-panel">Ingen kvalifiserte kandidater funnet.</div>;
  return <div className="candidate-panel"><strong>Kandidater</strong>{state.candidates.slice(0, 5).map((c, i) => <div key={c.employeeId ?? c.id ?? i}><span>{c.name}</span><span>{c.score != null ? `${c.score} %` : "Kvalifisert"}</span></div>)}</div>;
}
