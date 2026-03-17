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
      return {
        "X-Correlation-Id": this.correlationId,
        ...this.getAuthHeaders(),
      };
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

  getAuthHeaders(): Record<string, string> {
    if (this._token) {
      return { Authorization: `Bearer ${this._token}` };
    }
    if (this._apiKey) {
      return { "X-Api-Key": this._apiKey };
    }
    return {};
  }
}
