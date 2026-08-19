import { useState } from "react";
import { api } from "../services/api.js";

export default function Login({ onLogin }) {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

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
