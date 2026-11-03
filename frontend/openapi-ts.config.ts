import { defineConfig } from "@hey-api/openapi-ts";

export default defineConfig({
  input: "../contract/tsp-output/schema/openapi.yaml",
  output: "src/client",
});
