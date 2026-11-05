import { CalendarDays, Clock, ListChecks, type LucideIcon } from "lucide-react";
import { Link } from "react-router";
import { buttonVariants } from "@/components/ui/button";
import { useRole } from "@/role";
import { paths } from "@/routes";

interface Step {
  icon: LucideIcon;
  title: string;
  text: string;
}

const steps: Step[] = [
  {
    icon: ListChecks,
    title: "Выберите тип встречи",
    text: "Владелец заранее описал, о чём и сколько минут можно поговорить.",
  },
  {
    icon: CalendarDays,
    title: "Выберите день",
    text: "Доступны ближайшие 14 дней, в которых ещё есть место под вашу встречу.",
  },
  {
    icon: Clock,
    title: "Займите свободный слот",
    text: "Назовите своё имя и подтвердите. Встреча сразу появится в календаре.",
  },
];

export function HomePage() {
  const { role } = useRole();

  return (
    <div className="flex flex-col gap-20 py-20">
      <section className="flex flex-col items-start gap-6">
        <h1 className="max-w-3xl text-5xl font-semibold tracking-tight text-balance">
          Выберите удобное время, и встреча уже в календаре
        </h1>
        <p className="max-w-2xl text-lg text-muted-foreground">
          Календарь звонков показывает свободные слоты владельца. Выберите тип встречи, день и
          время, назовите имя, и слот ваш.
        </p>
        <div className="flex items-center gap-3">
          {role === "guest" ? (
            <>
              <Link to={paths.book} className={buttonVariants({ size: "lg" })}>
                Записаться на звонок
              </Link>
              <Link
                to={paths.meetings}
                className={buttonVariants({ variant: "ghost", size: "lg" })}
              >
                Мои встречи
              </Link>
            </>
          ) : (
            <Link to={paths.ownerMeetingTypes} className={buttonVariants({ size: "lg" })}>
              Настроить типы встреч
            </Link>
          )}
        </div>
      </section>

      <section className="flex flex-col gap-8">
        <h2 className="text-2xl font-semibold tracking-tight">Как это работает</h2>
        <ol className="grid grid-cols-3 gap-6">
          {steps.map((step, index) => (
            <li key={step.title} className="flex flex-col gap-4 rounded-xl border p-6">
              <div className="flex items-center justify-between">
                <span className="flex size-10 items-center justify-center rounded-lg bg-muted">
                  <step.icon className="size-5" />
                </span>
                <span className="text-sm text-muted-foreground">Шаг {index + 1}</span>
              </div>
              <div className="flex flex-col gap-1">
                <h3 className="font-medium">{step.title}</h3>
                <p className="text-sm text-muted-foreground">{step.text}</p>
              </div>
            </li>
          ))}
        </ol>
      </section>

      <section className="flex items-center justify-between gap-6 rounded-xl bg-muted p-8">
        <div className="flex flex-col gap-1">
          <h2 className="text-xl font-semibold tracking-tight">Это ваш календарь?</h2>
          <p className="text-muted-foreground">
            Войдите как владелец, чтобы настроить типы встреч и посмотреть, кто и когда к вам
            записался.
          </p>
        </div>
        <span className="shrink-0 text-sm text-muted-foreground">
          Кнопка «Войти как владелец» в шапке
        </span>
      </section>
    </div>
  );
}
