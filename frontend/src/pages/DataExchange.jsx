import { useMemo, useState } from "react";

const XLSX_URL = "https://cdn.sheetjs.com/xlsx-0.20.3/package/dist/xlsx.full.min.js";
const key = (v) => String(v ?? "").trim().toLowerCase();
const num = (v, fallback = 0) => { const n = Number(String(v ?? "").replace(",", ".").replace("%", "")); return Number.isFinite(n) ? n : fallback; };
const isoDate = (v) => { const s = String(v ?? "").slice(0, 10); return /^\d{4}-\d{2}-\d{2}$/.test(s) ? s : null; };
const clock = (v) => { const s = String(v ?? "").slice(0, 5); return /^\d{2}:\d{2}$/.test(s) ? s : null; };
const employeeKey = (e) => `${key(e.name)}|${key(e.role)}`;
const shiftKey = (s) => `${s.date}|${s.startTime || ""}|${key(s.shiftType)}|${key(s.department)}`;

function csvRows(text) {
  const rows = []; let row = []; let cell = ""; let quoted = false;
  for (let i = 0; i < text.length; i += 1) { const c = text[i]; if (c === '"') { if (quoted && text[i + 1] === '"') { cell += '"'; i += 1; } else quoted = !quoted; } else if (c === "," && !quoted) { row.push(cell); cell = ""; } else if ((c === "\n" || c === "\r") && !quoted) { if (c === "\r" && text[i + 1] === "\n") i += 1; row.push(cell); if (row.some((x) => x.trim())) rows.push(row); row = []; cell = ""; } else cell += c; }
  if (cell || row.length) { row.push(cell); if (row.some((x) => x.trim())) rows.push(row); }
  if (!rows.length) return [];
  const headers = rows[0].map((x) => key(x).replaceAll(" ", ""));
  return rows.slice(1).map((values) => Object.fromEntries(headers.map((h, i) => [h, (values[i] ?? "").trim()])));
}
const field = (row, ...names) => { for (const n of names) { const v = row[key(n).replaceAll(" ", "")]; if (v !== undefined && v !== "") return v; } return ""; };

function ensureXlsx() {
  if (window.XLSX) return Promise.resolve(window.XLSX);
  return new Promise((resolve, reject) => { const s = document.createElement("script"); s.src = XLSX_URL; s.onload = () => window.XLSX ? resolve(window.XLSX) : reject(new Error("Excel-motoren kunne ikke lastes.")); s.onerror = () => reject(new Error("Excel-motoren kunne ikke lastes.")); document.head.appendChild(s); });
}

function csvToPayload(text, filename) {
  const rows = csvRows(text); if (!rows.length) throw new Error("CSV-filen er tom.");
  const headers = Object.keys(rows[0]); const isCompetence = headers.includes("competencename") || headers.includes("competence"); const isShift = headers.includes("date") && (headers.includes("shifttype") || headers.includes("shift"));
  if (isCompetence) return { employees: [], competences: rows.map((r) => ({ name: field(r, "CompetenceName", "Competence", "Name"), category: field(r, "Category") || "General" })), shifts: [] };
  if (isShift) return { employees: [], competences: [], shifts: rows.map((r) => ({ date: isoDate(field(r, "Date")), startTime: clock(field(r, "StartTime", "Start")), shiftType: field(r, "ShiftType", "Shift", "Type") || "Shift", department: field(r, "Department"), hours: num(field(r, "Hours"), 8), minimumStaff: num(field(r, "MinimumStaff", "Minimum"), 1), isCritical: key(field(r, "IsCritical")) === "true", isPublished: key(field(r, "IsPublished")) === "true" })) };
  return { employees: rows.map((r) => ({ name: field(r, "Name", "Employee", "EmployeeName"), role: field(r, "Role", "Position", "JobTitle"), department: field(r, "Department"), authorization: field(r, "Authorization"), positionPercent: num(field(r, "PositionPercent", "Percent"), 100), maxWeeklyHours: num(field(r, "MaxWeeklyHours", "WeeklyHours"), 37.5), isActive: key(field(r, "IsActive")) !== "false" })), competences: [], shifts: [] };
}

