import { Button } from "@/components/ui/button";
import { serverUnavailable } from "@/messages";

/** Stands in for data that is not on screen yet: loading, or a failure with a retry. */
export function LoadStatus({
  status,
  onRetry,
}: {
  status: "loading" | "error" | "loaded";
  onRetry: () => void;
}) {
  if (status === "loading") {
    return <p className="text-muted-foreground">Загрузка…</p>;
  }
  if (status === "error") {
    return <LoadError onRetry={onRetry} />;
  }
  return null;
}

/** Shown instead of a list when the server could not be reached. */
function LoadError({ onRetry }: { onRetry: () => void }) {
  return (
    <div role="alert" className="flex flex-col items-start gap-3 rounded-xl border p-6">
      <p>{serverUnavailable}</p>
      <Button variant="outline" onClick={onRetry}>
        Повторить
      </Button>
    </div>
  );
}
