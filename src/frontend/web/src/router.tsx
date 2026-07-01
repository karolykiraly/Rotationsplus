import { createBrowserRouter } from "react-router-dom";
import { PublicLayout } from "./public/PublicLayout";
import { LandingPage } from "./public/LandingPage";
import { ForPreceptorsPage } from "./public/ForPreceptorsPage";
import { ConsultingServicesPage } from "./public/ConsultingServicesPage";
import { AboutPage } from "./public/AboutPage";
import { OurProcessPage } from "./public/OurProcessPage";
import { OurTeamPage } from "./public/OurTeamPage";
import { FaqPage } from "./public/FaqPage";
import { ResourcesPage } from "./public/ResourcesPage";
import { PrivacyPolicyPage } from "./public/PrivacyPolicyPage";
import { TermsPage } from "./public/TermsPage";
import { PublicComingSoon } from "./public/PublicComingSoon";
import { StaffMsalShell } from "./components/StaffMsalShell";
import { StaffLoginLauncher } from "./components/StaffLoginLauncher";
import { PostLoginRedirect } from "./components/PostLoginRedirect";
import { AppLayout } from "./components/AppLayout";
import { SpecialtiesPage } from "./specialties/SpecialtiesPage";
import { ProgramsPage } from "./programs/ProgramsPage";
import { DashboardPage } from "./dashboard/DashboardPage";
import { PermissionPage } from "./preceptors/PermissionPage";
import { RotationsPage } from "./rotations/RotationsPage";
import { HonorariumPage } from "./honorarium/HonorariumPage";
import { ContactsPage } from "./contacts/ContactsPage";
import { StudentProfilePage } from "./students/StudentProfilePage";
import { CustomerMsalShell } from "./portal/CustomerMsalShell";
import { BrowsePage } from "./portal/BrowsePage";
import { ProgramDetailPage } from "./portal/ProgramDetailPage";
import { MyRotationsPage } from "./portal/MyRotationsPage";

/** Three independent top-level branches, each with its own (or no) MSAL provider:
 *  - "/"            PUBLIC marketing site — anonymous, NO MsalProvider (PublicLayout).
 *  - staff          WORKFORCE MSAL (StaffMsalShell → Outlet): the /rotationsplusadmin|sales|sdr login
 *                   launchers + the authenticated "/admin/*" console (the workforce redirect target).
 *  - "/portal"      CUSTOMER (CIAM) MSAL — Student/Preceptor portal.
 *  The two MSAL instances never share a redirect hash: workforce lands on /admin, customer on /portal,
 *  and the public root never mounts a provider (see main.tsx + authConfig.ts). */
export const router = createBrowserRouter([
  {
    path: "/",
    element: <PublicLayout />,
    children: [
      { index: true, element: <LandingPage /> },
      { path: "about", element: <AboutPage /> },
      { path: "our-process", element: <OurProcessPage /> },
      { path: "our-team", element: <OurTeamPage /> },
      { path: "for-preceptors", element: <ForPreceptorsPage /> },
      { path: "consulting-services", element: <ConsultingServicesPage /> },
      { path: "faq", element: <FaqPage /> },
      // Blog deferred (content-source TBD) — placeholder keeps the nav link live.
      { path: "blog", element: <PublicComingSoon title="Blog" /> },
      { path: "resources", element: <ResourcesPage /> },
      { path: "privacy-policy", element: <PrivacyPolicyPage /> },
      { path: "terms", element: <TermsPage /> }
    ]
  },
  {
    element: <StaffMsalShell />,
    children: [
      { path: "rotationsplusadmin", element: <StaffLoginLauncher entry="admin" /> },
      { path: "rotationsplussales", element: <StaffLoginLauncher entry="sales" /> },
      { path: "rotationsplussdr", element: <StaffLoginLauncher entry="sdr" /> },
      {
        path: "admin",
        element: <AppLayout />,
        children: [
          { index: true, element: <PostLoginRedirect /> },
          { path: "dashboard", element: <DashboardPage /> },
          { path: "specialties", element: <SpecialtiesPage /> },
          { path: "programs", element: <ProgramsPage /> },
          // The Students + Preceptors directories now live as tabs inside the Contacts hub (matching
          // production, which has no standalone Students/Preceptors nav items); their per-id profile
          // pages arrive in later slices.
          { path: "contacts", element: <ContactsPage /> },
          { path: "students/:id", element: <StudentProfilePage /> },
          { path: "permission", element: <PermissionPage /> },
          { path: "rotations", element: <RotationsPage /> },
          { path: "honorarium", element: <HonorariumPage /> }
        ]
      }
    ]
  },
  {
    path: "/portal",
    element: <CustomerMsalShell />,
    children: [
      { index: true, element: <BrowsePage /> },
      { path: "programs/:id", element: <ProgramDetailPage /> },
      { path: "rotations", element: <MyRotationsPage /> }
    ]
  }
]);
