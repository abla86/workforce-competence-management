import { useMemo, useState } from "react";
import StatusBadge from "../components/StatusBadge.jsx";

const emptyEmployee = {
  name: "",
  role: "",
  positionPercent: 100,
  isActive: true,
};

export default function Employees({ employees, competences, api, mutate }) {
  const [search, setSearch] = useState("");
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState(emptyEmployee);
  const [skillEmployee, setSkillEmployee] = useState(null);
  const [skillForm, setSkillForm] = useState({
    competenceId: "",
    level: "Basic",
    validUntil: "",
  });

  const filtered = useMemo(
    () =>
      employees.filter((e) =>
        `${e.name} ${e.role}`.toLowerCase().includes(search.toLowerCase())
      ),
    [employees, search]
  );

  function startCreate() {
    setEditing("new");
    setForm(emptyEmployee);
  }

  function startEdit(employee) {
    setEditing(employee.id);
    setForm({
      name: employee.name,
      role: employee.role,
      positionPercent: employee.positionPercent,
      isActive: employee.isActive,
    });
  }

  function submitEmployee(event) {
    event.preventDefault();

    const body = {
      ...form,
      positionPercent: Number(form.positionPercent),
    };

    if (editing === "new") {
      mutate(() => api.createEmployee(body), "Employee added.");
    } else {
      mutate(() => api.updateEmployee(editing, body), "Employee updated.");
    }

    setEditing(null);
  }

  function submitCompetence(event) {
    event.preventDefault();
    if (!skillEmployee || !skillForm.competenceId) return;

    mutate(
      () =>
        api.setEmployeeCompetence(skillEmployee.id, {
          competenceId: Number(skillForm.competenceId),
          level: skillForm.level,
          validUntil: skillForm.validUntil || null,
        }),
      "Competence saved."
    );

    setSkillEmployee(null);
  }

  return (
    <>
      <div className="page-heading action-heading">
        <div>
          <p className="kicker">People</p>
          <h1>Employees</h1>
          <p>Manage workforce profiles, workload and competence status.</p>
        </div>
        <button className="primary-button" onClick={startCreate}>+ Add employee</button>
      </div>

      {editing && (
        <section className="editor-panel">
          <div className="editor-title">
            <div>
              <p className="kicker">{editing === "new" ? "Create" : "Edit"}</p>
              <h2>{editing === "new" ? "New employee" : "Employee profile"}</h2>
            </div>
            <button className="icon-button" onClick={() => setEditing(null)}>Close</button>
          </div>

          <form className="form-grid" onSubmit={submitEmployee}>
            <label>
              Name
              <input
                required
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
              />
            </label>
            <label>
              Role
              <input
                required
                value={form.role}
                onChange={(e) => setForm({ ...form, role: e.target.value })}
              />
            </label>
            <label>
              Position %
              <input
                required
                type="number"
                min="1"
                max="100"
                value={form.positionPercent}
                onChange={(e) => setForm({ ...form, positionPercent: e.target.value })}
              />
            </label>
            <label className="checkbox-label">
              <input
                type="checkbox"
                checked={form.isActive}
                onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
              />
              Active employee
            </label>
            <div className="form-actions">
              <button className="primary-button" type="submit">Save employee</button>
            </div>
          </form>
        </section>
      )}

      {skillEmployee && (
        <section className="editor-panel accent-panel">
          <div className="editor-title">
            <div>
              <p className="kicker">Competence</p>
              <h2>Add or update competence for {skillEmployee.name}</h2>
            </div>
            <button className="icon-button" onClick={() => setSkillEmployee(null)}>Close</button>
          </div>

          <form className="form-grid" onSubmit={submitCompetence}>
            <label>
              Competence
              <select
                required
                value={skillForm.competenceId}
                onChange={(e) => setSkillForm({ ...skillForm, competenceId: e.target.value })}
              >
                <option value="">Select competence</option>
                {competences.map((c) => (
                  <option key={c.id} value={c.id}>{c.name}</option>
                ))}
              </select>
            </label>

            <label>
              Level
              <select
                value={skillForm.level}
                onChange={(e) => setSkillForm({ ...skillForm, level: e.target.value })}
              >
                <option>Basic</option>
                <option>Intermediate</option>
                <option>Advanced</option>
              </select>
            </label>

            <label>
              Valid until
              <input
                type="date"
                value={skillForm.validUntil}
                onChange={(e) => setSkillForm({ ...skillForm, validUntil: e.target.value })}
              />
            </label>

            <div className="form-actions">
              <button className="primary-button" type="submit">Save competence</button>
            </div>
          </form>
        </section>
      )}

      <section className="panel">
        <div className="toolbar">
          <input
            type="search"
            placeholder="Search employees or roles..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          <span>{filtered.length} employees</span>
        </div>

        <div className="employee-grid">
          {filtered.map((employee) => (
            <article className="employee-card" key={employee.id}>
              <div className="avatar">
                {employee.name.split(" ").map((x) => x[0]).slice(0, 2).join("")}
              </div>

              <div className="employee-main">
                <div className="employee-title">
                  <div>
                    <h3>{employee.name}</h3>
                    <p>{employee.role} · {employee.positionPercent}%</p>
                  </div>
                  <StatusBadge status={employee.isActive ? "ACTIVE" : "INACTIVE"} />
                </div>

                <div className="card-actions">
                  <button onClick={() => startEdit(employee)}>Edit</button>
                  <button
                    onClick={() => {
                      setSkillEmployee(employee);
                      setSkillForm({ competenceId: "", level: "Basic", validUntil: "" });
                    }}
                  >
                    + Competence
                  </button>
                  <button
                    className="danger-link"
                    onClick={() => {
                      if (window.confirm(`Delete ${employee.name}?`)) {
                        mutate(() => api.deleteEmployee(employee.id), "Employee deleted.");
                      }
                    }}
                  >
                    Delete
                  </button>
                </div>

                <div className="skills">
                  {employee.competences.map((c) => (
                    <div className="skill-row" key={c.competenceId}>
                      <div>
                        <strong>{c.name}</strong>
                        <span>{c.level} · {c.category}</span>
                      </div>
                      <div className="row-actions">
                        <StatusBadge status={c.status} />
                        <button
                          className="mini-danger"
                          title="Remove competence"
                          onClick={() =>
                            mutate(
                              () => api.removeEmployeeCompetence(employee.id, c.competenceId),
                              "Competence removed."
                            )
                          }
                        >
                          ×
                        </button>
                      </div>
                    </div>
                  ))}

                  {employee.competences.length === 0 && (
                    <p className="muted">No competences registered.</p>
                  )}
                </div>
              </div>
            </article>
          ))}
        </div>
      </section>
    </>
  );
}
