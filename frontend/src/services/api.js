const API = import.meta.env.VITE_API_URL || "http://localhost:5080";

async function getJson(path) {
  const response = await fetch(`${API}${path}`);
  if (!response.ok) throw new Error(`Request failed: ${response.status}`);
  return response.json();
}

export const api = {
  dashboard: () => getJson("/api/dashboard"),
  employees: () => getJson("/api/employees"),
  competences: () => getJson("/api/competences"),
  shifts: () => getJson("/api/shifts"),
};
