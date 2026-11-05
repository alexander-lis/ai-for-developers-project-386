import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { afterEach, beforeEach, expect, it } from "vitest";
import App from "./App";

function renderApp(path = "/") {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>,
  );
}

beforeEach(() => {
  localStorage.clear();
});

// Без globals: true в Vitest Testing Library не чистит DOM сама
afterEach(cleanup);

it("shows the home page", () => {
  renderApp();

  expect(screen.getByRole("heading", { level: 1 }).textContent).toContain(
    "встреча уже в календаре",
  );
  expect(screen.getByRole("link", { name: "Записаться на звонок" })).toBeTruthy();
});

it("opens the booking page from the home page", async () => {
  renderApp();

  await userEvent.click(screen.getByRole("link", { name: "Записаться на звонок" }));

  expect(screen.getByRole("heading", { level: 1 }).textContent).toBe("Запись");
});

it("switches navigation when entering as owner", async () => {
  renderApp();

  await userEvent.click(screen.getByRole("button", { name: "Войти как владелец" }));

  const nav = screen.getByRole("navigation", { name: "Основная навигация" });
  expect(nav.textContent).toContain("Типы встреч");
  expect(nav.textContent).not.toContain("Запись");
  expect(screen.queryByRole("link", { name: "Записаться на звонок" })).toBeNull();
  expect(screen.getByRole("button", { name: "Выйти" })).toBeTruthy();
});
