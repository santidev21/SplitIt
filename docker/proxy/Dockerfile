# Production reverse proxy for SplitIt
# Based on nginx:alpine. Serves as the ONLY internet-facing container.
# Exposes 80 (ACME + redirect) and 443 (HTTPS + application traffic).
FROM nginx:1.27-alpine

# Install openssl for self-signed certificate generation in local/test mode.
# gettext provides envsubst for rendering the nginx template at runtime.
RUN apk add --no-cache openssl gettext && apk upgrade --no-cache

# Create non-root-friendly directories and set ownership.
# Nginx alpine image provides nginx user (UID 101) / group (GID 101).
RUN mkdir -p /etc/nginx/snippets /var/www/certbot /etc/nginx/ssl /var/cache/nginx /var/log/nginx && \
    touch /var/run/nginx.pid && \
    chown -R nginx:nginx /etc/nginx /var/www/certbot /etc/nginx/ssl /var/cache/nginx /var/log/nginx /var/run/nginx.pid

# Copy configuration template, snippets and entrypoint.
COPY docker/proxy/nginx.conf.template /etc/nginx/nginx.conf.template
COPY docker/proxy/snippets/* /etc/nginx/snippets/
COPY docker/proxy/entrypoint.sh /usr/local/bin/entrypoint.sh
RUN chmod +x /usr/local/bin/entrypoint.sh && \
    chown nginx:nginx /usr/local/bin/entrypoint.sh /etc/nginx/nginx.conf.template

# Do not copy certificates into the image. They are mounted at runtime.
# Do not copy .env or secrets into the image.

USER nginx

EXPOSE 8080 8443

ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
CMD ["nginx", "-g", "daemon off;"]
