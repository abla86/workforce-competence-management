import StatusBadge from "../components/StatusBadge.jsx";

export default function Competence({ employees, competences }) {
  return (
    <>
      <div className="page-heading">
        <div>
          <p className="kicker">Capability</p>
          <h1>Competence matrix</h1>
          <p>See who holds each competence and the registered proficiency level.</p>
        </div>
      </div>

      <section className="panel">
        <div className="competence-grid">
          {competences.map((competence) => {
            const holders = employees.flatMap(e =>
              e.competences
                .filter(c => c.competenceId === competence.id)
                .map(c => ({ employee: e, competence: c }))
            );

            return (
              <article className="competence-card" key={competence.id}>
                <div className="competence-head">
                  <div>
                    <span>{competence.category}</span>
                    <h3>{competence.name}</h3>
                  </div>
                  <strong>{holders.length}</strong>
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

                {holders.length === 0 && <p className="muted">No employees registered.</p>}
              </article>
            );
          })}
        </div>
      </section>
    </>
  );
}
