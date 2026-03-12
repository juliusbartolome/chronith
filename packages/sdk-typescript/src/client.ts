import { OpenAPI } from "./generated/core/OpenAPI.js";

export interface ChronithClientOptions {
  baseUrl: string;
}

export class ChronithClient {
  readonly correlationId: string;

  private _token: string | null = null;
  private _apiKey: string | null = null;

  constructor(options: ChronithClientOptions) {
    if (!options.baseUrl) {
      throw new Error("baseUrl is required");
    }

    this.correlationId = crypto.randomUUID();

    OpenAPI.BASE = options.baseUrl;
    OpenAPI.HEADERS = async () => {
      const authHeader = this.getAuthHeader();
      if (authHeader) {
        return { Authorization: authHeader };
      }
      return {} as Record<string, string>;
    };
  }

  setToken(token: string): void {
    this._token = token;
    this._apiKey = null;
  }

  setApiKey(apiKey: string): void {
    this._apiKey = apiKey;
    this._token = null;
  }

  clearAuth(): void {
    this._token = null;
    this._apiKey = null;
  }

  getAuthHeader(): string | null {
    if (this._token) {
      return `Bearer ${this._token}`;
    }
    if (this._apiKey) {
      return `ApiKey ${this._apiKey}`;
    }
    return null;
  }
}
