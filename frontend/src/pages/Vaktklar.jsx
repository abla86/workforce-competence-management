import { useCallback, useEffect, useMemo, useState } from "react";
import "./Vaktklar.css";

const STATUS = {
  Green: { label: "Klar", className: "good" },
  Yellow: { label: "Krever kontroll", className: "warning" },
  Red: { label: "Ikke klar", className: "bad" },
};

export default function Vaktklar({ shifts = [], employees = [], competences = [], api, mutate }) {
  const [view, setView] = useState("oversikt");
  const [selected, setSelected] = useState(null);
  const [coverage, setCoverage] = useState({});
  const [loadingCoverage, setLoadingCoverage] = useState(false);
  const [coverageError, setCoverageError] = useState("");
  const [candidateState, setCandidateState] = useState({ shiftId: null, loading: false, candidates: [], error: "" });
  const [scenarioState, setScenarioState] = useState({ shiftId: null, selected: [], loading: false, result: null, error: "" });

  const evaluateAll = useCallback(async () => {
    if (!api?.coverage || shifts.length === 0) {
      setCoverage({});
      return;
    }
    setLoadingCoverage(true);
    setCoverageError("");
    try {
      const results = await Promise.all(shifts.map(async shift => [shift.id, await api.coverage(shift.id)]));
      setCoverage(Object.fromEntries(results));
    } catch (error) {
      setCoverageError(error.message || "Kunne ikke hente Vaktklar-vurdering.");
    } finally {
      setLoadingCoverage(false);
    }
  }, [api, shifts]);

  useEffect(() => { evaluateAll(); }, [evaluateAll]);

  const evaluations = useMemo(() => shifts.map(shift => {
    const result = coverage[shift.id];
    return { shift, result, status: result?.status || "Pending" };
  }), [coverage, shifts]);

  const red = evaluations.filter(x => x.status === "Red").length;
  const yellow = evaluations.filter(x => x.status === "Yellow").length;
  const green = evaluations.filter(x => x.status === "Green").length;

  async function replacement(shift) {
    setCandidateState({ shiftId: shift.id, loading: true, candidates: [], error: "" });
    try {
      const candidates = await api.candidates(shift.id);
      setCandidateState({ shiftId: shift.id, loading: false, candidates, error: "" });
    } catch (error) {
      setCandidateState({ shiftId: shift.id, loading: false, candidates: [], error: error.message || "Kunne ikke hente kandidater." });
    }
  }

  async function simulate(shift) {
    const selectedIds = scenarioState.selected;
    if (!selectedIds.length) return;
    setScenarioState(state => ({ ...state, shiftId: shift.id, loading: true, result: null, error: "" }));
    try {
      const result = await api.coverageScenario(shift.id, selectedIds);
      setScenarioState(state => ({ ...state, loading: false, result, error: "" }));
    } catch (error) {
      setScenarioState(state => ({ ...state, loading: false, result: null, error: error.message || "Scenarioanalysen feilet." }));
    }
  }

  function toggleScenarioEmployee(employeeId) {
    setScenarioState(state => ({
      ...state,
      selected: state.selected.includes(employeeId)
        ? state.selected.filter(id => id !== employeeId)
        : [...state.selected, employeeId],
    }));
  }

  function refresh() {
    mutate?.(evaluateAll, "Vaktklar er oppdatert.");
  }

  const allTasks = useMemo(() => {
    const map = new Map();
    Object.values(coverage).forEach(result => (result?.tasks || []).forEach(task => map.set(task.taskName, task)));
    return [...map.values()].sort((a, b) => a.taskName.localeCompare(b.taskName));
  }, [coverage]);

  return <div className="vaktklar">
    <div className="page-heading action-heading">
      <div><p className="kicker">Vaktklar</p><h1>Bemanning og kompetanse</h1><p>Reell coverage-evaluering fra backend: bemanning, kompetanse, rolle, tilgjengelighet, overlapp og hviletid.</p></div>
      <button className="primary-button" onClick={refresh} disabled={loadingCoverage}>{loadingCoverage ? "Kontrollerer…" : "Oppdater kontroll"}</button>
    </div>

    <nav className="vaktklar-tabs" aria-label="Vaktklar">
      {[["oversikt", "Oversikt"], ["vakter", "Vakter"], ["oppgaver", "Arbeidsoppgaver"], ["ansatte", "Ansatte og kompetanse"]].map(([id, label]) => <button key={id} className={view === id ? "active" : ""} onClick={() => setView(id)}>{label}</button>)}
    </nav>

    {coverageError && <div className="vaktklar-alert bad"><strong>Coverage-feil</strong><span>{coverageError}</span></div>}

    {view === "oversikt" && <>
      <div className="metrics">
        <div className="metric-card"><span>Vakter i planen</span><strong>{evaluations.length}</strong><small>Registrerte vakter</small></div>
        <div className="metric-card"><span>Vakter klare</span><strong className="vaktklar-green">{green}</strong><small>Ingen registrerte avvik</small></div>
        <div className="metric-card"><span>Krever kontroll</span><strong>{yellow}</strong><small>Ikke-kritiske avvik eller bemanningsmangel</small></div>
        <div className="metric-card"><span>Ikke klare</span><strong className="vaktklar-red">{red}</strong><small>Kritisk coverage-avvik</small></div>
      </div>
      <section className="panel">
        <div className="panel-heading"><div><h2>Varsler som krever handling</h2><p>Statusen kommer fra CoverageEvaluationEngine.</p></div></div>
        {evaluations.filter(x => x.status === "Red" || x.status === "Yellow").map(({ shift, result, status }) => {
          const meta = STATUS[status];
          return <div className={`vaktklar-alert ${meta?.className || "bad"}`} key={shift.id}>
            <strong>{shift.date} · {shift.shiftType}</strong>
            {(result?.warnings || []).map(w => <span key={w}>• {w}</span>)}
            {(result?.tasks || []).filter(t => t.gaps?.length).slice(0, 5).map(t => <span key={t.taskName}>• {t.taskName}: {t.gaps[0].description}</span>)}
            <button className="primary-button" onClick={() => replacement(shift)}>Finn kvalifisert erstatter</button>
            {candidateState.shiftId === shift.id && <CandidatePanel state={candidateState} />}
          </div>;
        })}
        {!red && !yellow && <div className="vaktklar-alert good"><strong>Ingen åpne avvik</strong><span>Alle registrerte vakter er vurdert som klare.</span></div>}
      </section>
    </>}

    {view === "vakter" && <section className="panel">
      <div className="panel-heading"><div><h2>Vaktplan og kontroll</h2><p>Trykk på en vakt for detaljert coverage.</p></div></div>
      <div className="vaktklar-shifts">
        {evaluations.map(({ shift, result, status }) => {
          const meta = STATUS[status];
          const open = selected === shift.id;
          return <article className={`vaktklar-shift ${meta?.className || "warning"}`} key={shift.id}>
            <button className="vaktklar-shift-main" onClick={() => setSelected(open ? null : shift.id)}>
              <div><strong>{shift.date}</strong><span>{shift.shiftType} · {shift.hours} t</span></div>
              <div><strong>{shift.assignedStaff}/{shift.minimumStaff}</strong><span>bemanning</span></div>
              <span className={`status ${meta?.className || "warning"}`}>{meta?.label || "Vurderer…"}</span>
            </button>
            {open && <div className="vaktklar-details">
              {!result && <p>Henter coverage…</p>}
              {result && <>
                <h3>Resultat</h3><p><strong>{meta?.label || result.status}</strong></p>
                {(result.warnings || []).map(w => <p key={w}>• {w}</p>)}
                <h3>Arbeidsoppgaver</h3>
                {(result.tasks || []).map(task => <div key={task.taskName} className="task-result"><strong>{task.taskName}</strong><span>{task.actual}/{task.required} · {task.critical ? "Kritisk" : "Ikke-kritisk"}</span>{task.competenceName && <small>Kompetanse: {task.competenceName}</small>}{task.gaps?.map(g => <small key={`${task.taskName}-${g.type}-${g.description}`}>• {g.description}</small>)}</div>)}
                <button className="primary-button" onClick={() => replacement(shift)}>Finn erstatter</button>
                {candidateState.shiftId === shift.id && <CandidatePanel state={candidateState} />}
                <h3>Simuler fravær</h3>
                <div className="scenario-list">
                  {employees.map(employee => <label key={employee.id}><input type="checkbox" checked={scenarioState.selected.includes(employee.id)} onChange={() => toggleScenarioEmployee(employee.id)} />{employee.name}</label>)}
                </div>
                <button className="secondary-button" disabled={scenarioState.loading || !scenarioState.selected.length} onClick={() => simulate(shift)}>{scenarioState.loading ? "Analyserer…" : "Kjør scenario"}</button>
                {scenarioState.shiftId === shift.id && scenarioState.error && <p className="vaktklar-red">{scenarioState.error}</p>}
                {scenarioState.shiftId === shift.id && scenarioState.result && <ScenarioPanel result={scenarioState.result} />}
              </>}
            </div>}
          </article>;
        })}
      </div>
    </section>}

    {view === "oppgaver" && <section className="panel">
      <div className="panel-heading"><div><h2>Registrerte arbeidsoppgaver</h2><p>Dette er oppgavene CoverageEvaluationEngine faktisk vurderer for vaktene.</p></div></div>
      {allTasks.length === 0 && <p>Ingen arbeidsoppgaver er registrert på vaktene ennå.</p>}
      <div className="vaktklar-task-list">{allTasks.map(task => <div key={task.taskName}><strong>{task.taskName}</strong><span>{task.required} × {task.competenceName || "Kompetansekrav ikke angitt"}{task.critical ? " · Kritisk" : ""}</span></div>)}</div>
      <div className="panel-heading"><div><h2>Kompetanser i systemet</h2><p>{competences.length} registrerte kompetanser.</p></div></div>
      <div className="vaktklar-task-list">{competences.map(c => <div key={c.id}><strong>{c.name}</strong><span>{c.category || "Generell"}</span></div>)}</div>
    </section>}

    {view === "ansatte" && <section className="panel">
      <div className="panel-heading"><div><h2>Ansatte og kompetanse</h2><p>Data hentes fra eksisterende backend.</p></div></div>
      <div className="vaktklar-staff-grid">{employees.map(employee => <article key={employee.id}><strong>{employee.name}</strong><span>{employee.role} · {employee.positionPercent}%</span><div>{(employee.competences || []).map(c => <small key={c.competenceId}>{c.name} · {c.level} {c.status === "EXPIRED" ? "· UTLØPT" : ""}</small>)}</div></article>)}</div>
    </section>}
  </div>;
}

