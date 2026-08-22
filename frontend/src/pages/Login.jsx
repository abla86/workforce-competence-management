import { useEffect, useState } from "react";
import { api } from "../services/api.js";

const PUBLIC_DEMO_HOST = window.location.hostname === "workforce-frontend.onrender.com";
const DEMO_AUTO_LOGIN = import.meta.env.VITE_DEMO_AUTO_LOGIN === "true" || PUBLIC_DEMO_HOST;
const DEMO_USERNAME = import.meta.env.VITE_DEMO_USERNAME || (PUBLIC_DEMO_HOST ? "demo" : "");
const DEMO_PASSWORD = import.meta.env.VITE_DEMO_PASSWORD || "";

export default function Login({ onLogin }) {
  const [username, setUsername] = useState(DEMO_AUTO_LOGIN ? DEMO_USERNAME : "");
  const [password, setPassword] = useState(DEMO_AUTO_LOGIN ? DEMO_PASSWORD : "");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(DEMO_AUTO_LOGIN);

  useEffect(() => {
    if (!DEMO_AUTO_LOGIN || !DEMO_USERNAME || !DEMO_PASSWORD) {
      setBusy(false);
      return;
    }

    let cancelled = false;
    api.login(DEMO_USERNAME, DEMO_PASSWORD)
      .then((result) => {
        if (!cancelled) onLogin(result.user);
      })
      .catch((err) => {
        if (!cancelled) {
          setError(err.status === 423 ? "Demo-kontoet er midlertidig låst." : "Demo-innloggingen kunne ikke gjennomføres.");
          setBusy(false);
        }
      });

    return () => { cancelled = true; };
  }, [onLogin]);

  async function submit(event) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      const result = await api.login(username, password);
      onLogin(result.user);
    } catch (err) {
      setError(err.status === 423 ? "Kontoen er midlertidig låst." : "Feil brukernavn eller passord.");
    } finally {
      setBusy(false);
    }
  }

  if (DEMO_AUTO_LOGIN && busy && !error) {
    return (
      <main className="login-shell">
        <section className="login-card" aria-labelledby="login-title">
          <div className="login-mark">VK</div>
          <p className="kicker">Vaktklar</p>
          <h1 id="login-title">Åpner demo…</h1>
          <p className="login-subtitle">Klargjør Workforce & Competence Management.</p>
        </section>
      </main>
    );
  }

  return (
    <main className="login-shell">
      <section className="login-card" aria-labelledby="login-title">
        <div className="login-mark">VK</div>
        <p className="kicker">Vaktklar</p>
        <h1 id="login-title">Logg inn</h1>
        <p className="login-subtitle">Bemanning, kompetanse og planlegging samlet på ett sted.</p>
        <form onSubmit={submit} className="login-form">
          <label>Brukernavn<input autoComplete="username" value={username} onChange={(e) => setUsername(e.target.value)} required /></label>
          <label>Passord<input autoComplete="current-password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} required /></label>
          {error && <div className="login-error" role="alert">{error}</div>}
          <button className="primary-button login-button" disabled={busy}>{busy ? "Logger inn…" : "Logg inn"}</button>
        </form>
      </section>
    </main>
  );
}
