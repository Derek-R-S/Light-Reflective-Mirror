FROM mcr.microsoft.com/dotnet/sdk:5.0-alpine AS builder
WORKDIR /lrm
COPY . .
RUN [\
    "dotnet", "publish", \
    "--output", "/build/", \
    "--configuration", "Release",\
    "--no-self-contained", \
    "." ]

FROM mcr.microsoft.com/dotnet/runtime:5.0-alpine
WORKDIR /lrm-lb
COPY --from=builder /build/ .
ENV LRM_LB_CONFIG_PATH="/config/config-load-balancer.json"
CMD [ "./LRM_LoadBalancer" ]
ENTRYPOINT [ "./LRM_LoadBalancer" ]