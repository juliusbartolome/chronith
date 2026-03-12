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
