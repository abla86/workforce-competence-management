import { useState } from "react";
import StatusBadge from "../components/StatusBadge.jsx";

const today = new Date().toISOString().slice(0, 10);

export default function Shifts({ shifts, employees, competences, api, mutate }) {
  const [showCreate, setShowCreate] = useState(false);
  const [shiftForm, setShiftForm] = useState({
    date: today,
    shiftType: "Day",
    hours: 7.5,
    minimumStaff: 2,
  });
  const [manageShift, setManageShift] = useState(null);
  const [employeeId, setEmployeeId] = useState("");
  const [requirement, setRequirement] = useState({
    competenceId: "",
    minimumCount: 1,
    minimumLevel: "Basic",
  });

  function createShift(event) {
    event.preventDefault();
    mutate(
      () =>
        api.createShift({
          ...shiftForm,
          hours: Number(shiftForm.hours),
          minimumStaff: Number(shiftForm.minimumStaff),
        }),
      "Shift created."
    );
    setShowCreate(false);
  }

  function addAssignment(event) {
    event.preventDefault();
    if (!manageShift || !employeeId) return;

    mutate(
      () => api.assignEmployee(manageShift.id, Number(employeeId)),
      "Employee assigned."
    );
    setEmployeeId("");
  }

  function addRequirement(event) {
    event.preventDefault();
    if (!manageShift || !requirement.competenceId) return;

    mutate(
      () =>
        api.setShiftRequirement(manageShift.id, {
          competenceId: Number(requirement.competenceId),
          minimumCount: Number(requirement.minimumCount),
          minimumLevel: requirement.minimumLevel,
        }),
      "Requirement saved."
    );
  }

  const current = manageShift
    ? shifts.find((shift) => shift.id === manageShift.id) || manageShift
    : null;

  return (
    <>
      <div className="page-heading action-heading">
        <div>
          <p className="kicker">Planning</p>
          <h1>Shifts</h1>
          <p>Create shifts, assign employees and define competence requirements.</p>
        </div>
        <button className="primary-button" onClick={() => setShowCreate(!showCreate)}>
          + New shift
        </button>
      </div>

      {showCreate && (
        <section className="editor-panel">
          <div className="editor-title">
            <h2>Create shift</h2>
            <button className="icon-button" onClick={() => setShowCreate(false)}>Close</button>
          </div>

          <form className="form-grid" onSubmit={createShift}>
            <label>
              Date
              <input
                type="date"
                required
                value={shiftForm.date}
                onChange={(e) => setShiftForm({ ...shiftForm, date: e.target.value })}
              />
            </label>
            <label>
              Shift type
              <select
                value={shiftForm.shiftType}
                onChange={(e) => setShiftForm({ ...shiftForm, shiftType: e.target.value })}
              >
                <option>Day</option>
                <option>Evening</option>
                <option>Night</option>
              </select>
            </label>
            <label>
              Hours
              <input
                type="number"
                step="0.5"
                min="0.5"
                max="24"
                value={shiftForm.hours}
                onChange={(e) => setShiftForm({ ...shiftForm, hours: e.target.value })}
              />
            </label>
            <label>
              Minimum staff
              <input
                type="number"
                min="1"
                value={shiftForm.minimumStaff}
                onChange={(e) => setShiftForm({ ...shiftForm, minimumStaff: e.target.value })}
              />
            </label>
            <div className="form-actions">
              <button className="primary-button">Create shift</button>
            </div>
          </form>
        </section>
      )}

      {current && (
        <section className="editor-panel accent-panel">
          <div className="editor-title">
            <div>
              <p className="kicker">Manage shift</p>
              <h2>{current.date} · {current.shiftType}</h2>
            </div>
            <button className="icon-button" onClick={() => setManageShift(null)}>Close</button>
          </div>

          <div className="management-columns">
            <div>
              <h3>Assigned employees</h3>
              <form className="inline-form" onSubmit={addAssignment}>
                <select value={employeeId} onChange={(e) => setEmployeeId(e.target.value)} required>
                  <option value="">Select employee</option>
                  {employees
                    .filter((e) => e.isActive && !current.assignments.some((a) => a.employeeId === e.id))
                    .map((e) => <option key={e.id} value={e.id}>{e.name} · {e.role}</option>)}
                </select>
                <button className="primary-button">Assign</button>
              </form>

              <div className="manage-list">
                {current.assignments.map((assignment) => (
                  <div className="manage-row" key={assignment.employeeId}>
                    <div>
                      <strong>{assignment.name}</strong>
                      <span>{assignment.role}</span>
                    </div>
                    <button
                      className="mini-danger"
                      onClick={() =>
                        mutate(
                          () => api.removeAssignment(current.id, assignment.employeeId),
                          "Assignment removed."
                        )
                      }
                    >
                      Remove
                    </button>
                  </div>
                ))}
              </div>
            </div>

            <div>
              <h3>Competence requirements</h3>
              <form className="requirement-form" onSubmit={addRequirement}>
                <select
                  required
                  value={requirement.competenceId}
                  onChange={(e) => setRequirement({ ...requirement, competenceId: e.target.value })}
                >
                  <option value="">Select competence</option>
                  {competences.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                </select>
                <input
                  type="number"
                  min="1"
                  value={requirement.minimumCount}
                  onChange={(e) => setRequirement({ ...requirement, minimumCount: e.target.value })}
                />
                <select
                  value={requirement.minimumLevel}
                  onChange={(e) => setRequirement({ ...requirement, minimumLevel: e.target.value })}
                >
                  <option>Basic</option>
                  <option>Intermediate</option>
                  <option>Advanced</option>
                </select>
                <button className="primary-button">Save requirement</button>
              </form>

              <div className="manage-list">
                {current.requirements.map((r) => (
                  <div className={`manage-row status-row ${r.covered ? "covered-row" : "missing-row"}`} key={r.competenceId}>
                    <div>
                      <strong>{r.competence}</strong>
                      <span>{r.qualifiedCount}/{r.minimumCount} qualified · {r.minimumLevel}</span>
                    </div>
                    <div className="row-actions">
                      <StatusBadge status={r.status} />
                      <button
                        className="mini-danger"
                        onClick={() =>
                          mutate(
                            () => api.removeShiftRequirement(current.id, r.competenceId),
                            "Requirement removed."
                          )
                        }
                      >
                        ×
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </section>
      )}

      <div className="shift-grid">
        {shifts.map((shift) => (
          <article className={`shift-card ${shift.overallCovered ? "covered" : "gap"}`} key={shift.id}>
            <div className="shift-top">
              <div>
                <span>{shift.date} · {shift.hours} h</span>
                <h3>{shift.shiftType}</h3>
              </div>
              <StatusBadge status={shift.overallStatus} />
            </div>

            <div className={`coverage-banner ${shift.staffingCovered ? "green-banner" : "red-banner"}`}>
              <div>
                <span>Staffing</span>
                <strong>{shift.assignedStaff} / {shift.minimumStaff}</strong>
              </div>
              <StatusBadge status={shift.staffingStatus} />
            </div>

            <div className="requirements">
              {shift.requirements.map((r) => (
                <div className={`requirement ${r.covered ? "good-requirement" : "bad-requirement"}`} key={r.competenceId}>
                  <div>
                    <strong>{r.competence}</strong>
                    <span>{r.qualifiedCount} / {r.minimumCount} · min. {r.minimumLevel}</span>
                  </div>
                  <StatusBadge status={r.status} />
                </div>
              ))}
              {shift.requirements.length === 0 && (
                <p className="muted">No competence requirements defined.</p>
              )}
            </div>

            <div className="shift-actions">
              <button className="primary-button secondary" onClick={() => setManageShift(shift)}>
                Manage shift
              </button>
              <button
                className="danger-button"
                onClick={() => {
                  if (window.confirm(`Delete ${shift.date} ${shift.shiftType}?`)) {
                    mutate(() => api.deleteShift(shift.id), "Shift deleted.");
                  }
                }}
              >
                Delete
              </button>
            </div>
          </article>
        ))}
      </div>
    </>
  );
}
