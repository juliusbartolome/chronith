/** RFC 7807 Problem Details */
export interface ProblemDetails {
  type?: string;
  title: string;
  status: number;
  detail?: string;
  errors?: Record<string, string[]>;
}

export class ChronithApiError extends Error {
  readonly status: number;
  readonly title: string;
  readonly detail: string | undefined;
  readonly errors: Record<string, string[]> | undefined;

  constructor(problem: ProblemDetails) {
    super(problem.title);
    this.name = "ChronithApiError";
    this.status = problem.status;
    this.title = problem.title;
    this.detail = problem.detail;
    this.errors = problem.errors;
  }
}

export function parseProblemDetails(body: unknown): ChronithApiError | null {
  if (
    typeof body === "object" &&
    body !== null &&
    "status" in body &&
    "title" in body
  ) {
    return new ChronithApiError(body as ProblemDetails);
  }
  return null;
}

export class ChronithNotFoundError extends ChronithApiError {
  constructor(message = "Resource not found") {
    super({ title: message, status: 404 });
    this.name = "ChronithNotFoundError";
  }
}

export class ChronithUnauthorizedError extends ChronithApiError {
  constructor(message = "Unauthorized") {
    super({ title: message, status: 401 });
    this.name = "ChronithUnauthorizedError";
  }
}

export class ChronithValidationError extends ChronithApiError {
  constructor(
    message = "Validation failed",
    validationErrors?: Record<string, string[]>,
  ) {
    super({ title: message, status: 422, errors: validationErrors });
    this.name = "ChronithValidationError";
  }
}

/** @deprecated Use ChronithApiError instead */
export class ChronithError extends ChronithApiError {
  constructor(message: string, statusCode = 0) {
    super({ title: message, status: statusCode });
    this.name = "ChronithError";
  }
}
