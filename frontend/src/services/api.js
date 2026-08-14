const API = import.meta.env.VITE_API_URL || "http://localhost:5080";

async function request(path, options = {}) {
  const response = await fetch(`${API}${path}`, {
    headers: {
      "Content-Type": "application/json",
      ...(options.headers || {}),
    },
    ...options,
  });

  if (!response.ok) {
    let message = `Request failed: ${response.status}`;
    try {
      const body = await response.json();
      message = body.message || message;
    } catch {
      // Keep generic status message.
    }
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

  createEmployee: (body) =>
    request("/api/employees", { method: "POST", body: JSON.stringify(body) }),

  updateEmployee: (id, body) =>
    request(`/api/employees/${id}`, { method: "PUT", body: JSON.stringify(body) }),

  deleteEmployee: (id) =>
    request(`/api/employees/${id}`, { method: "DELETE" }),

  setEmployeeCompetence: (employeeId, body) =>
    request(`/api/employees/${employeeId}/competences`, {
      method: "POST",
      body: JSON.stringify(body),
    }),

  removeEmployeeCompetence: (employeeId, competenceId) =>
    request(`/api/employees/${employeeId}/competences/${competenceId}`, {
      method: "DELETE",
    }),

  createCompetence: (body) =>
    request("/api/competences", { method: "POST", body: JSON.stringify(body) }),

  deleteCompetence: (id) =>
    request(`/api/competences/${id}`, { method: "DELETE" }),

  createShift: (body) =>
    request("/api/shifts", { method: "POST", body: JSON.stringify(body) }),

  updateShift: (id, body) =>
    request(`/api/shifts/${id}`, { method: "PUT", body: JSON.stringify(body) }),

  deleteShift: (id) =>
    request(`/api/shifts/${id}`, { method: "DELETE" }),

  assignEmployee: (shiftId, employeeId) =>
    request(`/api/shifts/${shiftId}/assignments`, {
      method: "POST",
      body: JSON.stringify({ employeeId }),
    }),

  removeAssignment: (shiftId, employeeId) =>
    request(`/api/shifts/${shiftId}/assignments/${employeeId}`, {
      method: "DELETE",
    }),

  setShiftRequirement: (shiftId, body) =>
    request(`/api/shifts/${shiftId}/requirements`, {
      method: "POST",
      body: JSON.stringify(body),
    }),

  removeShiftRequirement: (shiftId, competenceId) =>
    request(`/api/shifts/${shiftId}/requirements/${competenceId}`, {
      method: "DELETE",
    }),
};
