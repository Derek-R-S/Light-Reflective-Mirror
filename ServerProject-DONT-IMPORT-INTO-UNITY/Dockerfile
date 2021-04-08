FROM mcr.microsoft.com/dotnet/sdk:5.0-alpine AS builder
WORKDIR /lrm
COPY . .
ARG BUILD_MODE="Release"
RUN dotnet publish \
    --output /build/ \
    --configuration $BUILD_MODE \
    --no-self-contained .

FROM mcr.microsoft.com/dotnet/runtime:5.0-alpine
WORKDIR /lrm
COPY --from=builder /build/ .
ENV LRM_CONFIG_PATH="/config/config.json"
CMD [ "./LRM" ]
ENTRYPOINT [ "./LRM" ]
