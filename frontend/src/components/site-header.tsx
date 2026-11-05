import { CalendarDays } from "lucide-react";
import { Link, NavLink } from "react-router";
import { cn } from "cn";
import { Button, buttonVariants } from "@/components/ui/button";
import { useRole } from "@/role";
import { navigation, paths } from "@/routes";

export function SiteHeader() {
  const { role, setRole } = useRole();

  return (
    <header className="border-b">
      <div className="mx-auto flex h-14 max-w-5xl items-center gap-6 px-6">
        <Link to={paths.home} className="flex items-center gap-2 font-semibold">
          <CalendarDays className="size-5" />
          Календарь звонков
        </Link>
        <nav aria-label="Основная навигация" className="flex flex-1 items-center gap-1">
          {navigation[role].map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end
              className={({ isActive }) =>
                cn(
                  buttonVariants({ variant: "ghost", size: "sm" }),
                  isActive && "bg-muted text-foreground",
                )
              }
            >
              {item.label}
            </NavLink>
          ))}
        </nav>
        {role === "owner" ? (
          <Button variant="outline" size="sm" onClick={() => setRole("guest")}>
            Выйти
          </Button>
        ) : (
          <Button variant="outline" size="sm" onClick={() => setRole("owner")}>
            Войти как владелец
          </Button>
        )}
      </div>
    </header>
  );
}