function jsonToPayload(data) {
  const employees = (data.employees || data.Employees || []).map((e) => ({ name: e.name || e.Name, role: e.role || e.Role, department: e.department || e.Department || "", authorization: e.authorization || e.Authorization || "", positionPercent: num(e.positionPercent ?? e.PositionPercent, 100), maxWeeklyHours: num(e.maxWeeklyHours ?? e.MaxWeeklyHours, 37.5), isActive: e.isActive ?? e.IsActive ?? true, competences: (e.competences || e.Competences || []).map((c) => ({ name: c.name || c.Name, level: c.level || c.Level || "Basic", validUntil: c.validUntil || c.ValidUntil || null })) }));
  const competences = (data.competences || data.Competences || []).map((c) => ({ name: c.name || c.Name, category: c.category || c.Category || "General" }));
  const shifts = (data.shifts || data.Shifts || []).map((s) => ({ date: isoDate(s.date || s.Date), startTime: clock(s.startTime || s.StartTime), shiftType: s.shiftType || s.ShiftType || "Shift", department: s.department || s.Department || "", hours: num(s.hours ?? s.Hours, 8), minimumStaff: num(s.minimumStaff ?? s.MinimumStaff, 1), isCritical: Boolean(s.isCritical ?? s.IsCritical), isPublished: Boolean(s.isPublished ?? s.IsPublished), assignments: (s.assignments || s.Assignments || []).map((a) => a.name ? `${a.name}|${a.role || ""}` : String(a)), requirements: (s.requirements || s.Requirements || []).map((r) => ({ competenceName: r.competenceName || r.Competence?.Name || r.name, minimumCount: num(r.minimumCount ?? r.MinimumCount, 1), minimumLevel: r.minimumLevel || r.MinimumLevel || "Basic", requiredRole: r.requiredRole || r.RequiredRole || null, isCritical: Boolean(r.isCritical ?? r.IsCritical) })) }));
  return { employees, competences, shifts };
}

function icsToPayload(text) {
  const shifts = [];
  for (const block of text.split(/BEGIN:VEVENT/i).slice(1)) {
    const start = block.match(/DTSTART(?:;[^:]+)?:([0-9TZ]+)/i)?.[1]; const end = block.match(/DTEND(?:;[^:]+)?:([0-9TZ]+)/i)?.[1]; if (!start) continue;
    const m = start.match(/^(\d{4})(\d{2})(\d{2})T(\d{2})(\d{2})/); if (!m) continue; const em = end?.match(/^(\d{4})(\d{2})(\d{2})T(\d{2})(\d{2})/); const summary = block.match(/SUMMARY:(.*)/i)?.[1]?.trim() || "Shift"; const description = block.match(/DESCRIPTION:(.*)/i)?.[1] || "";
    const startDate = new Date(`${m[1]}-${m[2]}-${m[3]}T${m[4]}:${m[5]}:00`); const endDate = em ? new Date(`${em[1]}-${em[2]}-${em[3]}T${em[4]}:${em[5]}:00`) : new Date(startDate.getTime() + 8 * 3600000); const minimum = num(description.match(/Minimum staffing:\s*(\d+)/i)?.[1], 1);
    shifts.push({ date: `${m[1]}-${m[2]}-${m[3]}`, startTime: `${m[4]}:${m[5]}`, shiftType: summary, department: description.match(/Department:\s*([^;]+)/i)?.[1]?.trim() || "", hours: Math.max(0.25, (endDate - startDate) / 3600000), minimumStaff: minimum, isCritical: false, isPublished: false });
  }
  return { employees: [], competences: [], shifts };
}

