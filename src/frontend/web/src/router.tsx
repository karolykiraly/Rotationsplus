import { createBrowserRouter } from "react-router-dom";
import { StaffMsalShell } from "./components/StaffMsalShell";
import { HomePage } from "./pages/HomePage";
import { SpecialtiesPage } from "./specialties/SpecialtiesPage";
import { ProgramsPage } from "./programs/ProgramsPage";
import { PreceptorsPage } from "./preceptors/PreceptorsPage";
import { RotationsPage } from "./rotations/RotationsPage";
import { StudentsPage } from "./students/StudentsPage";
import { CustomerMsalShell } from "./portal/CustomerMsalShell";
import { BrowsePage } from "./portal/BrowsePage";
import { ProgramDetailPage } from "./portal/ProgramDetailPage";
import { MyRotationsPage } from "./portal/MyRotationsPage";

/** Routes. "/" + "/admin/*" are the staff console (workforce MSAL, from main.tsx). "/portal/*" is the
 *  customer-facing portal, rooted on the CIAM MSAL instance via CustomerMsalShell. */
export const router = createBrowserRouter([
  {
    path: "/",
    element: <StaffMsalShell />,
    children: [
      { index: true, element: <HomePage /> },
      { path: "admin/specialties", element: <SpecialtiesPage /> },
      { path: "admin/programs", element: <ProgramsPage /> },
      { path: "admin/preceptors", element: <PreceptorsPage /> },
      { path: "admin/rotations", element: <RotationsPage /> },
      { path: "admin/students", element: <StudentsPage /> }
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
