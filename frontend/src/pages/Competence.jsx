import { useState } from "react";
import StatusBadge from "../components/StatusBadge.jsx";

export default function Competence({ employees, competences, api, mutate }) {
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState({ name: "", category: "" });

  function submit(event) {
    event.preventDefault();
    mutate(
      () => api.createCompetence(form),
      "Competence created."
    );
    setForm({ name: "", category: "" });
    setShowForm(false);
  }

  return (
    <>
      <div className="page-heading action-heading">
        <div>
          <p className="kicker">Capability</p>
          <h1>Competence matrix</h1>
          <p>See coverage, proficiency and review status across the workforce.</p>
        </div>
        <button className="primary-button" onClick={() => setShowForm(!showForm)}>
          + Add competence
        </button>
      </div>

      {showForm && (
        <section className="editor-panel">
          <div className="editor-title">
            <h2>New competence</h2>
            <button className="icon-button" onClick={() => setShowForm(false)}>Close</button>
          </div>
          <form className="form-grid" onSubmit={submit}>
            <label>
              Name
              <input
                required
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
              />
            </label>
            <label>
              Category
              <input
                required
                value={form.category}
                onChange={(e) => setForm({ ...form, category: e.target.value })}
              />
            </label>
            <div className="form-actions">
              <button className="primary-button">Create competence</button>
            </div>
          </form>
        </section>
      )}

      <section className="panel">
        <div className="competence-grid">
          {competences.map((competence) => {
            const holders = employees.flatMap((e) =>
              e.competences
                .filter((c) => c.competenceId === competence.id)
                .map((c) => ({ employee: e, competence: c }))
            );

            const active = holders.filter((h) => h.competence.status === "ACTIVE").length;
            const status = holders.length === 0 ? "ACTION_REQUIRED" : active === holders.length ? "GOOD" : "ATTENTION";

            return (
              <article className="competence-card" key={competence.id}>
                <div className="competence-head">
                  <div>
                    <span>{competence.category}</span>
                    <h3>{competence.name}</h3>
                  </div>
                  <div className="competence-score">
                    <strong>{holders.length}</strong>
                    <StatusBadge status={status} />
                  </div>
                </div>

                {holders.map(({ employee, competence: item }) => (
                  <div className="holder" key={employee.id}>
                    <div>
                      <strong>{employee.name}</strong>
                      <span>{item.level}</span>
                    </div>
                    <StatusBadge status={item.status} />
                  </div>
                ))}

                {holders.length === 0 && (
                  <div className="empty-warning">
                    <StatusBadge status="MISSING" />
                    <span>No employees hold this competence.</span>
                  </div>
                )}

                <button
                  className="danger-button subtle"
                  onClick={() => {
                    if (window.confirm(`Delete competence "${competence.name}"?`)) {
                      mutate(() => api.deleteCompetence(competence.id), "Competence deleted.");
                    }
                  }}
                >
                  Delete competence
                </button>
              </article>
            );
          })}
        </div>
      </section>
    </>
  );
}
