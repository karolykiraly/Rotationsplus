import { createBrowserRouter } from "react-router-dom";
import { PublicLayout } from "./public/PublicLayout";
import { LandingPage } from "./public/LandingPage";
import { PublicComingSoon } from "./public/PublicComingSoon";
import { StaffMsalShell } from "./components/StaffMsalShell";
import { StaffLoginLauncher } from "./components/StaffLoginLauncher";
import { PostLoginRedirect } from "./components/PostLoginRedirect";
import { AppLayout } from "./components/AppLayout";
import { SpecialtiesPage } from "./specialties/SpecialtiesPage";
import { ProgramsPage } from "./programs/ProgramsPage";
import { DashboardPage } from "./dashboard/DashboardPage";
import { PreceptorsPage } from "./preceptors/PreceptorsPage";
import { PermissionPage } from "./preceptors/PermissionPage";
import { RotationsPage } from "./rotations/RotationsPage";
import { HonorariumPage } from "./honorarium/HonorariumPage";
import { StudentsPage } from "./students/StudentsPage";
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
      // Marketing pages built in later PRs (LP-2…LP-6); placeholders keep the nav/footer links live.
      { path: "about", element: <PublicComingSoon title="About" /> },
      { path: "our-process", element: <PublicComingSoon title="Our Process" /> },
      { path: "our-team", element: <PublicComingSoon title="Our Team" /> },
      { path: "for-preceptors", element: <PublicComingSoon title="For Preceptors" /> },
      { path: "consulting-services", element: <PublicComingSoon title="Consulting Services" /> },
      { path: "faq", element: <PublicComingSoon title="Frequently Asked Questions" /> },
      { path: "blog", element: <PublicComingSoon title="Blog" /> },
      { path: "resources", element: <PublicComingSoon title="Resources" /> },
      { path: "privacy-policy", element: <PublicComingSoon title="Privacy Policy" /> },
      { path: "terms", element: <PublicComingSoon title="Terms of Service" /> }
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
          { path: "preceptors", element: <PreceptorsPage /> },
          { path: "permission", element: <PermissionPage /> },
          { path: "rotations", element: <RotationsPage /> },
          { path: "honorarium", element: <HonorariumPage /> },
          { path: "students", element: <StudentsPage /> }
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
