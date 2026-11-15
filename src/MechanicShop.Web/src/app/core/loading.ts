import { signal } from '@angular/core';

let pendingCount = 0;

/**
 * Global HTTP loading state. The authInterceptor calls start()/stop()
 * on each request. Components subscribe to the `loading` signal.
 */
export const loadingSignal = signal(false);

export function useLoading() {
  return {
    loading: loadingSignal,
    start: () => {
      pendingCount++;
      loadingSignal.set(true);
    },
    stop: () => {
      pendingCount = Math.max(0, pendingCount - 1);
      if (pendingCount === 0) {
        loadingSignal.set(false);
      }
    },
    isPending: () => pendingCount > 0,
  };
}
