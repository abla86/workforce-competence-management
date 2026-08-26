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

function csvToPayload(text) {
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
