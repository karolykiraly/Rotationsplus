import { useState } from "react";
import { useParams } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useMe } from "../useMe";
import { Tabs } from "../components/Tabs";
import { getStudent, type StudentDetail } from "../api";
import { StudentPersonalInfoTab } from "./StudentPersonalInfoTab";
import { StudentNeedsTab } from "./StudentNeedsTab";

/** Admin Student Profile (legacy StudentProfile.js, route `/admin/students/:id`) — the name-link target
 *  from the Contacts → Students tab. Seven sub-tabs match production; Personal Information is built here,
 *  the rest land in their own slices (Needs, Education, Rotations, Achievements, Documents, Sales). */
const TAB_LABELS = ["Personal information", "Needs", "Education", "Rotations", "Achievements", "Documents", "Sales"];

function ComingSoon({ label }: { label: string }) {
  return (
    <div className="state">
      The <strong>{label}</strong> tab is being ported next — it isn’t available yet.
    </div>
  );
}

export function StudentProfilePage() {
  const { id = "" } = useParams();
  const { user } = useMe();
  const [tab, setTab] = useState(0);
  const queryClient = useQueryClient();

  const detail = useQuery({ queryKey: ["student", id], queryFn: () => getStudent(id), enabled: !!id });

  if (user && !user.isAdmin) {
    return <div className="lead-page state">You need the Admin role to view a student profile.</div>;
  }

  if (detail.isLoading) return <div className="lead-page state">Loading profile…</div>;
  if (detail.isError || !detail.data) {
    return <div className="lead-page state">Couldn’t load this student{detail.error ? `: ${(detail.error as Error).message}` : "."}</div>;
  }

  const student = detail.data;
  const location = [student.city, student.state].filter(Boolean).join(", ");
  const onSaved = (updated: StudentDetail) => {
    queryClient.setQueryData(["student", id], updated);
    // Reflect the edit on the directory list + form pickers (name/type change immediately, like production).
    void queryClient.invalidateQueries({ queryKey: ["students"] });
    void queryClient.invalidateQueries({ queryKey: ["student-options"] });
  };

  return (
    <div className="profile-page">
      <div className="profile-header">
        <div className="profile-avatar" aria-hidden="true">
          <span className="profile-avatar-icon">📷</span>
        </div>
        <div className="profile-name heading-xs">{student.firstName} {student.lastName}</div>
        {location && <div className="profile-location">📍 {location}</div>}
      </div>

      <Tabs labels={TAB_LABELS} active={tab} onChange={setTab} />

      <div className="profile-body">
        {tab === 0 ? (
          <StudentPersonalInfoTab student={student} onSaved={onSaved} />
        ) : tab === 1 ? (
          <StudentNeedsTab student={student} onSaved={onSaved} />
        ) : (
          <ComingSoon label={TAB_LABELS[tab]} />
        )}
      </div>
    </div>
  );
}
