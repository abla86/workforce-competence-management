import { useCallback, useEffect, useState } from "react";
import Sidebar from "./components/Sidebar.jsx";
import Dashboard from "./pages/Dashboard.jsx";
import Employees from "./pages/Employees.jsx";
import Competence from "./pages/Competence.jsx";
import Shifts from "./pages/Shifts.jsx";
import GapAnalysis from "./pages/GapAnalysis.jsx";
import { api } from "./services/api.js";

export default function App() {
  const [page, setPage] = useState("dashboard");
  const [dashboard, setDashboard] = useState(null);
  const [employees, setEmployees] = useState([]);
  const [competences, setCompetences] = useState([]);
  const [shifts, setShifts] = useState([]);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [loading, setLoading] = useState(true);

  const reload = useCallback(async () => {
    setLoading(true);
    try {
      const [d, e, c, s] = await Promise.all([
        api.dashboard(),
        api.employees(),
        api.competences(),
        api.shifts(),
      ]);

      setDashboard(d);
      setEmployees(e);
      setCompetences(c);
      setShifts(s);
      setError("");
    } catch (err) {
      setError(err.message || "Could not connect to the API.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    reload();
  }, [reload]);

  async function mutate(action, successMessage) {
    try {
      await action();
      setNotice(successMessage);
      setError("");
      await reload();
      setTimeout(() => setNotice(""), 2500);
    } catch (err) {
      setError(err.message || "The operation failed.");
    }
  }

  let content;

  if (loading && !dashboard) {
    content = <div className="loading-state">Loading workforce data...</div>;
  } else if (page === "employees") {
    content = (
      <Employees
        employees={employees}
        competences={competences}
        api={api}
        mutate={mutate}
      />
    );
  } else if (page === "competence") {
    content = (
      <Competence
        employees={employees}
        competences={competences}
        api={api}
        mutate={mutate}
      />
    );
  } else if (page === "shifts") {
    content = (
      <Shifts
        shifts={shifts}
        employees={employees}
        competences={competences}
        api={api}
        mutate={mutate}
      />
    );
  } else if (page === "gaps") {
    content = <GapAnalysis shifts={shifts} />;
  } else {
    content = <Dashboard data={dashboard} />;
  }

  return (
    <div className="shell">
      <Sidebar page={page} setPage={setPage} />
      <main className="content">
        {notice && <div className="toast success">{notice}</div>}
        {error && <div className="toast error">{error}</div>}
        {content}
      </main>
    </div>
  );
}
