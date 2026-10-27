FROM golang:1.27.3-bookworm AS build
WORKDIR /src
COPY collector/go.mod ./
COPY collector/ ./
RUN CGO_ENABLED=0 GOOS=linux go build -trimpath -ldflags="-s -w" -o /out/nas-audit-collector ./cmd/nas-audit-collector

FROM debian:bookworm-slim AS final
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates systemd \
    && rm -rf /var/lib/apt/lists/* \
    && install -d -o 1655 -g 1655 -m 0700 /var/lib/nas-audit-collector
COPY --from=build /out/nas-audit-collector /usr/local/bin/nas-audit-collector
USER 1655:1655
EXPOSE 8081
ENTRYPOINT ["/usr/local/bin/nas-audit-collector"]
