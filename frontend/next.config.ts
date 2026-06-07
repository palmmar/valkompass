import type { NextConfig } from "next";

// Backend-API:t. I dev proxas klientanrop till /api/* hit så att cookies blir same-origin.
const backendUrl = process.env.BACKEND_URL ?? "http://localhost:5208";

const nextConfig: NextConfig = {
  async rewrites() {
    return [{ source: "/api/:path*", destination: `${backendUrl}/api/:path*` }];
  },
};

export default nextConfig;
