#!/usr/bin/env node
// Post-processes @hey-api/openapi-ts legacy/fetch generated files to add .js
// extensions to relative imports, as required by NodeNext module resolution.

import { readdirSync, readFileSync, writeFileSync } from "fs";
import { join } from "path";

const generatedDir = new URL("../src/generated", import.meta.url).pathname;

function fixFile(filePath) {
  const content = readFileSync(filePath, "utf8");
  // Add .js to relative imports that lack an extension
  const fixed = content.replace(
    /from '(\.\.?\/[^']+)(?<!\.js)(?<!\.json)'/g,
    (match, p1) => `from '${p1}.js'`
  );
  if (fixed !== content) {
    writeFileSync(filePath, fixed, "utf8");
    console.log(`Fixed: ${filePath}`);
  }
}

function walk(dir) {
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) {
      walk(full);
    } else if (entry.name.endsWith(".ts")) {
      fixFile(full);
    }
  }
}

walk(generatedDir);
console.log("Done fixing import extensions.");
