function downloadBlob(content, filename, type) {
  const blob = new Blob([content], { type });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 0);
}

function csvCell(value) {
  const text = value == null ? "" : String(value);
  return `"${text.replaceAll('"', '""')}"`;
}

function rowsToCsv(headers, rows) {
  return [headers, ...rows].map((row) => row.map(csvCell).join(",")).join("\r\n");
}

function parseCsv(text) {
  const input = String(text || "").replace(/^\uFEFF/, "");
  const delimiter = (input.split(/\r?\n/, 1)[0].match(/;/g) || []).length >
    (input.split(/\r?\n/, 1)[0].match(/,/g) || []).length ? ";" : ",";
  const rows = [];
  let row = [];
  let cell = "";
  let quoted = false;

  for (let i = 0; i < input.length; i += 1) {
    const char = input[i];
    const next = input[i + 1];
    if (char === '"') {
      if (quoted && next === '"') { cell += '"'; i += 1; }
      else quoted = !quoted;
    } else if (char === delimiter && !quoted) {
      row.push(cell); cell = "";
    } else if ((char === "\n" || char === "\r") && !quoted) {
      if (char === "\r" && next === "\n") i += 1;
      row.push(cell); cell = "";
      if (row.some((value) => value.trim() !== "")) rows.push(row);
      row = [];
    } else cell += char;
  }
  if (quoted) throw new Error("CSV contains an unterminated quoted field.");
  row.push(cell);
  if (row.some((value) => value.trim() !== "")) rows.push(row);
  if (rows.length < 2) throw new Error("CSV must contain a header row and at least one data row.");

  const headers = rows[0].map((value) => value.trim().toLowerCase().replace(/\s+/g, ""));
  return rows.slice(1).map((values) => Object.fromEntries(headers.map((header, index) => [header, (values[index] ?? "").trim()])));
}

function firstValue(row, ...names) {
  for (const name of names) {
    const value = row[name.toLowerCase().replace(/\s+/g, "")];
    if (value != null && value !== "") return value;
  }
  return "";
}

function parsePercent(value, fallback = 100) {
  const parsed = Number(String(value || "").replace(",", ".").replace("%", ""));
  return Number.isFinite(parsed) && parsed > 0 && parsed <= 100 ? parsed : fallback;
}

