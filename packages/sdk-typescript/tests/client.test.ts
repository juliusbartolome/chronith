import { describe, it, expect } from "vitest";
import { ChronithClient } from "../src/client.js";

describe("ChronithClient", () => {
  describe("constructor", () => {
    it("requires baseUrl", () => {
      expect(() => new ChronithClient({ baseUrl: "" })).toThrow();
    });

    it("accepts valid baseUrl", () => {
      const client = new ChronithClient({ baseUrl: "https://api.example.com" });
      expect(client).toBeDefined();
    });
  });

  describe("setToken", () => {
    it("sets Authorization Bearer header for JWT auth", () => {
      const client = new ChronithClient({ baseUrl: "https://api.example.com" });
      client.setToken("my-jwt-token");
      expect(client.getAuthHeaders()).toEqual({
        Authorization: "Bearer my-jwt-token",
      });
    });

    it("clears a previously set API key", () => {
      const client = new ChronithClient({ baseUrl: "https://api.example.com" });
      client.setApiKey("old-key");
      client.setToken("new-token");
      const headers = client.getAuthHeaders();
      expect(headers["Authorization"]).toBe("Bearer new-token");
      expect(headers["X-Api-Key"]).toBeUndefined();
    });
  });

  describe("setApiKey", () => {
    it("sets X-Api-Key header for API key auth", () => {
      const client = new ChronithClient({ baseUrl: "https://api.example.com" });
      client.setApiKey("my-api-key");
      expect(client.getAuthHeaders()).toEqual({ "X-Api-Key": "my-api-key" });
    });

    it("clears a previously set token", () => {
      const client = new ChronithClient({ baseUrl: "https://api.example.com" });
      client.setToken("old-token");
      client.setApiKey("new-key");
      const headers = client.getAuthHeaders();
      expect(headers["X-Api-Key"]).toBe("new-key");
      expect(headers["Authorization"]).toBeUndefined();
    });
  });

  describe("clearAuth", () => {
    it("removes stored credentials", () => {
      const client = new ChronithClient({ baseUrl: "https://api.example.com" });
      client.setToken("my-jwt");
      client.clearAuth();
      expect(client.getAuthHeaders()).toEqual({});
    });
  });

  describe("correlation ID", () => {
    it("generates a unique correlation ID per instance", () => {
      const a = new ChronithClient({ baseUrl: "https://api.example.com" });
      const b = new ChronithClient({ baseUrl: "https://api.example.com" });
      expect(a.correlationId).not.toBe(b.correlationId);
    });

    it("includes X-Correlation-Id in request headers", async () => {
      const client = new ChronithClient({ baseUrl: "https://api.example.com" });
      // OpenAPI.HEADERS is set as an async resolver — invoke it directly
      const { OpenAPI } = await import("../src/generated/core/OpenAPI.js");
      const headers = await (OpenAPI.HEADERS as () => Promise<Record<string, string>>)();
      expect(headers["X-Correlation-Id"]).toBe(client.correlationId);
    });
  });
});
