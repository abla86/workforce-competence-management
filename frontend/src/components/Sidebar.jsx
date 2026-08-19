const items = [
  ["vaktklar", "Vaktklar"],
  ["plan", "Dagsplan / skiftplan"],
  ["dashboard", "Dashboard"],
  ["employees", "Ansatte"],
  ["competence", "Kompetanse"],
  ["shifts", "Vakter"],
  ["gaps", "Kompetansegap"],
];

export default function Sidebar({ page, setPage }) {
  return (
    <aside className="sidebar">
      <div className="brand">
        <div className="brand-mark">VK</div>
        <div><strong>Vaktklar</strong><span>Bemanning og kompetanse</span></div>
      </div>
      <nav aria-label="Hovedmeny">
        {items.map(([id, label]) => <button key={id} className={page === id ? "active" : ""} onClick={() => setPage(id)}>{label}</button>)}
      </nav>
      <div className="sidebar-note"><strong>Systemstatus</strong><span className="dotline"><i /> API & database tilkoblet</span></div>
    </aside>
  );
}