function CandidatePanel({ state }) {
  if (state.loading) return <div className="candidate-panel">Henter kvalifiserte kandidater…</div>;
  if (state.error) return <div className="candidate-panel error">{state.error}</div>;
  if (!state.candidates.length) return <div className="candidate-panel">Ingen kvalifiserte kandidater funnet.</div>;
  return <div className="candidate-panel"><strong>Kvalifiserte kandidater</strong>{state.candidates.slice(0, 8).map(candidate => <div key={candidate.employeeId}><span>{candidate.employeeName}</span><span>{candidate.role} · nivå {candidate.competenceLevel || 0}</span></div>)}</div>;
}

function ScenarioPanel({ result }) {
  const status = STATUS[result.coverageWithoutEmployees?.status];
  const candidates = result.suggestedReplacements || [];
  return <div className="candidate-panel">
    <strong>Scenarioresultat: {status?.label || result.coverageWithoutEmployees?.status}</strong>
    {(result.coverageWithoutEmployees?.warnings || []).map(w => <div key={w}><span>{w}</span></div>)}
    {candidates.length > 0 && <><strong>Mulige erstattere</strong>{candidates.slice(0, 5).map(candidate => <div key={candidate.employeeId}><span>{candidate.employeeName}</span><span>{candidate.available ? "Kvalifisert" : candidate.missingRequirements.join("; ")}</span></div>)}</>}
  </div>;
}
