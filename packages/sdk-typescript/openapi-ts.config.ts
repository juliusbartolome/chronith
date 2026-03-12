import { defineConfig } from "@hey-api/openapi-ts";

export default defineConfig({
  client: "legacy/fetch",
  input: "./openapi.json",
  output: {
    path: "./src/generated",
    format: "prettier",
    tsConfigFile: "./tsconfig.json",
  },
  plugins: ["@hey-api/types", "@hey-api/services"],
});
