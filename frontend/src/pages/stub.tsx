export function StubPage({ title }: { title: string }) {
  return (
    <div className="py-20">
      <h1 className="text-3xl font-semibold tracking-tight">{title}</h1>
      <p className="mt-2 text-muted-foreground">Экран в разработке.</p>
    </div>
  );
}
