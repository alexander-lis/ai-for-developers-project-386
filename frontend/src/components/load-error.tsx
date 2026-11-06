import { Button } from "@/components/ui/button";

/** Shown instead of a list when the server could not be reached. */
export function LoadError({ onRetry }: { onRetry: () => void }) {
  return (
    <div role="alert" className="flex flex-col items-start gap-3 rounded-xl border p-6">
      <p>Не удалось связаться с сервером, попробуйте ещё раз</p>
      <Button variant="outline" onClick={onRetry}>
        Повторить
      </Button>
    </div>
  );
}
