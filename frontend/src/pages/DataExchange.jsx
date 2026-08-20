function downloadBlob(content, filename, type) {
  const blob = new Blob([content], { type });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(url);
}

function csvCell(value) {
  const text = value == null ? "" : String(value);
  return `"${text.replaceAll('"', '""')}"`;
}

function rowsToCsv(headers, rows) {
  return [headers, ...rows].map((row) => row.map(csvCell).join(",")).join("\n");
}

function isoDate(value) {
  return value instanceof Date ? value.toISOString().slice(0, 10) : String(value || "");
}

function shiftToIcs(shift) {
  const date = shift.date || shift.Date;
  const start = shift.startTime || shift.StartTime || "08:00:00";
  const hours = Number(shift.hours || shift.Hours || 0);
  const [h, m] = String(start).slice(0, 5).split(":").map(Number);
  const startDate = new Date(`${date}T${String(h).padStart(2, "0")}:${String(m).padStart(2, "0")}:00`);
  const endDate = new Date(startDate.getTime() + hours * 60 * 60 * 1000);
  const fmt = (d) => d.toISOString().replaceAll("-", "").replaceAll(":", "").replace(/\.\d{3}Z$/, "Z");
  return `BEGIN:VEVENT\nUID:workforce-shift-${shift.id || shift.Id}@workforce-competence\nDTSTART:${fmt(startDate)}\nDTEND:${fmt(endDate)}\nSUMMARY:${String(shift.shiftType || shift.ShiftType || "Shift").replaceAll("\n", " ")}\nDESCRIPTION:Minimum staffing ${shift.minimumStaff || shift.MinimumStaff || 0}\nEND:VEVENT`;
}

export default function DataExchange({ employees, competences, shifts, api, mutate }) {
  function exportJson() {
    const payload = { exportedAtUtc: new Date().toISOString(), version: "1.0", employees, competences, shifts };
    downloadBlob(JSON.stringify(payload, null, 2), "workforce-backup.json", "application/json;charset=utf-8");
  }

  function exportEmployees() {
    const rows = employees.map((e) => [e.id, e.name, e.role, e.department, e.positionPercent, e.maxWeeklyHours, e.isActive]);
    downloadBlob(rowsToCsv(["Id", "Name", "Role", "Department", "PositionPercent", "MaxWeeklyHours", "IsActive"], rows), "employees.csv", "text/csv;charset=utf-8");
  }

  function exportCompetences() {
    const rows = competences.map((c) => [c.id, c.name, c.category]);
    downloadBlob(rowsToCsv(["Id", "Name", "Category"], rows), "competences.csv", "text/csv;charset=utf-8");
  }

  function exportShifts() {
    const rows = shifts.map((s) => [s.id, s.date, s.shiftType, s.department, s.startTime, s.hours, s.minimumStaff, s.overallStatus, s.competenceCoverage]);
    downloadBlob(rowsToCsv(["Id", "Date", "ShiftType", "Department", "StartTime", "Hours", "MinimumStaff", "Status", "CompetenceCoverage"], rows), "shift-plan.csv", "text/csv;charset=utf-8");
  }

  function exportCalendar() {
    const body = shifts.map(shiftToIcs).join("\n");
    downloadBlob(`BEGIN:VCALENDAR\nVERSION:2.0\nPRODID:-//Workforce Competence Management//EN\n${body}\nEND:VCALENDAR\n`, "shift-plan.ics", "text/calendar;charset=utf-8");
  }

  function exportHtml() {
    const rows = shifts.map((s) => `<tr><td>${s.date || ""}</td><td>${s.shiftType || ""}</td><td>${s.department || ""}</td><td>${s.minimumStaff ?? ""}</td><td>${s.overallStatus || ""}</td><td>${s.competenceCoverage ?? ""}%</td></tr>`).join("");
    const html = `<!doctype html><html lang="en"><head><meta charset="utf-8"><title>Workforce shift plan</title><style>body{font-family:system-ui;margin:32px}table{border-collapse:collapse;width:100%}th,td{border:1px solid #ccc;padding:8px;text-align:left}th{background:#eee}</style></head><body><h1>Workforce shift plan</h1><p>Generated ${new Date().toLocaleString()}</p><table><thead><tr><th>Date</th><th>Shift</th><th>Department</th><th>Minimum staff</th><th>Status</th><th>Competence coverage</th></tr></thead><tbody>${rows}</tbody></table></body></html>`;
    downloadBlob(html, "shift-plan.html", "text/html;charset=utf-8");
  }

  async function importJson(file) {
    const text = await file.text();
    const data = JSON.parse(text);
    if (!Array.isArray(data.employees) && !Array.isArray(data.competences)) throw new Error("JSON backup must contain employees and/or competences arrays.");
    const competenceByName = new Map(competences.map((c) => [c.name.toLowerCase(), c]));
    let created = 0;
    for (const c of data.competences || []) {
      if (!c?.name || competenceByName.has(String(c.name).toLowerCase())) continue;
      await api.createCompetence({ name: String(c.name).trim(), category: String(c.category || "General").trim() });
      created += 1;
    }
    for (const e of data.employees || []) {
      if (!e?.name || !e?.role) continue;
      await api.createEmployee({ name: String(e.name).trim(), role: String(e.role).trim(), positionPercent: Number(e.positionPercent || 100) });
      created += 1;
    }
    await mutate(async () => {}, `Import complete: ${created} records submitted.`);
  }

  function handleImport(event) {
    const file = event.target.files?.[0];
    if (!file) return;
    importJson(file).catch((error) => mutate(async () => { throw error; }, "Import failed."));
    event.target.value = "";
  }

  return (
    <section>
      <div className="page-header"><div><h1>Data & Reports</h1><p>Export operational data for analysis, sharing, backup and calendar use.</p></div></div>
      <div className="grid two-col">
        <article className="panel"><h2>Exports</h2><div className="button-row">
          <button className="primary-button" onClick={exportJson}>JSON backup</button>
          <button className="secondary-button" onClick={exportEmployees}>Employees CSV</button>
          <button className="secondary-button" onClick={exportCompetences}>Competence CSV</button>
          <button className="secondary-button" onClick={exportShifts}>Shift plan CSV</button>
          <button className="secondary-button" onClick={exportCalendar}>Calendar ICS</button>
          <button className="secondary-button" onClick={exportHtml}>Share HTML</button>
          <button className="secondary-button" onClick={() => window.print()}>Print / PDF</button>
        </div><p className="muted">PDF uses the browser print dialog so the user can choose a printer or “Save as PDF”.</p></article>
        <article className="panel"><h2>Import</h2><p>Import a JSON backup into the current environment. Existing competence names are not duplicated. Employee records are added as new records.</p><label className="secondary-button" htmlFor="backup-import">Choose JSON backup</label><input id="backup-import" type="file" accept="application/json,.json" onChange={handleImport} hidden /></article>
      </div>
      <article className="panel"><h2>Current dataset</h2><div className="metrics"><div><strong>{employees.length}</strong><span>employees</span></div><div><strong>{competences.length}</strong><span>competences</span></div><div><strong>{shifts.length}</strong><span>shifts</span></div></div></article>
    </section>
  );
}
