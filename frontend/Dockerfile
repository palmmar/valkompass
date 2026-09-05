# syntax=docker/dockerfile:1
# Bygg-/körkontext = mappen "frontend/".

# ---- deps ----
FROM node:22-alpine AS deps
RUN apk add --no-cache libc6-compat
WORKDIR /app
# Bara lockfilen först – ger lager-cachad install som bara körs om beroendena ändras.
COPY package.json package-lock.json ./
RUN npm ci

# ---- build ----
FROM node:22-alpine AS build
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .

ENV NEXT_TELEMETRY_DISABLED=1
# NEXT_PUBLIC_API_BASE_URL lämnas medvetet osatt: tom sträng ger relativa /api-anrop mot
# samma origin som sidan laddades från. Ingen domän bakas in i imagen, så samma tagg kan
# promotas mellan miljöer, och admin-cookien förblir same-origin.
RUN npm run build

# ---- runtime ----
FROM node:22-alpine AS final
WORKDIR /app
ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    PORT=3000 \
    HOSTNAME=0.0.0.0

# curl behövs för HEALTHCHECK nedan; alpine har annars bara wget.
RUN apk add --no-cache curl

# Kör som icke-root.
RUN addgroup --system --gid 1001 nodejs \
 && adduser --system --uid 1001 nextjs

# output: "standalone" i next.config.ts ger en självförsörjande server.js med bara de
# node_modules som faktiskt används. public/ och .next/static kopieras separat.
COPY --from=build /app/public ./public
COPY --from=build --chown=nextjs:nodejs /app/.next/standalone ./
COPY --from=build --chown=nextjs:nodejs /app/.next/static ./.next/static

USER nextjs
EXPOSE 3000

# /health är en force-static route (src/app/health/route.ts) och svarar utan att röra API:t.
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -fsS http://localhost:3000/health || exit 1

CMD ["node", "server.js"]