function validate(payload) {
  const errors = [];
  if (!payload.employees.length && !payload.competences.length && !payload.shifts.length) errors.push("Ingen gjenkjennbare poster.");
  payload.employees.forEach((e, i) => { if (!e.name || !e.role) errors.push(`Ansatt ${i + 1}: navn og rolle er påkrevd.`); if (e.positionPercent <= 0 || e.positionPercent > 100) errors.push(`Ansatt ${i + 1}: stillingsprosent må være 1–100.`); });
  payload.competences.forEach((c, i) => { if (!c.name) errors.push(`Kompetanse ${i + 1}: navn mangler.`); });
  payload.shifts.forEach((s, i) => { if (!s.date || !s.shiftType || s.hours <= 0 || s.minimumStaff <= 0) errors.push(`Vakt ${i + 1}: dato, type, timer og minimumsbemanning må være gyldig.`); });
  return errors;
}

export default function DataExchange({ employees, competences, shifts, api, mutate }) {
  const [payload, setPayload] = useState(null); const [file, setFile] = useState(null); const [format, setFormat] = useState(""); const [mode, setMode] = useState("Skip"); const [busy, setBusy] = useState(false); const [message, setMessage] = useState(""); const [error, setError] = useState("");
  const conflicts = useMemo(() => { if (!payload) return []; const out = []; payload.employees.forEach((e) => { if (employees.some((x) => employeeKey(x) === employeeKey(e))) out.push({ type: "Ansatt", key: `${e.name} / ${e.role}` }); }); payload.competences.forEach((c) => { if (competences.some((x) => key(x.name) === key(c.name))) out.push({ type: "Kompetanse", key: c.name }); }); payload.shifts.forEach((s) => { if (shifts.some((x) => shiftKey(x) === shiftKey(s))) out.push({ type: "Vakt", key: `${s.date} ${s.startTime || ""} ${s.shiftType}` }); }); return out; }, [payload, employees, competences, shifts]);

  async function inspect(selected) {
    setBusy(true); setError(""); setMessage(""); setFile(selected); setPayload(null);
    try {
      const name = selected.name.toLowerCase(); let next;
      if (name.endsWith(".json")) next = jsonToPayload(JSON.parse(await selected.text()));
      else if (name.endsWith(".ics")) next = icsToPayload(await selected.text());
      else if (name.endsWith(".csv")) next = csvToPayload(await selected.text(), name);
      else if (name.endsWith(".xlsx") || name.endsWith(".xlsm")) {
        const XLSX = await ensureXlsx(); const wb = XLSX.read(await selected.arrayBuffer(), { type: "array", cellDates: false }); next = { employees: [], competences: [], shifts: [] };
        for (const sheet of wb.SheetNames) { const rows = XLSX.utils.sheet_to_json(wb.Sheets[sheet], { header: 1, raw: false, defval: "" }); if (!rows.length) continue; const headers = rows[0].map((x) => key(x).replaceAll(" ", "")); for (const values of rows.slice(1)) { const r = Object.fromEntries(headers.map((h, i) => [h, values[i] ?? ""])); if (headers.includes("competencename") || key(sheet).includes("compet")) next.competences.push({ name: field(r, "CompetenceName", "Competence", "Name"), category: field(r, "Category") || "General" }); else if (headers.includes("date") || key(sheet).includes("shift") || key(sheet).includes("vakt")) next.shifts.push({ date: isoDate(field(r, "Date")), startTime: clock(field(r, "StartTime", "Start")), shiftType: field(r, "ShiftType", "Shift", "Type") || "Shift", department: field(r, "Department"), hours: num(field(r, "Hours"), 8), minimumStaff: num(field(r, "MinimumStaff", "Minimum"), 1), isCritical: key(field(r, "IsCritical")) === "true", isPublished: false }); else next.employees.push({ name: field(r, "Name", "Employee", "EmployeeName"), role: field(r, "Role", "Position", "JobTitle"), department: field(r, "Department"), authorization: field(r, "Authorization"), positionPercent: num(field(r, "PositionPercent", "Percent"), 100), maxWeeklyHours: num(field(r, "MaxWeeklyHours", "WeeklyHours"), 37.5), isActive: key(field(r, "IsActive")) !== "false" }); } }
      } else throw new Error("Formatet støttes ikke. Bruk CSV, Excel, JSON eller ICS.");
      const errors = validate(next); if (errors.length) throw new Error(errors.slice(0, 10).join(" ")); setPayload(next); setFormat(name.endsWith("xlsx") || name.endsWith("xlsm") ? "Excel" : name.endsWith("json") ? "JSON" : name.endsWith("ics") ? "ICS" : "CSV"); setMessage("Format gjenkjent. Mapping foreslått. Ingenting er lagret ennå.");
    } catch (e) { setError(e.message || "Kunne ikke lese filen."); } finally { setBusy(false); }
  }

  async function confirmImport() {
    if (!payload) return; setBusy(true); setError("");
    try { const result = await api.migrationImport({ ...payload, mode, sourceFileName: file?.name || "manual" }); setMessage(`Import fullført: ${result.created} opprettet, ${result.updated} oppdatert, ${result.skipped} hoppet over. ${result.conflicts?.length || 0} konflikter ble ikke skrevet.`); setPayload(null); setFile(null); await mutate(async () => {}, "Migrering fullført."); } catch (e) { setError(e.message || "Import feilet. Databasen ble rullet tilbake."); } finally { setBusy(false); }
  }

  async function exportExcel() {
    setBusy(true); setError(""); try { const XLSX = await ensureXlsx(); const wb = XLSX.utils.book_new(); XLSX.utils.book_append_sheet(wb, XLSX.utils.json_to_sheet(employees), "Employees"); XLSX.utils.book_append_sheet(wb, XLSX.utils.json_to_sheet(competences), "Competences"); XLSX.utils.book_append_sheet(wb, XLSX.utils.json_to_sheet(shifts), "Shifts"); XLSX.writeFile(wb, "workforce-backup.xlsx"); } catch (e) { setError(e.message); } finally { setBusy(false); }
  }
  function exportJson() { const blob = new Blob([JSON.stringify({ version: "vaktklar-backup-2", exportedAtUtc: new Date().toISOString(), employees, competences, shifts }, null, 2)], { type: "application/json" }); const url = URL.createObjectURL(blob); const a = document.createElement("a"); a.href = url; a.download = "workforce-backup.json"; a.click(); URL.revokeObjectURL(url); }
  function exportIcs() { const events = shifts.map((s) => { const start = new Date(`${s.date}T${s.startTime || "08:00"}:00`); const end = new Date(start.getTime() + Number(s.hours || 8) * 3600000); const fmt = (d) => `${d.getFullYear()}${String(d.getMonth()+1).padStart(2,"0")}${String(d.getDate()).padStart(2,"0")}T${String(d.getHours()).padStart(2,"0")}${String(d.getMinutes()).padStart(2,"0")}00`; return `BEGIN:VEVENT\r\nUID:workforce-${s.id}@vaktklar\r\nDTSTART:${fmt(start)}\r\nDTEND:${fmt(end)}\r\nSUMMARY:${s.shiftType || "Shift"}\r\nDESCRIPTION:Department: ${s.department || ""}; Minimum staffing: ${s.minimumStaff || 0}\r\nEND:VEVENT`; }).join("\r\n"); const blob = new Blob([`BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Vaktklar//EN\r\n${events}\r\nEND:VCALENDAR\r\n`], { type: "text/calendar" }); const url = URL.createObjectURL(blob); const a = document.createElement("a"); a.href = url; a.download = "shift-plan.ics"; a.click(); URL.revokeObjectURL(url); }

  return <section>
    <div className="page-heading"><div><div className="kicker">Operations</div><h1>Data & Migrering 2.0</h1><p>Import, mapping, forhåndsvisning, validering, konflikter og eksport i én arbeidsflyt.</p></div></div>
    {error && <div className="note" role="alert"><strong>Feil:</strong> {error}</div>}{message && <div className="note verified"><strong>Status:</strong> {message}</div>}
    <div className="management-columns">
      <article className="panel"><div className="panel-heading"><div><h2>IMPORT</h2><p>Ingen data skrives før bekreftelse.</p></div></div>
        <label className="primary-button">Last opp fil<input hidden type="file" accept=".csv,.xlsx,.xlsm,.json,.ics" onChange={(e) => e.target.files?.[0] && inspect(e.target.files[0])} /></label>
        <div className="chips"><span className="chip">CSV</span><span className="chip">Excel (.xlsx)</span><span className="chip">JSON</span><span className="chip">ICS</span><span className="chip">Egendefinert fil</span></div>
        {busy && <p className="muted">Behandler …</p>}
        {payload && <div className="import-wizard"><div className="note"><strong>1–5:</strong> {format} gjenkjent · felter lest · mapping foreslått · forhåndsvisning · validering OK.</div>
          <div className="metrics"><div className="metric"><strong>{payload.employees.length}</strong><span>ansatte</span></div><div className="metric"><strong>{payload.competences.length}</strong><span>kompetanser</span></div><div className="metric"><strong>{payload.shifts.length}</strong><span>vakter</span></div><div className="metric"><strong>{conflicts.length}</strong><span>konflikter</span></div></div>
          <label>Konfliktbehandling<select value={mode} onChange={(e) => setMode(e.target.value)}><option value="Skip">Hopp over</option><option value="Update">Oppdater</option><option value="Create">Ikke opprett ved konflikt</option></select></label>
          {conflicts.length > 0 && <div className="table-wrap"><table><thead><tr><th>Type</th><th>Konflikt</th><th>Valg</th></tr></thead><tbody>{conflicts.slice(0, 100).map((c, i) => <tr key={`${c.type}-${c.key}-${i}`}><td>{c.type}</td><td>{c.key}</td><td>{mode === "Skip" ? "Hopp over" : mode === "Update" ? "Oppdater" : "Stopp ved konflikt"}</td></tr>)}</tbody></table></div>}
          <div className="note"><strong>9.</strong> Importen sendes til backend som én batch. EF Core bruker én database-transaksjon; feil ruller hele batchen tilbake. Migreringen logges i auditloggen.</div>
          <div className="card-actions"><button className="primary-button" disabled={busy} onClick={confirmImport}>Bekreft og importer</button><button className="primary-button secondary" onClick={() => { setPayload(null); setFile(null); }}>Avbryt</button></div>
        </div>}
      </article>
      <article className="panel"><div className="panel-heading"><div><h2>EKSPORT</h2><p>Backup, migrering og deling.</p></div></div>
        <div className="card-actions"><button className="primary-button" onClick={exportJson}>Komplett systembackup · JSON</button><button className="primary-button secondary" onClick={exportExcel}>Komplett systembackup · Excel</button><button className="primary-button secondary" onClick={() => api.download("/api/export/employees.csv")}>Ansatte · CSV</button><button className="primary-button secondary" onClick={() => api.download("/api/export/competences.csv")}>Ansatt + kompetanse · CSV</button><button className="primary-button secondary" onClick={() => api.download("/api/export/shifts.xls")}>Vaktplan · Excel</button><button className="primary-button secondary" onClick={exportIcs}>Vaktplan · ICS</button><button className="primary-button secondary" onClick={() => api.download("/api/share/shiftplan")}>Vaktplan · HTML</button><button className="primary-button secondary" onClick={() => window.print()}>PDF / utskrift</button></div>
        <ul className="checks"><li>Ansatt + kompetanse gir grunnlag for kompetansematrise.</li><li>Excel-eksport har egne ark for Employees, Competences og Shifts.</li><li>JSON-backup kan importeres tilbake gjennom samme migreringsflyt.</li><li>ICS kan importeres som vakter.</li><li>Migreringsresultatet lagres som audit-event.</li></ul>
      </article>
    </div>
  </section>;
}
