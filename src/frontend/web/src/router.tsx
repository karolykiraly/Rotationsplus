import { createBrowserRouter } from "react-router-dom";
import { AppLayout } from "./components/AppLayout";
import { HomePage } from "./pages/HomePage";
import { SpecialtiesPage } from "./specialties/SpecialtiesPage";
import { ProgramsPage } from "./programs/ProgramsPage";
import { PreceptorsPage } from "./preceptors/PreceptorsPage";

/** Staff console routes. The layout owns the auth gate; children render inside it. */
export const router = createBrowserRouter([
  {
    path: "/",
    element: <AppLayout />,
    children: [
      { index: true, element: <HomePage /> },
      { path: "admin/specialties", element: <SpecialtiesPage /> },
      { path: "admin/programs", element: <ProgramsPage /> },
      { path: "admin/preceptors", element: <PreceptorsPage /> }
    ]
  }
]);
