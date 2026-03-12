import { describe, it, expect, vi, beforeEach } from "vitest";
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
    it("stores the token for use in requests", () => {
      const client = new ChronithClient({ baseUrl: "https://api.example.com" });
      client.setToken("my-jwt-token");
      expect(client.getAuthHeader()).toBe("Bearer my-jwt-token");
    });
  });

  describe("setApiKey", () => {
    it("stores the API key for use in requests", () => {
      const client = new ChronithClient({ baseUrl: "https://api.example.com" });
      client.setApiKey("my-api-key");
      expect(client.getAuthHeader()).toBe("ApiKey my-api-key");
    });
  });

  describe("clearAuth", () => {
    it("removes stored credentials", () => {
      const client = new ChronithClient({ baseUrl: "https://api.example.com" });
      client.setToken("my-jwt");
      client.clearAuth();
      expect(client.getAuthHeader()).toBeNull();
    });
  });

  describe("correlation ID", () => {
    it("generates a unique correlation ID per instance", () => {
      const a = new ChronithClient({ baseUrl: "https://api.example.com" });
      const b = new ChronithClient({ baseUrl: "https://api.example.com" });
      expect(a.correlationId).not.toBe(b.correlationId);
    });
  });
});
