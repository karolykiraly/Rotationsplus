import { useState } from "react";
import { useMe } from "../useMe";
import { Tabs } from "../components/Tabs";
import { StudentsPage } from "../students/StudentsPage";
import { PreceptorsPage } from "../preceptors/PreceptorsPage";

/** The production admin "Contacts" hub (legacy AdminAchievements) — one screen with tabs over the
 *  customer directories. Admin sees Students / Preceptors / Sales / SDR / Contacts. Slice 1 wires the
 *  shell + the two built directories (Students, Preceptors) as the first tabs; Sales / SDR / Contacts
 *  are their own later slices (each needs new backend), shown here as a clearly-labelled placeholder so
 *  the tab structure matches production while the content lands incrementally. */
const TAB_LABELS = ["Students", "Preceptors", "Sales", "SDR", "Contacts"];

function ComingSoon({ label }: { label: string }) {
  return (
    <div className="lead-page state">
      The <strong>{label}</strong> tab is being ported next — it needs its own data and isn’t available yet.
    </div>
  );
}

export function ContactsPage() {
  const { user } = useMe();
  const [tab, setTab] = useState(0);

  if (user && !user.isAdmin) {
    return <div className="lead-page state">You need the Admin role to view contacts.</div>;
  }

  return (
    <>
      <div className="lead-page">
        <h2 className="heading-xxs">List of Existing Customers</h2>
        <Tabs labels={TAB_LABELS} active={tab} onChange={setTab} />
      </div>

      {tab === 0 ? (
        <StudentsPage />
      ) : tab === 1 ? (
        <PreceptorsPage />
      ) : (
        <ComingSoon label={TAB_LABELS[tab]} />
      )}
    </>
  );
}
