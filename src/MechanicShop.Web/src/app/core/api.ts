import { HttpErrorResponse } from '@angular/common/http';

/** The one user-facing string out of an API failure. Staff actions fail with
 * ProblemDetails + a detail the domain already worded (conflict rules, etc.),
 * so surface that detail instead of a generic message. */
export function problemMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const body = error.error as
      | { detail?: string; errors?: Record<string, string[]> }
      | null;

    // ValidationProblem: the field-to-messages map is the real content.
    if (body?.errors) {
      const fieldErrors = Object.values(body.errors).flat();

      if (fieldErrors.length > 0) {
        return fieldErrors.join(' ');
      }
    }

    if (typeof body?.detail === 'string' && body.detail.length > 0) {
      return body.detail;
    }

    if (typeof error.error === 'string' && error.error.length > 0) {
      return error.error;
    }
  }

  return 'Something went wrong. Try again.';
}
