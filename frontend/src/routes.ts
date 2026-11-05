import type { Role } from "./role";

export interface NavItem {
  to: string;
  label: string;
}

export const paths = {
  home: "/",
  book: "/book",
  meetings: "/meetings",
  ownerMeetingTypes: "/owner/meeting-types",
  ownerMeetings: "/owner/meetings",
} as const;

export const navigation: Record<Role, NavItem[]> = {
  guest: [
    { to: paths.home, label: "Главная" },
    { to: paths.book, label: "Запись" },
    { to: paths.meetings, label: "Мои встречи" },
  ],
  owner: [
    { to: paths.home, label: "Главная" },
    { to: paths.ownerMeetingTypes, label: "Типы встреч" },
    { to: paths.ownerMeetings, label: "Встречи" },
  ],
};
