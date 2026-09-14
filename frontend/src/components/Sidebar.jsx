const items = [
  ["dashboard", "Dashboard"],
  ["employees", "Employees"],
  ["competence", "Competence"],
  ["shifts", "Shifts"],
  ["gaps", "Gap Analysis"],
  ["data", "Data & Reports"],
];

export default function Sidebar({ page, setPage }) {
  return (
    <aside className="sidebar">
      <div className="brand">
        <div className="brand-mark">WC</div>
        <div>
          <strong>Workforce</strong>
          <span>Competence Management</span>
        </div>
      </div>

      <nav>
        {items.map(([id, label]) => (
          <button
            key={id}
            className={page === id ? "active" : ""}
            onClick={() => setPage(id)}
          >
            {label}
          </button>
        ))}
      </nav>

      <div className="sidebar-note">
        <strong>System status</strong>
        <span className="dotline"><i /> API & database connected</span>
      </div>
    </aside>
  );
}
