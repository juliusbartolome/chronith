import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
  env: {
    CHRONITH_API_URL: process.env.CHRONITH_API_URL ?? "http://localhost:5001",
  },
};

export default nextConfig;
