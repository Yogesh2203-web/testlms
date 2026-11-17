const fs = require("node:fs");
const path = require("node:path");
const apiUrl = (process.env.LMS_API_BASE_URL || "").trim().replace(/\/$/, "");
if (!apiUrl) {
  if (process.env.VERCEL === "1" || process.env.CI === "true") {
    throw new Error("Set LMS_API_BASE_URL in Vercel Environment Variables to your deployed HTTPS API origin.");
  }
  console.log("LMS_API_BASE_URL is not set; keeping the local development API URL.");
  process.exit(0);
}
if (!/^https:\/\//i.test(apiUrl) && !/^http:\/\/localhost(?::\d+)?$/i.test(apiUrl)) {
  throw new Error("LMS_API_BASE_URL must use HTTPS (HTTP is allowed only for localhost development).");
}
const output = `// Generated during build. This file contains a public API URL, never secrets.\nwindow.LMS_CONFIG = { API_BASE_URL: ${JSON.stringify(apiUrl)} };\n`;
fs.writeFileSync(path.join(__dirname, "..", "public", "lms-config.js"), output, "utf8");
console.log(`Configured LMS API: ${apiUrl}`);
