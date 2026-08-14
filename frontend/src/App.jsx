import { useEffect, useState } from "react";
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

  useEffect(() => {
    Promise.all([api.dashboard(), api.employees(), api.competences(), api.shifts()])
      .then(([d, e, c, s]) => {
        setDashboard(d);
        setEmployees(e);
        setCompetences(c);
        setShifts(s);
      })
      .catch(() => setError("Could not connect to the API. Start the backend on http://localhost:5080."));
  }, []);

  let content;
  if (error) {
    content = <div className="error-state"><strong>Connection error</strong><p>{error}</p></div>;
  } else if (page === "employees") {
    content = <Employees employees={employees} />;
  } else if (page === "competence") {
    content = <Competence employees={employees} competences={competences} />;
  } else if (page === "shifts") {
    content = <Shifts shifts={shifts} />;
  } else if (page === "gaps") {
    content = <GapAnalysis shifts={shifts} />;
  } else {
    content = <Dashboard data={dashboard} />;
  }

  return (
    <div className="shell">
      <Sidebar page={page} setPage={setPage} />
      <main className="content">{content}</main>
    </div>
  );
}
