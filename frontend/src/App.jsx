import { useCallback, useEffect, useState } from "react";
import Sidebar from "./components/Sidebar.jsx";
import Dashboard from "./pages/Dashboard.jsx";
import Employees from "./pages/Employees.jsx";
import Competence from "./pages/Competence.jsx";
import Shifts from "./pages/Shifts.jsx";
import GapAnalysis from "./pages/GapAnalysis.jsx";
import Login from "./pages/Login.jsx";
import { api } from "./services/api.js";

export default function App() {
  const [authenticated, setAuthenticated] = useState(false);
  const [authLoading, setAuthLoading] = useState(true);
  const [user, setUser] = useState(null);
  const [page, setPage] = useState("dashboard");
  const [dashboard, setDashboard] = useState(null);
  const [employees, setEmployees] = useState([]);
  const [competences, setCompetences] = useState([]);
  const [shifts, setShifts] = useState([]);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    api.me().then((me) => { setUser(me); setAuthenticated(true); }).catch(() => {}).finally(() => setAuthLoading(false));
  }, []);

  const reload = useCallback(async () => {
    setLoading(true);
    try {
      const [d, e, c, s] = await Promise.all([api.dashboard(), api.employees(), api.competences(), api.shifts()]);
      setDashboard(d); setEmployees(e); setCompetences(c); setShifts(s); setError("");
    } catch (err) {
      if (err.status === 401) { setAuthenticated(false); setUser(null); }
      setError(err.message || "Kunne ikke hente data fra API-et.");
    } finally { setLoading(false); }
  }, []);

  useEffect(() => { if (authenticated) reload(); }, [authenticated, reload]);

  async function mutate(action, successMessage) {
    try { await action(); setNotice(successMessage); setError(""); await reload(); setTimeout(() => setNotice(""), 2500); }
    catch (err) { setError(err.message || "Handlingen kunne ikke gjennomføres."); }
  }

  async function logout() {
    await api.logout();
    setAuthenticated(false); setUser(null); setDashboard(null);
  }

  if (authLoading) return <div className="loading-state">Laster…</div>;
  if (!authenticated) return <Login onLogin={(nextUser) => { setUser(nextUser); setAuthenticated(true); }} />;

  let content;
  if (loading && !dashboard) content = <div className="loading-state">Laster bemanningsdata…</div>;
  else if (page === "employees") content = <Employees employees={employees} competences={competences} api={api} mutate={mutate} />;
  else if (page === "competence") content = <Competence employees={employees} competences={competences} api={api} mutate={mutate} />;
  else if (page === "shifts") content = <Shifts shifts={shifts} employees={employees} competences={competences} api={api} mutate={mutate} />;
  else if (page === "gaps") content = <GapAnalysis shifts={shifts} />;
  else content = <Dashboard data={dashboard} employees={employees} />;

  return (
    <div className="shell">
      <Sidebar page={page} setPage={setPage} />
      <main className="content">
        <div className="user-bar"><span>{user?.username} · {user?.role}</span><button className="icon-button" onClick={logout}>Logg ut</button></div>
        {notice && <div className="toast success">{notice}</div>}
        {error && <div className="toast error">{error}</div>}
        {content}
      </main>
    </div>
  );
}
