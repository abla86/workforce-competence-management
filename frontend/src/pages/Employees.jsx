import { useMemo, useState } from "react";
import StatusBadge from "../components/StatusBadge.jsx";

export default function Employees({ employees }) {
  const [search, setSearch] = useState("");

  const filtered = useMemo(
    () => employees.filter((e) =>
      `${e.name} ${e.role}`.toLowerCase().includes(search.toLowerCase())
    ),
    [employees, search]
  );

  return (
    <>
      <div className="page-heading">
        <div>
          <p className="kicker">People</p>
          <h1>Employees</h1>
          <p>Workforce profiles, roles, position percentages and competence status.</p>
        </div>
      </div>

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
              <div className="avatar">{employee.name.split(" ").map(x => x[0]).slice(0,2).join("")}</div>
              <div className="employee-main">
                <div className="employee-title">
                  <div>
                    <h3>{employee.name}</h3>
                    <p>{employee.role} · {employee.positionPercent}%</p>
                  </div>
                  <StatusBadge status={employee.isActive ? "ACTIVE" : "INACTIVE"} />
                </div>

                <div className="skills">
                  {employee.competences.map((c) => (
                    <div className="skill-row" key={c.competenceId}>
                      <div>
                        <strong>{c.name}</strong>
                        <span>{c.level} · {c.category}</span>
                      </div>
                      <StatusBadge status={c.status} />
                    </div>
                  ))}
                  {employee.competences.length === 0 && <p className="muted">No competences registered.</p>}
                </div>
              </div>
            </article>
          ))}
        </div>
      </section>
    </>
  );
}
