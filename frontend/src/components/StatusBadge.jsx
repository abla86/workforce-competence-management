export default function StatusBadge({ status }) {
  const normalized = (status || "INFO").toUpperCase();
  const good = ["GOOD", "COVERED", "ACTIVE"].includes(normalized);
  const warn = ["REVIEW_DUE", "PARTIAL", "ATTENTION"].includes(normalized);
  const tone = good ? "good" : warn ? "warn" : normalized === "INFO" ? "neutral" : "bad";

  return <span className={`status ${tone}`}>{normalized.replaceAll("_", " ")}</span>;
}
