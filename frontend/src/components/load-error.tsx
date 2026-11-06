import { Button } from "@/components/ui/button";
import { serverUnavailable } from "@/messages";

/** Shown instead of a list when the server could not be reached. */
export function LoadError({ onRetry }: { onRetry: () => void }) {
  return (
    <div role="alert" className="flex flex-col items-start gap-3 rounded-xl border p-6">
      <p>{serverUnavailable}</p>
      <Button variant="outline" onClick={onRetry}>
        Повторить
      </Button>
    </div>
  );
}
