import { useMemo, useState } from "react";

const empty = { name: "", role: "", authorization: "", competence: "", minimumLevel: "Basic", requiredCount: 1, critical: false };

export default function TaskRequirements({ tasks = [], competences = [], onSave }) {
  const [form, setForm] = useState(empty);
  const [query, setQuery] = useState("");

  const filtered = useMemo(() => tasks.filter((task) => task.name.toLowerCase().includes(query.toLowerCase())), [tasks, query]);

  function submit(event) {
    event.preventDefault();
    onSave?.({ ...form, requiredCount: Number(form.requiredCount) });
    setForm(empty);
  }

  return (
    <section className="two">
      <div className="panel">
        <p className="kicker">Arbeidsoppgaver</p>
        <h1>Oppgavekrav</h1>
        <p className="muted">Definer hva som faktisk må kunne utføres på vakten. Kravene brukes av bemanningskontrollen.</p>
        <input type="search" placeholder="Søk arbeidsoppgave..." value={query} onChange={(e) => setQuery(e.target.value)} />
        <div className="task-requirement-list">
          {filtered.map((task) => (
            <article className="task-requirement" key={task.id || task.name}>
              <div>
                <strong>{task.name}</strong>
                <div className="muted">{task.role || "Alle roller"} · {task.competence || "Ingen spesifikk kompetanse"}</div>
              </div>
              <div className="task-meta">
                <span>{task.requiredCount || 1} person(er)</span>
                {task.critical && <span className="status-pill bad">Kritisk</span>}
              </div>
            </article>
          ))}
          {!filtered.length && <p className="muted">Ingen oppgaver funnet.</p>}
        </div>
      </div>

      <div className="panel">
        <p className="kicker">Konfigurasjon</p>
        <h2>Ny arbeidsoppgave</h2>
        <form className="form-grid" onSubmit={submit}>
          <label>Navn<input required value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></label>
          <label>Rolle<input value={form.role} placeholder="Sykepleier" onChange={(e) => setForm({ ...form, role: e.target.value })} /></label>
          <label>Autorisasjon<input value={form.authorization} placeholder="Sykepleier" onChange={(e) => setForm({ ...form, authorization: e.target.value })} /></label>
          <label>Kompetanse<select value={form.competence} onChange={(e) => setForm({ ...form, competence: e.target.value })}><option value="">Ingen spesifikk</option>{competences.map((c) => <option key={c.id || c.name} value={c.name}>{c.name}</option>)}</select></label>
          <label>Minimum nivå<select value={form.minimumLevel} onChange={(e) => setForm({ ...form, minimumLevel: e.target.value })}><option>Basic</option><option>Intermediate</option><option>Advanced</option></select></label>
          <label>Antall personer<input type="number" min="1" required value={form.requiredCount} onChange={(e) => setForm({ ...form, requiredCount: e.target.value })} /></label>
          <label className="checkbox-label"><input type="checkbox" checked={form.critical} onChange={(e) => setForm({ ...form, critical: e.target.checked })} /> Kritisk oppgave</label>
          <div className="form-actions"><button className="primary-button" type="submit">Lagre oppgavekrav</button></div>
        </form>
      </div>
    </section>
  );
}
