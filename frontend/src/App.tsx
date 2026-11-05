import { Navigate, Outlet, Route, Routes } from "react-router";
import { SiteHeader } from "@/components/site-header";
import { HomePage } from "@/pages/home";
import { StubPage } from "@/pages/stub";
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
            <Route path={paths.book} element={<StubPage title="Запись" />} />
            <Route path={paths.meetings} element={<StubPage title="Мои встречи" />} />
          </Route>
          <Route element={<RequireRole role="owner" />}>
            <Route path={paths.ownerMeetingTypes} element={<StubPage title="Типы встреч" />} />
            <Route path={paths.ownerMeetings} element={<StubPage title="Встречи" />} />
          </Route>
          <Route path="*" element={<Navigate to={paths.home} replace />} />
        </Route>
      </Routes>
    </RoleProvider>
  );
}

export default App;
