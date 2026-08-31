# Multi-stage Dockerfile for SplitIt Angular SPA Frontend
# Stage 1: Build Angular application using Node.js
FROM node:20-alpine AS build
WORKDIR /app

ARG GOOGLE_CLIENT_ID=
ENV GOOGLE_CLIENT_ID=${GOOGLE_CLIENT_ID}

# Copy package definition files to leverage Docker layer caching
COPY split-it-ui/package.json split-it-ui/package-lock.json ./
RUN npm ci --legacy-peer-deps

# Copy full frontend source code
COPY split-it-ui/ ./

# Generate environment.ts from env var
RUN node scripts/set-env.js

# Build production distribution
RUN npm run build -- --configuration production

# Stage 2: Serve static files with Nginx Alpine
FROM nginx:1.27-alpine AS final

# Install wget and upgrade Alpine packages to patch base OS vulnerabilities
RUN apk add --no-cache wget && apk upgrade --no-cache

# Copy custom Nginx configuration
COPY docker/frontend/nginx.conf /etc/nginx/conf.d/default.conf

# Remove default static files and copy compiled Angular output
RUN rm -rf /usr/share/nginx/html/*
COPY --from=build /app/dist/split-it-ui/browser /usr/share/nginx/html

# Remove Angular's media="print" onload optimization from index.html.
# This optimization uses an inline event handler (onload) which is blocked by CSP script-src 'self'.
# Without this fix, the global CSS applies only to print media and never switches to screen.
RUN sed -i 's/ media="print" onload="this\.media=.all."//g' /usr/share/nginx/html/index.html

# Adjust permissions for non-root execution
RUN chown -R nginx:nginx /usr/share/nginx/html && \
    chmod -R 755 /usr/share/nginx/html && \
    touch /var/run/nginx.pid && \
    chown -R nginx:nginx /var/run/nginx.pid /var/cache/nginx /var/log/nginx

USER nginx

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=5s --start-period=5s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:80/ || exit 1

CMD ["nginx", "-g", "daemon off;"]