function parseHours(value) {
  const parsed = Number(String(value || "").replace(",", "."));
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

function htmlCell(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

function icsText(value) {
  return String(value ?? "")
    .replaceAll("\\", "\\\\")
    .replaceAll(";", "\\;")
    .replaceAll(",", "\\,")
    .replaceAll(/\r?\n/g, "\\n");
}

function icsLocalDateTime(date, start, hours) {
  const dateText = String(date || "").slice(0, 10);
  const startText = String(start || "08:00").slice(0, 5);
  const [year, month, day] = dateText.split("-").map(Number);
  const [hour, minute] = startText.split(":").map(Number);
  if (![year, month, day, hour, minute].every(Number.isFinite)) throw new Error("Shift contains an invalid date or start time.");
  const startDate = new Date(year, month - 1, day, hour, minute, 0, 0);
  if (Number.isNaN(startDate.getTime())) throw new Error("Shift contains an invalid date or start time.");
  const endDate = new Date(startDate.getTime() + Number(hours || 0) * 60 * 60 * 1000);
  const fmt = (d) => `${d.getFullYear()}${String(d.getMonth() + 1).padStart(2, "0")}${String(d.getDate()).padStart(2, "0")}T${String(d.getHours()).padStart(2, "0")}${String(d.getMinutes()).padStart(2, "0")}00`;
  return [fmt(startDate), fmt(endDate)];
}

function shiftToIcs(shift) {
  const [dtStart, dtEnd] = icsLocalDateTime(shift.date || shift.Date, shift.startTime || shift.StartTime, Number(shift.hours || shift.Hours || 0));
  const id = shift.id || shift.Id;
  const type = shift.shiftType || shift.ShiftType || "Shift";
  const department = shift.department || shift.Department || "";
  return [
    "BEGIN:VEVENT",
    `UID:workforce-shift-${icsText(id)}@workforce-competence`,
    `DTSTART:${dtStart}`,
    `DTEND:${dtEnd}`,
    `SUMMARY:${icsText(type)}`,
    `DESCRIPTION:${icsText(`Department: ${department}; Minimum staffing: ${shift.minimumStaff || shift.MinimumStaff || 0}`)}`,
    "END:VEVENT",
  ].join("\r\n");
}

function validateImport(data) {
  if (!data || typeof data !== "object" || Array.isArray(data)) throw new Error("Invalid JSON backup format.");
  if (data.version && String(data.version) !== "1.0") throw new Error(`Unsupported backup version: ${data.version}`);
  if (!Array.isArray(data.employees) && !Array.isArray(data.competences)) throw new Error("JSON backup must contain employees and/or competences arrays.");
  if ((data.employees || []).length > 1000 || (data.competences || []).length > 1000) throw new Error("Import is limited to 1000 records per collection.");
  for (const c of data.competences || []) if (!c || typeof c !== "object" || !String(c.name || "").trim()) throw new Error("Every competence must contain a name.");
  for (const e of data.employees || []) {
    if (!e || typeof e !== "object" || !String(e.name || "").trim() || !String(e.role || "").trim()) throw new Error("Every employee must contain a name and role.");
    const percent = Number(e.positionPercent ?? 100);
    if (!Number.isFinite(percent) || percent <= 0 || percent > 100) throw new Error("Employee positionPercent must be between 1 and 100.");
  }
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
    try {
      const body = shifts.map(shiftToIcs).join("\r\n");
      downloadBlob(`BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Workforce Competence Management//EN\r\nCALSCALE:GREGORIAN\r\nMETHOD:PUBLISH\r\n${body}\r\nEND:VCALENDAR\r\n`, "shift-plan.ics", "text/calendar;charset=utf-8");
    } catch (error) { mutate(async () => { throw error; }, "Calendar export failed."); }
  }

  function exportHtml() {
    const rows = shifts.map((s) => `<tr><td>${htmlCell(s.date)}</td><td>${htmlCell(s.shiftType)}</td><td>${htmlCell(s.department)}</td><td>${htmlCell(s.minimumStaff)}</td><td>${htmlCell(s.overallStatus)}</td><td>${htmlCell(s.competenceCoverage)}%</td></tr>`).join("");
    const html = `<!doctype html><html lang="en"><head><meta charset="utf-8"><title>Workforce shift plan</title><meta name="robots" content="noindex,nofollow"><style>body{font-family:system-ui;margin:32px}table{border-collapse:collapse;width:100%}th,td{border:1px solid #ccc;padding:8px;text-align:left}th{background:#eee}</style></head><body><h1>Workforce shift plan</h1><p>Generated ${htmlCell(new Date().toLocaleString())}</p><table><thead><tr><th>Date</th><th>Shift</th><th>Department</th><th>Minimum staff</th><th>Status</th><th>Competence coverage</th></tr></thead><tbody>${rows}</tbody></table></body></html>`;
    downloadBlob(html, "shift-plan.html", "text/html;charset=utf-8");
  }

  async function importJson(file) {
    if (file.size > 5 * 1024 * 1024) throw new Error("Import file is limited to 5 MB.");
    const data = JSON.parse(await file.text());
    validateImport(data);

    const competenceByName = new Map(competences.map((c) => [String(c.name).trim().toLowerCase(), c]));
    const employeeByKey = new Map(employees.map((e) => [`${e.name}|${e.role}`.toLowerCase(), e]));
    let created = 0;
    let skipped = 0;

    for (const c of data.competences || []) {
      const name = String(c.name).trim();
      if (competenceByName.has(name.toLowerCase())) { skipped += 1; continue; }
      const createdCompetence = await api.createCompetence({ name, category: String(c.category || "General").trim() });
      competenceByName.set(name.toLowerCase(), createdCompetence);
      created += 1;
    }

    for (const e of data.employees || []) {
      const name = String(e.name).trim();
      const role = String(e.role).trim();
      const key = `${name}|${role}`.toLowerCase();
      if (employeeByKey.has(key)) { skipped += 1; continue; }
      const createdEmployee = await api.createEmployee({
        name,
        role,
        positionPercent: Number(e.positionPercent ?? 100),
        maxWeeklyHours: e.maxWeeklyHours == null ? null : parseHours(e.maxWeeklyHours),
      });
      employeeByKey.set(key, createdEmployee);
      created += 1;

      for (const competence of e.competences || []) {
        const sourceName = String(competence.name || "").trim().toLowerCase();
        const target = competenceByName.get(sourceName);
        if (target) await api.setEmployeeCompetence(createdEmployee.id, { competenceId: target.id, level: competence.level || "Basic", validUntil: competence.validUntil || null });
      }
    }

    await mutate(async () => {}, `JSON import complete: ${created} created, ${skipped} skipped. Shift assignments are not restored automatically.`);
  }

  async function importCsv(file) {
    if (file.size > 5 * 1024 * 1024) throw new Error("Import file is limited to 5 MB.");
    const rows = parseCsv(await file.text());
    if (rows.length > 1000) throw new Error("CSV import is limited to 1000 rows.");

    const name = file.name.toLowerCase();
    const looksLikeCompetence = name.includes("compet") || rows.some((row) => firstValue(row, "category") && !firstValue(row, "role"));
    let created = 0;
    let skipped = 0;

    if (looksLikeCompetence) {
      const existing = new Set(competences.map((c) => c.name.trim().toLowerCase()));
      for (const row of rows) {
        const competenceName = firstValue(row, "name", "competence", "competencename");
        if (!competenceName) throw new Error("Competence CSV requires a Name/Competence column.");
        const key = competenceName.toLowerCase();
        if (existing.has(key)) { skipped += 1; continue; }
        await api.createCompetence({ name: competenceName, category: firstValue(row, "category", "type") || "General" });
        existing.add(key); created += 1;
      }
    } else {
      const existing = new Set(employees.map((e) => `${e.name}|${e.role}`.trim().toLowerCase()));
      for (const row of rows) {
        const employeeName = firstValue(row, "name", "employee", "employeename");
        const role = firstValue(row, "role", "position", "jobtitle");
        if (!employeeName || !role) throw new Error("Employee CSV requires Name and Role columns.");
        const key = `${employeeName}|${role}`.trim().toLowerCase();
        if (existing.has(key)) { skipped += 1; continue; }
        await api.createEmployee({
          name: employeeName,
          role,
          positionPercent: parsePercent(firstValue(row, "positionpercent", "position", "percentage", "percent")),
          maxWeeklyHours: parseHours(firstValue(row, "maxweeklyhours", "weeklyhours", "hoursperweek")),
        });
        existing.add(key); created += 1;
      }
    }

    await mutate(async () => {}, `CSV import complete: ${created} created, ${skipped} skipped.`);
  }

  function handleImport(event) {
    const file = event.target.files?.[0];
    if (!file) return;
    const promise = file.name.toLowerCase().endsWith(".json") ? importJson(file) : importCsv(file);
    promise.catch((error) => mutate(async () => { throw error; }, error.message || "Import failed."));
    event.target.value = "";
  }

  const secondary = "primary-button secondary";

  return (
    <section>
      <div className="page-heading"><div><div className="kicker">Operations</div><h1>Data & Reports</h1><p>Export operational data for analysis, sharing, backup and calendar use.</p></div></div>
      <div className="management-columns">
        <article className="panel"><div className="panel-heading"><div><h2>Exports</h2><p>Browser-side files from the current authenticated dataset.</p></div></div><div className="card-actions">
          <button className="primary-button" onClick={exportJson}>JSON backup</button>
          <button className={secondary} onClick={exportEmployees}>Employees CSV</button>
          <button className={secondary} onClick={exportCompetences}>Competence CSV</button>
          <button className={secondary} onClick={exportShifts}>Shift plan CSV</button>
          <button className={secondary} onClick={exportCalendar}>Calendar ICS</button>
          <button className={secondary} onClick={exportHtml}>Share HTML</button>
          <button className={secondary} onClick={() => window.print()}>Print / PDF</button>
        </div><p className="muted">Exports contain operational employee data. Treat downloaded files as confidential and store them only in approved locations.</p></article>
        <article className="panel"><div className="panel-heading"><div><h2>Import</h2><p>CSV and JSON import with duplicate protection.</p></div></div><p>CSV accepts comma- or semicolon-separated files. Employee imports require Name and Role. Competence imports require Name/Competence and optionally Category. Existing records with the same employee name + role or competence name are skipped.</p><label className={secondary} htmlFor="data-import">Choose CSV or JSON</label><input id="data-import" type="file" accept=".csv,.json,text/csv,application/json" onChange={handleImport} hidden /></article>
      </div>
      <article className="panel" style={{ marginTop: 16 }}><div className="panel-heading"><div><h2>Current dataset</h2><p>Records currently loaded from the API.</p></div></div><div className="metrics"><div className="metric-card"><strong>{employees.length}</strong><small>employees</small></div><div className="metric-card"><strong>{competences.length}</strong><small>competences</small></div><div className="metric-card"><strong>{shifts.length}</strong><small>shifts</small></div></div></article>
    </section>
  );
}
