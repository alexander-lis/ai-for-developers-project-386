import { createContext, useContext } from "react";

export type Role = "guest" | "owner";

export interface RoleState {
  role: Role;
  setRole: (role: Role) => void;
}

export const RoleContext = createContext<RoleState | null>(null);

export function useRole(): RoleState {
  const state = useContext(RoleContext);
  if (!state) {
    throw new Error("useRole вызван вне RoleProvider");
  }
  return state;
}
