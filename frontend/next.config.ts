import type { NextConfig } from "next";

// Backend-API:t. I dev proxas klientanrop till /api/* hit så att cookies blir same-origin.
// I drift fångar ingressen /api före Next, så rewriten används bara lokalt.
const backendUrl = process.env.BACKEND_URL ?? "http://localhost:5208";

const nextConfig: NextConfig = {
  // Fristående server.js med bara de node_modules som faktiskt används – ger en liten
  // container-image. Se frontend/Dockerfile.
  output: "standalone",
  async rewrites() {
    return [{ source: "/api/:path*", destination: `${backendUrl}/api/:path*` }];
  },
};

export default nextConfig;
