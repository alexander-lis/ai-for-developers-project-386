import { useEffect, useState, type ReactNode } from "react";
import { RoleContext, type Role } from "@/role";

const STORAGE_KEY = "calendar.role";

function readRole(): Role {
  return localStorage.getItem(STORAGE_KEY) === "owner" ? "owner" : "guest";
}

export function RoleProvider({ children }: { children: ReactNode }) {
  const [role, setRole] = useState<Role>(readRole);

  useEffect(() => {
    localStorage.setItem(STORAGE_KEY, role);
  }, [role]);

  return <RoleContext value={{ role, setRole }}>{children}</RoleContext>;
}
