import { useEffect, useState } from "react";

/** Data of a screen: on its way, failed to arrive (network or server), or on screen. */
export type LoadState<T> =
  | { status: "loading" }
  | { status: "error" }
  | { status: "loaded"; data: T };

/**
 * Fetches data when the page mounts, with no polling or cache: a page mounts anew on each
 * navigation. `fetchData` resolves to undefined on network and server failures; pass a
 * module-level function, since the data is fetched again whenever it changes.
 */
export function useLoad<T>(fetchData: () => Promise<T | undefined>) {
  const [state, setState] = useState<LoadState<T>>({ status: "loading" });

  useEffect(() => {
    let current = true;
    void fetchData().then((data) => current && setState(toState(data)));
    return () => {
      current = false;
    };
  }, [fetchData]);

  return {
    state,
    /** Shows loading and fetches again: after a failure, or when the data on screen is wrong. */
    reload() {
      setState({ status: "loading" });
      void fetchData().then((data) => setState(toState(data)));
    },
    /** Fetches again, keeping the data on screen until the fresh data arrives. */
    refresh() {
      void fetchData().then((data) => setState(toState(data)));
    },
  };
}

function toState<T>(data: T | undefined): LoadState<T> {
  return data === undefined ? { status: "error" } : { status: "loaded", data };
}
