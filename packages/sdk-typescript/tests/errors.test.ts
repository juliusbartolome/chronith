import { describe, it, expect } from "vitest";
import {
  ChronithApiError,
  parseProblemDetails,
} from "../src/errors.js";
import type { ProblemDetails } from "../src/errors.js";

describe("ChronithApiError", () => {
  it("sets name to ChronithApiError", () => {
    const err = new ChronithApiError({ title: "Not Found", status: 404 });
    expect(err.name).toBe("ChronithApiError");
  });

  it("uses title as message", () => {
    const err = new ChronithApiError({ title: "Bad Request", status: 400 });
    expect(err.message).toBe("Bad Request");
  });

  it("exposes status", () => {
    const err = new ChronithApiError({ title: "Forbidden", status: 403 });
    expect(err.status).toBe(403);
  });

  it("exposes detail when provided", () => {
    const err = new ChronithApiError({
      title: "Unprocessable",
      status: 422,
      detail: "Validation failed",
    });
    expect(err.detail).toBe("Validation failed");
  });

  it("exposes errors map when provided", () => {
    const errorsMap = { email: ["Email is required"] };
    const err = new ChronithApiError({
      title: "Unprocessable",
      status: 422,
      errors: errorsMap,
    });
    expect(err.errors).toEqual(errorsMap);
  });

  it("is an instance of Error", () => {
    const err = new ChronithApiError({ title: "Error", status: 500 });
    expect(err).toBeInstanceOf(Error);
  });
});

describe("parseProblemDetails", () => {
  it("returns ChronithApiError for a valid problem details object", () => {
    const body: ProblemDetails = { title: "Not Found", status: 404 };
    const result = parseProblemDetails(body);
    expect(result).toBeInstanceOf(ChronithApiError);
    expect(result?.status).toBe(404);
    expect(result?.title).toBe("Not Found");
  });

  it("returns null for null", () => {
    expect(parseProblemDetails(null)).toBeNull();
  });

  it("returns null for a plain string", () => {
    expect(parseProblemDetails("error")).toBeNull();
  });

  it("returns null when status is missing", () => {
    expect(parseProblemDetails({ title: "Error" })).toBeNull();
  });

  it("returns null when title is missing", () => {
    expect(parseProblemDetails({ status: 400 })).toBeNull();
  });
});
