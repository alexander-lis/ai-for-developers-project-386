import { Navigate, Outlet, Route, Routes } from "react-router";
import { SiteHeader } from "@/components/site-header";
import { Toaster } from "@/components/ui/sonner";
import { BookPage } from "@/pages/book";
import { HomePage } from "@/pages/home";
import { MeetingTypesPage } from "@/pages/meeting-types";
import { MeetingsPage } from "@/pages/meetings";
import { useRole, type Role } from "@/role";
import { RoleProvider } from "@/role-provider";
import { paths } from "@/routes";

function Layout() {
  return (
    <>
      <SiteHeader />
      <main className="mx-auto max-w-5xl px-6">
        <Outlet />
      </main>
      {/* The app has no dark theme: "system" would darken toasts on a dark OS. */}
      <Toaster theme="light" />
    </>
  );
}

function RequireRole({ role }: { role: Role }) {
  const current = useRole().role;
  return current === role ? <Outlet /> : <Navigate to={paths.home} replace />;
}

function App() {
  return (
    <RoleProvider>
      <Routes>
        <Route element={<Layout />}>
          <Route index element={<HomePage />} />
          <Route element={<RequireRole role="guest" />}>
            <Route path={paths.book} element={<BookPage />} />
            <Route path={paths.meetings} element={<MeetingsPage title="Мои встречи" />} />
          </Route>
          <Route element={<RequireRole role="owner" />}>
            <Route path={paths.ownerMeetingTypes} element={<MeetingTypesPage />} />
            <Route path={paths.ownerMeetings} element={<MeetingsPage title="Встречи" />} />
          </Route>
          <Route path="*" element={<Navigate to={paths.home} replace />} />
        </Route>
      </Routes>
    </RoleProvider>
  );
}

export default App;
