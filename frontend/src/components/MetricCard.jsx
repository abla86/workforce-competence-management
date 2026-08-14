import StatusBadge from "./StatusBadge.jsx";

export default function MetricCard({ label, value, status, detail }) {
  return (
    <article className="metric-card">
      <div className="metric-top">
        <span>{label}</span>
        <StatusBadge status={status} />
      </div>
      <strong>{value}</strong>
      <small>{detail}</small>
    </article>
  );
}
