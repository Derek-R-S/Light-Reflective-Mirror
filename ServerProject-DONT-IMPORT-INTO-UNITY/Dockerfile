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
WORKDIR /lrm
COPY --from=builder /build/ .
COPY ./MultiCompiled.dll .
ENV LRM_CONFIG_PATH="/config/config.json"
CMD [ "./LRM" ]
ENTRYPOINT [ "./LRM" ]