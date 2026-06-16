import { createBrowserRouter } from "react-router-dom";
import { AppLayout } from "./components/AppLayout";
import { HomePage } from "./pages/HomePage";
import { SpecialtiesPage } from "./specialties/SpecialtiesPage";

/** Staff console routes. The layout owns the auth gate; children render inside it. */
export const router = createBrowserRouter([
  {
    path: "/",
    element: <AppLayout />,
    children: [
      { index: true, element: <HomePage /> },
      { path: "admin/specialties", element: <SpecialtiesPage /> }
    ]
  }
]);
