const API = import.meta.env.VITE_API_URL || "http://localhost:5080";

async function request(path, options = {}) {
  const response = await fetch(`${API}${path}`, {
    ...options,
    headers: { Accept: "application/json", "Content-Type": "application/json", ...(options.headers || {}) },
    credentials: "same-origin",
  });
  if (!response.ok) {
    let message = `Request failed: ${response.status}`;
    try { const body = await response.json(); message = body.message || message; } catch { /* generic error */ }
    throw new Error(message);
  }
  if (response.status === 204) return null;
  return response.json();
}

export const api = {
  dashboard: () => request("/api/dashboard"),
  employees: () => request("/api/employees"),
  competences: () => request("/api/competences"),
  shifts: () => request("/api/shifts"),
  candidates: (shiftId) => request(`/api/shifts/${shiftId}/candidates`),
  autoStaff: (shiftId, body = {}) => request(`/api/shifts/${shiftId}/auto-staff`, { method: "POST", body: JSON.stringify({ shiftId, ...body }) }),
  teamAvailability: (departmentId, date) => request(`/api/availability/team/${departmentId}${date ? `?date=${encodeURIComponent(date)}` : ""}`),
  employeeStatus: (employeeId, date) => request(`/api/employees/${employeeId}/status${date ? `?date=${encodeURIComponent(date)}` : ""}`),
  setEmployeeStatus: (employeeId, body) => request(`/api/employees/${employeeId}/status`, { method: "PUT", body: JSON.stringify(body) }),
  todayPlan: (departmentId) => request(`/api/dailyplans/today/${departmentId}`),
  dailyPlanHistory: (departmentId, days = 7) => request(`/api/dailyplans/history/${departmentId}?days=${days}`),
  publishDailyPlan: (departmentId) => request(`/api/dailyplans/today/publish/${departmentId}`, { method: "POST" }),
  addDailyTask: (planId, body) => request(`/api/dailyplans/${planId}/tasks`, { method: "POST", body: JSON.stringify(body) }),
  currentShiftPlan: (departmentId) => request(`/api/shiftplans/current/${departmentId}`),
  shiftPlanHistory: (departmentId, count = 5) => request(`/api/shiftplans/history/${departmentId}?count=${count}`),
  createShiftPlan: (body) => request("/api/shiftplans", { method: "POST", body: JSON.stringify(body) }),
  publishShiftPlan: (planId) => request(`/api/shiftplans/${planId}/publish`, { method: "POST" }),
  notifications: () => request("/api/notifications"),
  markNotificationRead: (id) => request(`/api/notifications/${id}/read`, { method: "PUT" }),
  rules: () => request("/api/rules"),
  absences: (employeeId) => request(`/api/absences/${employeeId}`),
  createAbsence: (body) => request("/api/absences", { method: "POST", body: JSON.stringify(body) }),
  createEmployee: (body) => request("/api/employees", { method: "POST", body: JSON.stringify(body) }),
  updateEmployee: (id, body) => request(`/api/employees/${id}`, { method: "PUT", body: JSON.stringify(body) }),
  deleteEmployee: (id) => request(`/api/employees/${id}`, { method: "DELETE" }),
  setEmployeeCompetence: (employeeId, body) => request(`/api/employees/${employeeId}/competences`, { method: "POST", body: JSON.stringify(body) }),
  removeEmployeeCompetence: (employeeId, competenceId) => request(`/api/employees/${employeeId}/competences/${competenceId}`, { method: "DELETE" }),
  createCompetence: (body) => request("/api/competences", { method: "POST", body: JSON.stringify(body) }),
  deleteCompetence: (id) => request(`/api/competences/${id}`, { method: "DELETE" }),
  createShift: (body) => request("/api/shifts", { method: "POST", body: JSON.stringify(body) }),
  updateShift: (id, body) => request(`/api/shifts/${id}`, { method: "PUT", body: JSON.stringify(body) }),
  deleteShift: (id) => request(`/api/shifts/${id}`, { method: "DELETE" }),
  assignEmployee: (shiftId, employeeId) => request(`/api/shifts/${shiftId}/assignments`, { method: "POST", body: JSON.stringify({ employeeId }) }),
  removeAssignment: (shiftId, employeeId) => request(`/api/shifts/${shiftId}/assignments/${employeeId}`, { method: "DELETE" }),
  setShiftRequirement: (shiftId, body) => request(`/api/shifts/${shiftId}/requirements`, { method: "POST", body: JSON.stringify(body) }),
  removeShiftRequirement: (shiftId, competenceId) => request(`/api/shifts/${shiftId}/requirements/${competenceId}`, { method: "DELETE" }),
};
