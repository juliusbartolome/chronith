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

export class ChronithError extends Error {
  constructor(
    message: string,
    public readonly statusCode?: number,
  ) {
    super(message);
    this.name = "ChronithError";
  }
}

export class ChronithNotFoundError extends ChronithError {
  constructor(message = "Resource not found") {
    super(message, 404);
    this.name = "ChronithNotFoundError";
  }
}

export class ChronithUnauthorizedError extends ChronithError {
  constructor(message = "Unauthorized") {
    super(message, 401);
    this.name = "ChronithUnauthorizedError";
  }
}

export class ChronithValidationError extends ChronithError {
  constructor(
    message = "Validation failed",
    public readonly errors?: Record<string, string[]>,
  ) {
    super(message, 422);
    this.name = "ChronithValidationError";
  }
}
